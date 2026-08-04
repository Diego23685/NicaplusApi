using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Administrador")]
    public class AuditoriaController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuditoriaController> _logger;

        public AuditoriaController(
            ApplicationDbContext context, 
            ILogger<AuditoriaController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetLogs()
        {
            try
            {
                // Optimización con AsNoTracking() para lectura pura
                var logs = await _context.LogsAuditoria
                    .AsNoTracking()
                    .OrderByDescending(l => l.FechaRegistro)
                    .Take(100)
                    .ToListAsync();

                return Ok(logs);
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Error de base de datos al recuperar el historial de auditoría.");
                return StatusCode(500, new 
                { 
                    mensaje = "Error al consultar la base de datos de auditoría.", 
                    detalles = "Hubo un problema al comunicarse con el servidor de datos." 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error no esperado al procesar la solicitud de auditoría.");
                return StatusCode(500, new 
                { 
                    mensaje = "Error interno del servidor.", 
                    detalles = "No se pudo recuperar los registros de auditoría en este momento." 
                });
            }
        }
    }
}