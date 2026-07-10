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
                var ventaOriginal = await _context.Ventas.Include(v => v.Detalles)
                    .FirstOrDefaultAsync(v => v.Id == id);
                
                if (ventaOriginal == null) return NotFound("Venta no encontrada.");

                // 1. REVERSIÓN DE STOCK
                foreach (var detalleOrig in ventaOriginal.Detalles)
                {
                    var prod = await _context.Productos.FindAsync(detalleOrig.IdProducto);
                    if (prod != null && !prod.EsDigital && !prod.RequiereServicio)
                    {
                        prod.StockActual += detalleOrig.Cantidad;
                    }
                }

                // 2. APLICAR NUEVOS VALORES FINANCIEROS
                ventaOriginal.MetodoPago = ventaActualizada.MetodoPago;
                ventaOriginal.IdCliente = ventaActualizada.IdCliente;

                _context.RemoveRange(ventaOriginal.Detalles);

                // 3. DESCONTAR EL NUEVO STOCK E INYECTAR NUEVOS DETALLES
                ventaOriginal.Detalles = ventaActualizada.Detalles;
                foreach (var nuevoDetalle in ventaOriginal.Detalles)
                {
                    var prod = await _context.Productos.FindAsync(nuevoDetalle.IdProducto);
                    if (prod != null && !prod.EsDigital && !prod.RequiereServicio)
                    {
                        if (prod.StockActual < nuevoDetalle.Cantidad)
                            return BadRequest($"Stock insuficiente tras reajuste para: {prod.Nombre}");
                        
                        prod.StockActual -= nuevoDetalle.Cantidad;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return NoContent();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, "Fallo al revertir o recalcular stock y flujos de caja.");
            }
        }
    }
}