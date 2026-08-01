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

                if (venta.FechaVenta == default(DateTime) || venta.FechaVenta.Year == 1)
                {
                    venta.FechaVenta = ahoraNicaragua;
                }

                _context.Ventas.Add(venta);
                await _context.SaveChangesAsync();

                foreach (var detalle in venta.Detalles)
                {
                    var prod = await _context.Productos.FindAsync(detalle.IdProducto);
                    if (prod != null)
                    {
                        // Control de inventario explícito
                        if (prod.ControlaStock)
                        {
                            if (prod.StockActual < detalle.Cantidad)
                                return BadRequest($"Stock insuficiente para: {prod.Nombre}");
                            
                            prod.StockActual -= detalle.Cantidad;
                            if (prod.StockActual <= 0) prod.Estado = "Agotado";
                        }

                        // Lógica de Suscripciones Optimizada con AccountGroupKey
                        if (prod.EsSuscripcion)
                        {
                            if (!venta.IdCliente.HasValue || venta.IdCliente.Value == 0)
                                return BadRequest($"Operación Denegada: El producto '{prod.Nombre}' requiere obligatoriamente un cliente asociado.");

                            // 1. Buscamos qué cuenta madre (GroupKey) tiene suficientes pantallas libres para cubrir la CANTIDAD solicitada en ESTE detalle
                            var grupoValido = await _context.PerfilesCuentas
                                .Where(p => p.IdProducto == prod.Id && !p.Ocupado && p.EstadoPerfil == "Disponible" && !string.IsNullOrEmpty(p.AccountGroupKey))
                                .GroupBy(p => p.AccountGroupKey)
                                .Where(g => g.Count() >= detalle.Cantidad)
                                .Select(g => g.Key)
                                .FirstOrDefaultAsync();

                            // Si no hay ninguna cuenta única que cubra todo el lote, buscamos perfiles sueltos de cualquier grupo
                            bool usarAgrupacionEstricta = !string.IsNullOrEmpty(grupoValido);

                            List<int> perfilesAGanarIds = new List<int>();

                            if (usarAgrupacionEstricta)
                            {
                                // Traemos los IDs del mismo lote/cuenta física
                                perfilesAGanarIds = await _context.PerfilesCuentas
                                    .Where(p => p.IdProducto == prod.Id && !p.Ocupado && p.EstadoPerfil == "Disponible" && p.AccountGroupKey == grupoValido)
                                    .Take(detalle.Cantidad)
                                    .Select(p => p.Id)
                                    .ToListAsync();
                            }

                            // Loop de contingencia por si se metieron hilos concurrentes o no se halló lote único
                            int intentos = 0;
                            List<PerfilCuenta> perfilesAsignados = new List<PerfilCuenta>();

                            while (perfilesAsignados.Count < detalle.Cantidad && intentos < 5)
                            {
                                intentos++;
                                if (!usarAgrupacionEstricta || perfilesAGanarIds.Count < detalle.Cantidad)
                                {
                                    // Contingencia: Tomar lo que esté libre del pool general
                                    perfilesAGanarIds = await _context.PerfilesCuentas
                                        .Where(p => p.IdProducto == prod.Id && !p.Ocupado && p.EstadoPerfil == "Disponible")
                                        .Take(detalle.Cantidad - perfilesAsignados.Count)
                                        .Select(p => p.Id)
                                        .ToListAsync();
                                }

                                if (!perfilesAGanarIds.Any()) break;

                                foreach (var perfilId in perfilesAGanarIds)
                                {
                                    int filasAfectadas = await _context.PerfilesCuentas
                                        .Where(p => p.Id == perfilId && !p.Ocupado)
                                        .ExecuteUpdateAsync(setters => setters
                                            .SetProperty(p => p.Ocupado, true)
                                            .SetProperty(p => p.IdClienteAsignado, venta.IdCliente.Value)
                                            .SetProperty(p => p.EstadoPerfil, "Asignado")
                                            .SetProperty(p => p.FechaAsignacion, ahoraNicaragua));

                                    if (filasAfectadas > 0)
                                    {
                                        var pAsignado = await _context.PerfilesCuentas.FindAsync(perfilId);
                                        if (pAsignado != null) perfilesAsignados.Add(pAsignado);
                                    }
                                }
                            }

                            if (perfilesAsignados.Count < detalle.Cantidad)
                            {
                                return BadRequest($"Acción Denegada: No quedan suficientes pantallas juntas/disponibles para '{prod.Nombre}'.");
                            }

                            // Construimos los metadatos concatenando las cuentas despachadas
                            var metadataList = perfilesAsignados.Select(p => $"PERFIL: {p.NombrePerfil} | PIN: {p.PIN} | Acceso: {p.CorreoCuenta} / {p.PasswordCuenta}");
                            detalle.MetadataDigital = string.Join(" || ", metadataList);

                            // Generamos las suscripciones individuales vinculadas
                            foreach (var perfil in perfilesAsignados)
                            {
                                var nuevaSuscripcion = new Suscripcion
                                {
                                    IdCliente = venta.IdCliente.Value,
                                    NombreServicio = $"{prod.Nombre} ({perfil.NombrePerfil})",
                                    TipoSuscripcion = "Digital",
                                    IdProducto = prod.Id,
                                    IdPerfilCuenta = perfil.Id,
                                    CostoRenovacion = detalle.PrecioUnitario / detalle.Cantidad, // Proporcional si es combo
                                    FechaInicio = venta.FechaVenta,
                                    FechaVencimiento = venta.FechaVenta.AddDays(prod.DiasDuracion > 0 ? prod.DiasDuracion : 30),
                                    Estado = "Activa",
                                    DetallesCredenciales = $"PERFIL: {perfil.NombrePerfil} | PIN: {perfil.PIN} | Acceso: {perfil.CorreoCuenta} / {perfil.PasswordCuenta}"
                                };
                                _context.Suscripciones.Add(nuevaSuscripcion);
                            }
                        }
                    }
                }

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
                foreach (var detalleOrig in ventaOriginal.Detalles)
                {
                    var prod = await _context.Productos.FindAsync(detalleOrig.IdProducto);
                    if (prod != null && prod.ControlaStock)
                    {
                        prod.StockActual += detalleOrig.Cantidad;
                        if (prod.Estado == "Agotado" && prod.StockActual > 0) prod.Estado = "Activo";
                    }
                }

                var suscripcionesViejas = await _context.Suscripciones
                    .Where(s => s.IdProducto != null && _context.DetallesVentas.Where(dv => dv.IdVenta == id).Select(dv => dv.IdProducto).Contains(s.IdProducto.Value)) 
                    .ToListAsync(); // Selección más segura basada en productos de la venta

                foreach (var suscripcionVieja in suscripcionesViejas)
                {
                    if (suscripcionVieja.IdPerfilCuenta.HasValue)
                    {
                        var perfil = await _context.PerfilesCuentas.FindAsync(suscripcionVieja.IdPerfilCuenta.Value);
                        if (perfil != null)
                        {
                            perfil.Ocupado = false;
                            perfil.IdClienteAsignado = null;
                            perfil.EstadoPerfil = "Disponible";
                            _context.PerfilesCuentas.Update(perfil);
                        }
                    }
                }

                _context.Suscripciones.RemoveRange(suscripcionesViejas);
                _context.DetallesVentas.RemoveRange(ventaOriginal.Detalles);
                await _context.SaveChangesAsync(); 

                // ==========================================
                // 2. APLICAR NUEVOS VALORES Y RE-ASIGNAR POR LOTES
                // ==========================================
                ventaOriginal.IdCliente = ventaActualizada.IdCliente == 0 ? null : ventaActualizada.IdCliente;
                ventaOriginal.MetodoPago = ventaActualizada.MetodoPago;
                ventaOriginal.IdUsuario = ventaActualizada.IdUsuario;

                decimal nuevoTotalCalculado = 0;

                foreach (var nuevoDetalle in ventaActualizada.Detalles)
                {
                    var prod = await _context.Productos.FindAsync(nuevoDetalle.IdProducto);
                    if (prod == null) return BadRequest($"El producto con ID {nuevoDetalle.IdProducto} no existe.");

                    if (prod.ControlaStock)
                    {
                        if (prod.StockActual < nuevoDetalle.Cantidad)
                            return BadRequest($"Stock insuficiente para: {prod.Nombre}. Disponible: {prod.StockActual}");
                        
                        prod.StockActual -= nuevoDetalle.Cantidad;
                        if (prod.StockActual <= 0) prod.Estado = "Agotado";
                    }

                    if (prod.EsSuscripcion)
                    {
                        if (!ventaOriginal.IdCliente.HasValue)
                            return BadRequest($"El producto '{prod.Nombre}' requiere un cliente asociado.");

                        // Estrategia de agrupación por AccountGroupKey también en la edición
                        var grupoValido = await _context.PerfilesCuentas
                            .Where(p => p.IdProducto == prod.Id && !p.Ocupado && p.EstadoPerfil == "Disponible" && !string.IsNullOrEmpty(p.AccountGroupKey))
                            .GroupBy(p => p.AccountGroupKey)
                            .Where(g => g.Count() >= nuevoDetalle.Cantidad)
                            .Select(g => g.Key)
                            .FirstOrDefaultAsync();

                        List<PerfilCuenta> perfilesDisponibles;

                        if (!string.IsNullOrEmpty(grupoValido))
                        {
                            perfilesDisponibles = await _context.PerfilesCuentas
                                .Where(p => p.IdProducto == prod.Id && !p.Ocupado && p.AccountGroupKey == grupoValido)
                                .Take(nuevoDetalle.Cantidad)
                                .ToListAsync();
                        }
                        else
                        {
                            perfilesDisponibles = await _context.PerfilesCuentas
                                .Where(p => p.IdProducto == prod.Id && !p.Ocupado)
                                .Take(nuevoDetalle.Cantidad)
                                .ToListAsync();
                        }

                        if (perfilesDisponibles.Count < nuevoDetalle.Cantidad)
                            return BadRequest($"No quedan suficientes pantallas disponibles en el pool para '{prod.Nombre}'.");

                        var metadataList = new List<string>();

                        foreach (var perfil in perfilesDisponibles)
                        {
                            perfil.Ocupado = true;
                            perfil.IdClienteAsignado = ventaOriginal.IdCliente.Value;
                            perfil.EstadoPerfil = "Asignado";
                            perfil.FechaAsignacion = ventaOriginal.FechaVenta;
                            _context.PerfilesCuentas.Update(perfil);
                            
                            var credencialIndividual = $"PERFIL: {perfil.NombrePerfil} | PIN: {perfil.PIN} | Acceso: {perfil.CorreoCuenta} / {perfil.PasswordCuenta}";
                            metadataList.Add(credencialIndividual);

                            var nuevaSuscripcion = new Suscripcion
                            {
                                IdCliente = ventaOriginal.IdCliente.Value,
                                NombreServicio = $"{prod.Nombre} ({perfil.NombrePerfil})",
                                TipoSuscripcion = prod.EsDigital ? "Digital" : "Físico",
                                IdProducto = prod.Id,
                                IdPerfilCuenta = perfil.Id,
                                CostoRenovacion = nuevoDetalle.PrecioUnitario / nuevoDetalle.Cantidad,
                                FechaInicio = ventaOriginal.FechaVenta,
                                FechaVencimiento = ventaOriginal.FechaVenta.AddDays(prod.DiasDuracion > 0 ? prod.DiasDuracion : 30),
                                Estado = "Activa",
                                DetallesCredenciales = credencialIndividual
                            };
                            _context.Suscripciones.Add(nuevaSuscripcion);
                        }

                        nuevoDetalle.MetadataDigital = string.Join(" || ", metadataList);
                    }

                    nuevoDetalle.IdVenta = id;
                    nuevoDetalle.Id = 0;
                    nuevoDetalle.SubTotal = (nuevoDetalle.Cantidad * nuevoDetalle.PrecioUnitario) - nuevoDetalle.Descuento;
                    nuevoTotalCalculado += nuevoDetalle.SubTotal;

                    _context.DetallesVentas.Add(nuevoDetalle);
                }

                ventaOriginal.Total = nuevoTotalCalculado;

                // ==========================================
                // 3. SINCRONIZACIÓN CONTABLE
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

                // 1. Devolver Stock Controlado
                foreach (var detalle in venta.Detalles)
                {
                    var prod = await _context.Productos.FindAsync(detalle.IdProducto);
                    if (prod != null && prod.ControlaStock)
                    {
                        prod.StockActual += detalle.Cantidad;
                        if (prod.Estado == "Agotado" && prod.StockActual > 0) prod.Estado = "Activo";
                    }
                }

                // 2. Liberar perfiles vinculados
                var suscripciones = await _context.Suscripciones
                    .Where(s => s.IdCliente == venta.IdCliente && s.FechaInicio == venta.FechaVenta)
                    .ToListAsync();

                foreach (var sus in suscripciones)
                {
                    if (sus.IdPerfilCuenta.HasValue)
                    {
                        var perfil = await _context.PerfilesCuentas.FindAsync(sus.IdPerfilCuenta.Value);
                        if (perfil != null)
                        {
                            perfil.Ocupado = false;
                            perfil.IdClienteAsignado = null;
                            _context.PerfilesCuentas.Update(perfil);
                        }
                    }
                }

                _context.Suscripciones.RemoveRange(suscripciones);

                var movimiento = await _context.MovimientosCaja.FirstOrDefaultAsync(m => m.IdVenta == id);
                if (movimiento != null) _context.MovimientosCaja.Remove(movimiento);

                var cpc = await _context.CuentasPorCobrar.FirstOrDefaultAsync(c => c.IdVenta == id);
                if (cpc != null) _context.CuentasPorCobrar.Remove(cpc);

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