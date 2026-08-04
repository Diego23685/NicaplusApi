using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.DTOs;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Protegido mediante JWT
    public class NotificacionesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<NotificacionesController> _logger;

        public NotificacionesController(
            ApplicationDbContext context,
            ILogger<NotificacionesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private static TimeZoneInfo GetNicaraguaTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/Managua");
            }
        }

        private DateTime GetNicaraguaTime()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, GetNicaraguaTimeZone());
        }

        // GET: api/Notificaciones/pendientes
        [HttpGet("pendientes")]
        public async Task<IActionResult> GetPendientes()
        {
            // Verificamos que el usuario no sea un Cliente antes de entregar alertas operativas
            var tipoUsuario = User.FindFirst("TipoUsuario")?.Value;
            if (string.Equals(tipoUsuario, "Cliente", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(403, new { mensaje = "Acceso denegado. Este recurso es de uso operativo interno." });
            }

            try
            {
                var ahoraNicaragua = GetNicaraguaTime();
                var alertaFecha = ahoraNicaragua.AddDays(7); // Renovaciones a vencer dentro de los próximos 7 días

                // 1. Suscripciones próximas a vencer (Corregido: cálculo de días con EF.Functions)
                var renovaciones = await _context.Suscripciones
                    .AsNoTracking()
                    .Where(s => s.Estado == "Activa" && s.FechaVencimiento <= alertaFecha)
                    .OrderBy(s => s.FechaVencimiento)
                    .Select(s => new NotificacionRenovacionDto
                    {
                        IdSuscripcion = s.Id,
                        NombreServicio = s.NombreServicio,
                        ClienteNombre = s.Cliente != null ? s.Cliente.Nombre : "Genérico",
                        FechaVencimiento = s.FechaVencimiento,
                        DiasRestantes = EF.Functions.DateDiffDay(ahoraNicaragua, s.FechaVencimiento),
                        Tipo = "Renovación"
                    })
                    .ToListAsync();

                // 2. Tickets de Soporte Pendientes
                var tickets = await _context.TicketsSoporte
                    .AsNoTracking()
                    .Where(t => t.Estado == "Pendiente")
                    .OrderByDescending(t => t.FechaCreacion)
                    .Select(t => new NotificacionTicketDto
                    {
                        IdTicket = t.Id,
                        TipoTicket = t.TipoTicket,
                        ClienteNombre = t.Cliente != null ? t.Cliente.Nombre : "Genérico",
                        FechaCreacion = t.FechaCreacion,
                        Tipo = "Ticket"
                    })
                    .ToListAsync();

                // 3. Productos con Stock Crítico
                var stockBajo = await _context.Productos
                    .AsNoTracking()
                    .Where(p => p.StockActual <= p.StockMinimo)
                    .OrderBy(p => p.StockActual)
                    .Select(p => new NotificacionStockBajoDto
                    {
                        IdProducto = p.Id,
                        NombreProducto = p.Nombre,
                        StockActual = p.StockActual,
                        StockMinimo = p.StockMinimo,
                        Tipo = "Inventario"
                    })
                    .ToListAsync();

                // 4. Garantías Pendientes de Procesar
                var garantias = await _context.GarantiasTickets
                    .AsNoTracking()
                    .Where(g => g.Estado == "Pendiente")
                    .OrderByDescending(g => g.FechaRepo)
                    .Select(g => new NotificacionGarantiaDto
                    {
                        IdGarantia = g.Id,
                        ClienteNombre = g.Cliente != null ? g.Cliente.Nombre : "Genérico",
                        Motivo = g.Motivo,
                        FechaRepo = g.FechaRepo,
                        Tipo = "Garantía"
                    })
                    .ToListAsync();

                var response = new SummaryNotificacionesResponseDto
                {
                    TotalAlertas = renovaciones.Count + tickets.Count + stockBajo.Count + garantias.Count,
                    Renovaciones = renovaciones,
                    Tickets = tickets,
                    StockBajo = stockBajo,
                    Garantias = garantias
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar el resumen de notificaciones operativas.");
                return StatusCode(500, new { mensaje = "Error interno al recuperar las alertas del sistema." });
            }
        }
    }
}