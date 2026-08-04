using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.DTOs;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Protegemos las configuraciones globales del sistema
    public class ConfiguracionMensajesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ConfiguracionMensajesController> _logger;

        public ConfiguracionMensajesController(
            ApplicationDbContext context,
            ILogger<ConfiguracionMensajesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/ConfiguracionMensajes
        [HttpGet]
        public async Task<IActionResult> GetConfiguraciones()
        {
            try
            {
                var configuraciones = await _context.ConfiguracionesMensajes
                    .AsNoTracking()
                    .ToListAsync();

                return Ok(configuraciones);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar las configuraciones de mensajes.");
                return StatusCode(500, new { mensaje = "Error interno al obtener las plantillas de mensajes." });
            }
        }

        // GET: api/ConfiguracionMensajes/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var config = await _context.ConfiguracionesMensajes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (config == null)
                {
                    return NotFound(new { mensaje = "La configuración de plantilla no existe." });
                }

                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar la configuración de mensaje con ID {Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al consultar la plantilla." });
            }
        }

        // PUT: api/ConfiguracionMensajes/5
        [HttpPut("{id}")]
        public async Task<IActionResult> ActualizarPlantilla(int id, [FromBody] ActualizarPlantillaDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { mensaje = "Datos de plantilla inválidos.", detalles = ModelState });
            }

            try
            {
                var config = await _context.ConfiguracionesMensajes.FindAsync(id);
                if (config == null)
                {
                    return NotFound(new { mensaje = "La plantilla especificada no existe." });
                }

                config.PlantillaTexto = dto.PlantillaTexto.Trim();
                config.DiasAnticipacion = dto.DiasAnticipacion;
                config.Activo = dto.Activo;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    mensaje = "Plantilla de mensaje actualizada con éxito.",
                    plantilla = config
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar la plantilla de mensaje con ID {Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al guardar los cambios de la plantilla." });
            }
        }
    }
}