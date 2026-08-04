using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.DTOs;
using NicaplusApi.Models;
using System.Security.Claims;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RenovacionesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RenovacionesController> _logger;

        public RenovacionesController(
            ApplicationDbContext context,
            ILogger<RenovacionesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private DateTime GetNicaraguaTime()
        {
            TimeZoneInfo zone;
            try
            {
                zone = TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                zone = TimeZoneInfo.FindSystemTimeZoneById("America/Managua");
            }

            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
        }

        private int ObtenerUsuarioIdActual()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                     ?? User.FindFirst("id")?.Value;

            return int.TryParse(claim, out int id) ? id : 1;
        }

        // 1. GET: api/Renovaciones
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                var renovaciones = await _context.Renovaciones
                    .AsNoTracking()
                    .Include(r => r.Cliente)
                    .Include(r => r.Suscripcion)
                    .OrderByDescending(r => r.FechaPago)
                    .Select(r => new RenovacionResponseDto
                    {
                        Id = r.Id,
                        IdSuscripcion = r.IdSuscripcion,
                        IdCliente = r.IdCliente,
                        Cliente = r.Cliente != null ? r.Cliente.Nombre : "Cliente Desconocido",
                        Servicio = r.Suscripcion != null ? r.Suscripcion.NombreServicio : "Servicio Desconocido",
                        Monto = r.Monto,
                        FechaPago = r.FechaPago,
                        FechaAnterior = r.FechaAnterior,
                        NuevaFechaVencimiento = r.NuevaFechaVencimiento,
                        MetodoPago = r.MetodoPago,
                        Observacion = r.Observacion
                    })
                    .ToListAsync();

                return Ok(renovaciones);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar la base de historial de renovaciones.");
                return StatusCode(500, new { mensaje = "Error interno al recuperar las renovaciones." });
            }
        }

        // 2. GET: api/Renovaciones/suscripcion/5
        [HttpGet("suscripcion/{idSuscripcion}")]
        public async Task<IActionResult> GetPorSuscripcion(int idSuscripcion)
        {
            try
            {
                var renovaciones = await _context.Renovaciones
                    .AsNoTracking()
                    .Where(r => r.IdSuscripcion == idSuscripcion)
                    .Include(r => r.Cliente)
                    .Include(r => r.Suscripcion)
                    .OrderByDescending(r => r.FechaPago)
                    .Select(r => new RenovacionResponseDto
                    {
                        Id = r.Id,
                        IdSuscripcion = r.IdSuscripcion,
                        IdCliente = r.IdCliente,
                        Cliente = r.Cliente != null ? r.Cliente.Nombre : "Cliente Desconocido",
                        Servicio = r.Suscripcion != null ? r.Suscripcion.NombreServicio : "Servicio Desconocido",
                        Monto = r.Monto,
                        FechaPago = r.FechaPago,
                        FechaAnterior = r.FechaAnterior,
                        NuevaFechaVencimiento = r.NuevaFechaVencimiento,
                        MetodoPago = r.MetodoPago,
                        Observacion = r.Observacion
                    })
                    .ToListAsync();

                return Ok(renovaciones);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar renovaciones para la suscripción {IdSuscripcion}", idSuscripcion);
                return StatusCode(500, new { mensaje = "Error interno al obtener el historial de la suscripción." });
            }
        }

        // 3. POST: api/Renovaciones
        [HttpPost]
        public async Task<IActionResult> Renovar([FromBody] RegistrarRenovacionDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { mensaje = "Datos de renovación no válidos.", detalles = ModelState });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var suscripcion = await _context.Suscripciones
                    .Include(s => s.Cliente)
                    .Include(s => s.Producto)
                    .FirstOrDefaultAsync(s => s.Id == dto.IdSuscripcion);

                if (suscripcion == null)
                {
                    return NotFound(new { mensaje = "La suscripción especificada no existe." });
                }

                if (suscripcion.Estado == "Cancelada")
                {
                    return BadRequest(new { mensaje = "No se puede renovar una suscripción en estado 'Cancelada'." });
                }

                var ahoraNicaragua = GetNicaraguaTime();
                var fechaPagoReal = dto.FechaPago.HasValue && dto.FechaPago.Value != default 
                    ? dto.FechaPago.Value 
                    : ahoraNicaragua;

                var fechaAnterior = suscripcion.FechaVencimiento;
                int diasDuracion = suscripcion.Producto?.DiasDuracion ?? 30;

                // Lógica de extensión comercial: acumulativa o reinicio si está vencida
                DateTime nuevaFechaVencimiento = (suscripcion.FechaVencimiento < ahoraNicaragua)
                    ? fechaPagoReal.AddDays(diasDuracion)
                    : suscripcion.FechaVencimiento.AddDays(diasDuracion);

                // 1. Entidad Renovación
                var renovacion = new Renovacion
                {
                    IdSuscripcion = suscripcion.Id,
                    IdCliente = suscripcion.IdCliente,
                    Monto = dto.Monto,
                    FechaPago = fechaPagoReal,
                    FechaAnterior = fechaAnterior,
                    NuevaFechaVencimiento = nuevaFechaVencimiento,
                    MetodoPago = dto.MetodoPago.Trim(),
                    Observacion = dto.Observacion?.Trim() ?? string.Empty
                };

                _context.Renovaciones.Add(renovacion);

                // 2. Actualizar estado y vencimiento de la suscripción
                suscripcion.FechaVencimiento = nuevaFechaVencimiento;
                suscripcion.Estado = "Activa";

                // 3. Registrar Venta General
                int idUsuario = ObtenerUsuarioIdActual();
                var venta = new Venta
                {
                    FechaVenta = fechaPagoReal,
                    IdUsuario = idUsuario,
                    IdCliente = suscripcion.IdCliente,
                    IdSuscripcion = suscripcion.Id,
                    Total = dto.Monto,
                    MetodoPago = dto.MetodoPago.Trim(),
                    Detalles = new List<DetalleVenta>
                    {
                        new DetalleVenta
                        {
                            IdProducto = suscripcion.IdProducto ?? 1,
                            Cantidad = 1,
                            PrecioUnitario = dto.Monto,
                            SubTotal = dto.Monto,
                            MetadataDigital = $"Renovación perfil: {suscripcion.NombreServicio}"
                        }
                    }
                };

                _context.Ventas.Add(venta);

                // 4. MOVIMIENTO CAJA (Asociando navegaciones en memoria para resolución de Foreign Keys)
                var movimientoCaja = new MovimientoCaja
                {
                    Fecha = fechaPagoReal,
                    Tipo = "Ingreso",
                    Concepto = "Renovacion",
                    Monto = dto.Monto,
                    Detalle = $"Renovación de cuenta streaming: {suscripcion.NombreServicio} | Método: {dto.MetodoPago.Trim()}",
                    Venta = venta,           // ✅ CORREGIDO: Usar navegación en lugar de IdVenta = venta.Id
                    Renovacion = renovacion  // ✅ Correcto
                };

                _context.MovimientosCaja.Add(movimientoCaja);

                // Commit de la unidad de trabajo en un único viaje a la BD
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    mensaje = "Renovación procesada y contabilizada correctamente.",
                    renovacionId = renovacion.Id,
                    ventaId = venta.Id,
                    nuevaFechaVencimiento = renovacion.NuevaFechaVencimiento
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al procesar la renovación de la suscripción ID {IdSuscripcion}", dto.IdSuscripcion);
                return StatusCode(500, new { mensaje = "Error interno al procesar la transacción de renovación." });
            }
        }

        // POST: api/Renovaciones/cancelar
        [HttpPost("cancelar")]
        public async Task<IActionResult> Cancelar([FromBody] CancelarSuscripcionDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { mensaje = "Datos de cancelación incompletos.", detalles = ModelState });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var suscripcion = await _context.Suscripciones
                    .Include(s => s.PerfilCuenta)
                    .FirstOrDefaultAsync(s => s.Id == dto.IdSuscripcion);

                if (suscripcion == null)
                {
                    return NotFound(new { mensaje = "La asignación/suscripción especificada no existe." });
                }

                if (suscripcion.Estado == "Cancelada")
                {
                    return BadRequest(new { mensaje = "La suscripción ya se encuentra cancelada previamente." });
                }

                var ahoraNicaragua = GetNicaraguaTime();

                var cancelacion = new Cancelacion
                {
                    IdSuscripcion = suscripcion.Id,
                    IdCliente = suscripcion.IdCliente,
                    Motivo = dto.Motivo.Trim(),
                    FechaCancelacion = ahoraNicaragua
                };

                _context.Cancelaciones.Add(cancelacion);

                // EVALUAR SI AÚN TIENE DÍAS VIGENTES
                if (suscripcion.FechaVencimiento > ahoraNicaragua)
                {
                    // Solo marcar que no renovará cuando venza
                    suscripcion.Estado = "NoRenovar"; 
                    
                    // NO tocamos suscripcion.PerfilCuenta para que el cliente conserve su acceso
                }
                else
                {
                    // Si ya está vencida, procedemos a la baja e independización inmediata del perfil
                    suscripcion.Estado = "Cancelada";

                    if (suscripcion.PerfilCuenta != null)
                    {
                        suscripcion.PerfilCuenta.Ocupado = false;
                        suscripcion.PerfilCuenta.IdClienteAsignado = null;
                        suscripcion.PerfilCuenta.FechaLiberacion = ahoraNicaragua;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    mensaje = suscripcion.Estado == "NoRenovar" 
                        ? "Servicio programado para no renovar. El cliente conservará su acceso hasta la fecha de vencimiento."
                        : "Servicio cancelado correctamente y perfil liberado para reasignación.",
                    suscripcionId = suscripcion.Id
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al cancelar la suscripción ID {IdSuscripcion}", dto.IdSuscripcion);
                return StatusCode(500, new { mensaje = "Error interno al procesar la cancelación del servicio." });
            }
        }
    }
}