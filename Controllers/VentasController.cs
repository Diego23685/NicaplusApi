using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.Models;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VentasController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        
        // Zona horaria estándar para Nicaragua
        private static readonly TimeZoneInfo NicaraguaZone = TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");

        public VentasController(ApplicationDbContext context) { _context = context; }

        private DateTime GetNicaraguaTime()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, NicaraguaZone);
        }

        [HttpGet]
        [Authorize(Roles = "Administrador,Socio,Ventas")]
        public async Task<ActionResult<IEnumerable<Venta>>> Get() => 
            await _context.Ventas.Include(v => v.Detalles).Include(v => v.Cliente).OrderByDescending(v => v.Id).ToListAsync();

        [HttpPost]
        public async Task<ActionResult<Venta>> Post([FromBody] Venta venta)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var ahoraNicaragua = GetNicaraguaTime();

                // CORREGIDO: Forzar hora local comercial de Nicaragua
                if (venta.FechaVenta == default(DateTime) || venta.FechaVenta.Year == 1)
                {
                    venta.FechaVenta = ahoraNicaragua;
                }

                // 1. Guardar la venta inicial para generar el ID automático
                _context.Ventas.Add(venta);
                await _context.SaveChangesAsync();

                // 2. Procesar detalles, inventario y registros financieros
                foreach (var detalle in venta.Detalles)
                {
                    var prod = await _context.Productos.FindAsync(detalle.IdProducto);
                    if (prod != null)
                    {
                        // Control de inventario (Físicos)
                        if (!prod.EsDigital && !prod.RequiereServicio)
                        {
                            if (prod.StockActual < detalle.Cantidad)
                                return BadRequest($"Stock insuficiente para: {prod.Nombre}");
                            prod.StockActual -= detalle.Cantidad;
                        }

                        // Lógica de Suscripciones (Streaming, Licencias)
                        if (prod.EsSuscripcion)
                        {
                            if (!venta.IdCliente.HasValue || venta.IdCliente.Value == 0)
                                return BadRequest($"Operación Denegada: El producto '{prod.Nombre}' requiere obligatoriamente un cliente asociado.");

                            // Pool de perfiles
                            var perfilDisponible = await _context.PerfilesCuentas
                                .FirstOrDefaultAsync(p => p.IdProducto == prod.Id && !p.Ocupado);

                            if (perfilDisponible == null)
                            {
                                return BadRequest($"Acción Denegada: No quedan pantallas disponibles en el pool para '{prod.Nombre}'. Ingrese más perfiles en el catálogo antes de facturar.");
                            }

                            perfilDisponible.Ocupado = true;
                            perfilDisponible.IdClienteAsignado = venta.IdCliente.Value;
                            _context.PerfilesCuentas.Update(perfilDisponible);

                            detalle.MetadataDigital = $"PERFIL: {perfilDisponible.NombrePerfil} | PIN: {perfilDisponible.PIN} | Acceso: {perfilDisponible.CorreoCuenta} / {perfilDisponible.PasswordCuenta}";

                            // CORREGIDO: La suscripción hereda los tiempos limpios del huso local del negocio
                            var nuevaSuscripcion = new Suscripcion
                            {
                                IdCliente = venta.IdCliente.Value,
                                NombreServicio = prod.Nombre,
                                TipoSuscripcion = prod.EsDigital ? "Digital" : "Físico",
                                IdProducto = prod.Id,
                                IdPerfilCuenta = perfilDisponible.Id,
                                CostoRenovacion = detalle.PrecioUnitario,
                                FechaInicio = venta.FechaVenta,
                                FechaVencimiento = venta.FechaVenta.AddDays(prod.DiasDuracion > 0 ? prod.DiasDuracion : 30),
                                Estado = "Activa",
                                DetallesCredenciales = detalle.MetadataDigital
                            };
                            _context.Suscripciones.Add(nuevaSuscripcion);
                        }
                    }
                }

                // 3. Registrar Movimiento de Caja sincronizado al día real
                var ingresoCaja = new MovimientoCaja
                {
                    Fecha = venta.FechaVenta,
                    Tipo = "Ingreso",
                    Concepto = "Venta",
                    Monto = venta.Total,
                    Detalle = $"Facturación de Orden #{venta.Id}. Método: {venta.MetodoPago}",
                    IdVenta = venta.Id 
                };
                _context.MovimientosCaja.Add(ingresoCaja);

                // 4. Control de Crédito atado al mismo eje de tiempo
                if (venta.MetodoPago == "Crédito")
                {
                    var nuevaCuentaCobrar = new CuentaPorCobrar
                    {
                        IdCliente = venta.IdCliente!.Value,
                        IdVenta = venta.Id,
                        MontoTotal = venta.Total,
                        SaldoPendiente = venta.Total,
                        FechaEmision = venta.FechaVenta,
                        FechaVencimiento = venta.FechaVencimientoCreditoManual ?? venta.FechaVenta.AddDays(15),
                        Estado = "Pendiente"
                    };
                    _context.CuentasPorCobrar.Add(nuevaCuentaCobrar);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(venta);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> Put(int id, [FromBody] Venta ventaActualizada)
        {
            if (id != ventaActualizada.Id) return BadRequest("IDs no coinciden.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var ventaOriginal = await _context.Ventas
                    .Include(v => v.Detalles)
                    .FirstOrDefaultAsync(v => v.Id == id);
                
                if (ventaOriginal == null) return NotFound("Venta no encontrada.");

                // ==========================================
                // 1. REVERSIÓN TOTAL DE LA VENTA ANTERIOR
                // ==========================================
                
                // Revertir stock físico y liberar perfiles digitales/suscripciones
                foreach (var detalleOrig in ventaOriginal.Detalles)
                {
                    var prod = await _context.Productos.FindAsync(detalleOrig.IdProducto);
                    if (prod != null)
                    {
                        if (!prod.EsDigital && !prod.RequiereServicio)
                        {
                            prod.StockActual += detalleOrig.Cantidad;
                        }
                    }
                }

                // Eliminar suscripciones asociadas a esta venta
                var suscripcionesViejas = _context.Suscripciones.Where(s => s.IdCliente == ventaOriginal.IdCliente && s.FechaInicio == ventaOriginal.FechaVenta);
                _context.Suscripciones.RemoveRange(suscripcionesViejas);

                // Purgar detalles viejos
                _context.DetallesVentas.RemoveRange(ventaOriginal.Detalles);
                await _context.SaveChangesAsync(); 

                // ==========================================
                // 2. APLICAR NUEVOS VALORES (EDICIÓN ABSOLUTA)
                // ==========================================
                
                ventaOriginal.IdCliente = ventaActualizada.IdCliente == 0 ? null : ventaActualizada.IdCliente;
                ventaOriginal.MetodoPago = ventaActualizada.MetodoPago;
                ventaOriginal.IdUsuario = ventaActualizada.IdUsuario; // Permite cambiar el vendedor si es necesario

                decimal nuevoTotalCalculado = 0;

                foreach (var nuevoDetalle in ventaActualizada.Detalles)
                {
                    var prod = await _context.Productos.FindAsync(nuevoDetalle.IdProducto);
                    if (prod == null) return BadRequest($"El producto con ID {nuevoDetalle.IdProducto} no existe.");

                    // Validar y descontar stock nuevo
                    if (!prod.EsDigital && !prod.RequiereServicio)
                    {
                        if (prod.StockActual < nuevoDetalle.Cantidad)
                            return BadRequest($"Stock insuficiente para: {prod.Nombre}. Disponible: {prod.StockActual}");
                        
                        prod.StockActual -= nuevoDetalle.Cantidad;
                    }

                    // Si es suscripción, recrear la lógica de negocio
                    if (prod.EsSuscripcion)
                    {
                        if (!ventaOriginal.IdCliente.HasValue)
                            return BadRequest($"El producto '{prod.Nombre}' requiere un cliente asociado.");

                        var perfilDisponible = await _context.PerfilesCuentas
                            .FirstOrDefaultAsync(p => p.IdProducto == prod.Id && (!p.Ocupado || p.IdClienteAsignado == ventaOriginal.IdCliente));

                        if (perfilDisponible != null)
                        {
                            perfilDisponible.Ocupado = true;
                            perfilDisponible.IdClienteAsignado = ventaOriginal.IdCliente.Value;
                            _context.PerfilesCuentas.Update(perfilDisponible);
                            nuevoDetalle.MetadataDigital = $"PERFIL: {perfilDisponible.NombrePerfil} | PIN: {perfilDisponible.PIN}";
                        }

                        var nuevaSuscripcion = new Suscripcion
                        {
                            IdCliente = ventaOriginal.IdCliente.Value,
                            NombreServicio = prod.Nombre,
                            TipoSuscripcion = prod.EsDigital ? "Digital" : "Físico",
                            IdProducto = prod.Id,
                            CostoRenovacion = nuevoDetalle.PrecioUnitario,
                            FechaInicio = ventaOriginal.FechaVenta,
                            FechaVencimiento = ventaOriginal.FechaVenta.AddDays(prod.DiasDuracion > 0 ? prod.DiasDuracion : 30),
                            Estado = "Activa",
                            DetallesCredenciales = nuevoDetalle.MetadataDigital
                        };
                        _context.Suscripciones.Add(nuevaSuscripcion);
                    }

                    nuevoDetalle.IdVenta = id;
                    nuevoDetalle.Id = 0;
                    // El subtotal se calcula explícitamente con lo que venga del cliente aplicando descuento
                    nuevoDetalle.SubTotal = (nuevoDetalle.Cantidad * nuevoDetalle.PrecioUnitario) - nuevoDetalle.Descuento;
                    nuevoTotalCalculado += nuevoDetalle.SubTotal;

                    _context.DetallesVentas.Add(nuevoDetalle);
                }

                ventaOriginal.Total = nuevoTotalCalculado;

                // ==========================================
                // 3. SINCRONIZACIÓN CONTABLE (CAJA Y CRÉDITOS)
                // ==========================================
                
                var movimientoCaja = await _context.MovimientosCaja.FirstOrDefaultAsync(m => m.IdVenta == id);
                if (movimientoCaja != null)
                {
                    movimientoCaja.Monto = nuevoTotalCalculado;
                    movimientoCaja.Detalle = $"Facturación (Editada) de Orden #{id}. Método: {ventaOriginal.MetodoPago}";
                    _context.MovimientosCaja.Update(movimientoCaja);
                }

                var cpc = await _context.CuentasPorCobrar.FirstOrDefaultAsync(c => c.IdVenta == id);
                if (ventaOriginal.MetodoPago == "Crédito" && ventaOriginal.IdCliente.HasValue)
                {
                    if (cpc != null)
                    {
                        cpc.IdCliente = ventaOriginal.IdCliente.Value;
                        cpc.MontoTotal = nuevoTotalCalculado;
                        cpc.SaldoPendiente = nuevoTotalCalculado;
                        _context.CuentasPorCobrar.Update(cpc);
                    }
                    else
                    {
                        _context.CuentasPorCobrar.Add(new CuentaPorCobrar
                        {
                            IdCliente = ventaOriginal.IdCliente.Value,
                            IdVenta = id,
                            MontoTotal = nuevoTotalCalculado,
                            SaldoPendiente = nuevoTotalCalculado,
                            FechaEmision = ventaOriginal.FechaVenta,
                            FechaVencimiento = ventaOriginal.FechaVenta.AddDays(15),
                            Estado = "Pendiente"
                        });
                    }
                }
                else if (cpc != null)
                {
                    _context.CuentasPorCobrar.Remove(cpc);
                }

                _context.Ventas.Update(ventaOriginal);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                
                return NoContent();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error en auditoría: {ex.Message}");
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> Delete(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var venta = await _context.Ventas
                    .Include(v => v.Detalles)
                    .FirstOrDefaultAsync(v => v.Id == id);

                if (venta == null) return NotFound("La venta no existe.");

                // 1. Devolver Stock
                foreach (var detalle in venta.Detalles)
                {
                    var prod = await _context.Productos.FindAsync(detalle.IdProducto);
                    if (prod != null && !prod.EsDigital && !prod.RequiereServicio)
                    {
                        prod.StockActual += detalle.Cantidad;
                    }
                }

                // 2. Romper o liberar suscripciones / perfiles digitales
                var suscripciones = _context.Suscripciones.Where(s => s.IdCliente == venta.IdCliente && s.FechaInicio == venta.FechaVenta);
                _context.Suscripciones.RemoveRange(suscripciones);

                // 3. Eliminar rastro financiero (Caja y Cuentas por Cobrar)
                var movimiento = await _context.MovimientosCaja.FirstOrDefaultAsync(m => m.IdVenta == id);
                if (movimiento != null) _context.MovimientosCaja.Remove(movimiento);

                var cpc = await _context.CuentasPorCobrar.FirstOrDefaultAsync(c => c.IdVenta == id);
                if (cpc != null) _context.CuentasPorCobrar.Remove(cpc);

                // 4. Eliminar la venta (la cascada borrará DetallesVentas dependientes si está configurada, si no, borrar manual)
                _context.DetallesVentas.RemoveRange(venta.Detalles);
                _context.Ventas.Remove(venta);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error al eliminar la venta: {ex.Message}");
            }
        }
    }
}