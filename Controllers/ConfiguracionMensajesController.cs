using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.Models;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ConfiguracionMensajesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ConfiguracionMensajesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/ConfiguracionMensajes
        // (Para que React pinte todas las plantillas disponibles)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ConfiguracionMensaje>>> GetConfiguraciones()
        {
            return await _context.ConfiguracionesMensajes.ToListAsync();
        }

        // PUT: api/ConfiguracionMensajes/{id}
        // (Para cuando el usuario edite el texto o los días en React y presione "Guardar")
        [HttpPut("{id}")]
        public async Task<IActionResult> ActualizarPlantilla(int id, [FromBody] ActualizarPlantillaDto dto)
        {
            var config = await _context.ConfiguracionesMensajes.FindAsync(id);
            if (config == null) return NotFound("La configuración no existe.");

            config.PlantillaTexto = dto.PlantillaTexto;
            config.DiasAnticipacion = dto.DiasAnticipacion;
            config.Activo = dto.Activo;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        public class ActualizarPlantillaDto
        {
            public required string PlantillaTexto { get; set; }
            public int DiasAnticipacion { get; set; }
            public bool Activo { get; set; }
        }
    }
}