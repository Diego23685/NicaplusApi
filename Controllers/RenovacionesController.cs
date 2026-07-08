using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.Models;
using NicaplusApi.Models.Requests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RenovacionesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        
        // Zona horaria estándar para Nicaragua
        private static readonly TimeZoneInfo NicaraguaZone = TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");

        public RenovacionesController(ApplicationDbContext context)
        {
            _context = context;
        }

        private DateTime GetNicaraguaTime()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, NicaraguaZone);
        }

        // GET: api/renovaciones
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var renovaciones = await _context.Renovaciones
                .Include(r => r.Cliente)
                .Include(r => r.Suscripcion)
                .OrderByDescending(r => r.FechaPago)
                .ToListAsync();

            return Ok(renovaciones);
        }

        // POST api/renovaciones/cancelar
        [HttpPost("cancelar")]
        public async Task<IActionResult> Cancelar([FromBody] CancelarSuscripcionRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var suscripcion = await _context.Suscripciones
                    .Include(s => s.PerfilCuenta)
                    .FirstOrDefaultAsync(s => s.Id == request.IdSuscripcion);

                if (suscripcion == null) return NotFound("La asignación/suscripción no existe.");
                if (suscripcion.Estado == "Cancelada") return BadRequest("La suscripción ya está cancelada.");

                var ahoraNicaragua = GetNicaraguaTime();

                var cancelacion = new Cancelacion
                {
                    IdSuscripcion = suscripcion.Id,
                    IdCliente = suscripcion.IdCliente,
                    Motivo = request.Motivo,
                    FechaCancelacion = ahoraNicaragua // Corregido huso horario
                };

                suscripcion.Estado = "Cancelada";

                if (suscripcion.PerfilCuenta != null)
                {
                    suscripcion.PerfilCuenta.Ocupado = false;
                    suscripcion.PerfilCuenta.IdClienteAsignado = null;
                    suscripcion.PerfilCuenta.FechaLiberacion = ahoraNicaragua; // Corregido huso horario
                }

                _context.Cancelaciones.Add(cancelacion);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { mensaje = "Servicio cancelado correctamente y perfil liberado." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error cancelando servicio: {ex.Message}");
            }
        }

        // GET: api/renovaciones/suscripcion/{idSuscripcion}
        [HttpGet("suscripcion/{idSuscripcion}")]
        public async Task<IActionResult> GetPorSuscripcion(int idSuscripcion)
        {
            var renovaciones = await _context.Renovaciones
                .Where(r => r.IdSuscripcion == idSuscripcion)
                .Include(r => r.Suscripcion)
                .Include(r => r.Cliente)
                .OrderByDescending(r => r.FechaPago)
                .Select(r => new
                {
                    r.Id,
                    r.IdSuscripcion,
                    Cliente = r.Cliente != null ? r.Cliente.Nombre : "Cliente desconocido",
                    Servicio = r.Suscripcion != null ? r.Suscripcion.NombreServicio : "Servicio desconocido",
                    r.Monto,
                    r.FechaPago,
                    r.FechaAnterior,
                    r.NuevaFechaVencimiento,
                    r.MetodoPago,
                    r.Observacion
                })
                .ToListAsync();

            return Ok(renovaciones);
        }

        // POST: api/renovaciones
        [HttpPost]
        public async Task<IActionResult> Renovar([FromBody] Renovacion renovacion)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var suscripcion = await _context.Suscripciones
                    .Include(s => s.Cliente)
                    .Include(s => s.Producto)
                    .FirstOrDefaultAsync(s => s.Id == renovacion.IdSuscripcion);

                if (suscripcion == null) return NotFound("La suscripción no existe.");
                if (suscripcion.Estado == "Cancelada") return BadRequest("Esta suscripción se encuentra cancelada.");

                var ahoraNicaragua = GetNicaraguaTime();

                // 1. Seteo controlado de la fecha de pago (Prioriza lo enviado manualmente por tu React)
                if (renovacion.FechaPago == default)
                {
                    renovacion.FechaPago = ahoraNicaragua;
                }

                renovacion.IdCliente = suscripcion.IdCliente;
                renovacion.FechaAnterior = suscripcion.FechaVencimiento;

                var dias = suscripcion.Producto?.DiasDuracion ?? 30;

                // CORRECCIÓN COMERCIAL: Si la cuenta ya está vencida, los días corren a partir de que paga hoy.
                // Si la cuenta aún está vigente, se acumulan los días de forma normal sobre su vencimiento actual.
                if (suscripcion.FechaVencimiento < ahoraNicaragua)
                {
                    renovacion.NuevaFechaVencimiento = renovacion.FechaPago.AddDays(dias);
                }
                else
                {
                    renovacion.NuevaFechaVencimiento = suscripcion.FechaVencimiento.AddDays(dias);
                }

                _context.Renovaciones.Add(renovacion);

                // 2. ACTUALIZAR SUSCRIPCION
                suscripcion.FechaVencimiento = renovacion.NuevaFechaVencimiento;
                suscripcion.Estado = "Activa";
                _context.Suscripciones.Update(suscripcion);
                await _context.SaveChangesAsync();

                // 3. CREAR VENTA FINANCIERA (Enlazada al mismo eje de tiempo)
                var idUsuarioClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                int idUsuario = 1;

                if (int.TryParse(idUsuarioClaim, out int usuarioParseado))
                {
                    idUsuario = usuarioParseado;
                }

                var venta = new Venta
                {
                    FechaVenta = renovacion.FechaPago, // Mismo día estricto de la operación
                    IdUsuario = idUsuario,
                    IdCliente = suscripcion.IdCliente,
                    IdSuscripcion = suscripcion.Id,
                    Total = renovacion.Monto,
                    MetodoPago = renovacion.MetodoPago
                };

                _context.Ventas.Add(venta);
                await _context.SaveChangesAsync();

                // 4. DETALLE DE VENTA (Corregido mapeo relacional seguro para evitar desajustes de nombres en DB Context)
                var detalleVenta = new DetalleVenta
                {
                    IdVenta = venta.Id,
                    IdProducto = suscripcion.IdProducto ?? 1, // Fallback controlado a ID base de servicios
                    Cantidad = 1,
                    PrecioUnitario = renovacion.Monto,
                    SubTotal = renovacion.Monto,
                    MetadataDigital = $"Renovación perfil: {suscripcion.NombreServicio}"
                };

                _context.Entry(detalleVenta).State = EntityState.Added; // Evita problemas si el DbSet se llama diferente en plural
                await _context.SaveChangesAsync();

                // 5. MOVIMIENTO CAJA (Sincronizado al reporte diario)
                var movimientoCaja = new MovimientoCaja
                {
                    Fecha = renovacion.FechaPago,
                    Tipo = "Ingreso",
                    Concepto = "Renovacion",
                    Monto = renovacion.Monto,
                    Detalle = $"Renovación de cuenta streaming: {suscripcion.NombreServicio} | Método: {renovacion.MetodoPago}",
                    IdVenta = venta.Id,
                    IdRenovacion = renovacion.Id
                };

                _context.MovimientosCaja.Add(movimientoCaja);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return Ok(new
                {
                    mensaje = "Renovación procesada correctamente.",
                    ventaId = venta.Id,
                    nuevaFechaVencimiento = renovacion.NuevaFechaVencimiento
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error procesando renovación: {ex.Message}");
            }
        }
    }
}