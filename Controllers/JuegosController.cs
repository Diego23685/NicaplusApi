using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.DTOs;
using NicaplusApi.Models;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JuegosController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<JuegosController> _logger;

        public JuegosController(
            ApplicationDbContext context,
            ILogger<JuegosController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/Juegos (Público o Lectura Libre para Catálogo)
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                var juegos = await _context.Juegos
                    .AsNoTracking()
                    .Select(j => new JuegoResponseDto
                    {
                        Id = j.Id,
                        Nombre = j.Nombre,
                        ImagenUrl = j.ImagenUrl,
                        CantidadProductosAsociados = _context.Productos.Count(p => p.JuegoId == j.Id)
                    })
                    .OrderBy(j => j.Nombre)
                    .ToListAsync();

                return Ok(juegos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener el catálogo de juegos.");
                return StatusCode(500, new { mensaje = "Error interno al consultar los juegos." });
            }
        }

        // GET: api/Juegos/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var juego = await _context.Juegos
                    .AsNoTracking()
                    .FirstOrDefaultAsync(j => j.Id == id);

                if (juego == null)
                {
                    return NotFound(new { mensaje = "El juego especificado no existe." });
                }

                var response = new JuegoResponseDto
                {
                    Id = juego.Id,
                    Nombre = juego.Nombre,
                    ImagenUrl = juego.ImagenUrl,
                    CantidadProductosAsociados = await _context.Productos.CountAsync(p => p.JuegoId == juego.Id)
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener el juego con ID {Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al consultar el registro." });
            }
        }

        // POST: api/Juegos
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Post([FromBody] CrearJuegoDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { mensaje = "Datos del juego inválidos.", detalles = ModelState });
            }

            try
            {
                var juego = new Juego
                {
                    Nombre = dto.Nombre.Trim(),
                    ImagenUrl = dto.ImagenUrl?.Trim() ?? string.Empty
                };

                _context.Juegos.Add(juego);
                await _context.SaveChangesAsync();

                var response = new JuegoResponseDto
                {
                    Id = juego.Id,
                    Nombre = juego.Nombre,
                    ImagenUrl = juego.ImagenUrl,
                    CantidadProductosAsociados = 0
                };

                return CreatedAtAction(nameof(GetById), new { id = juego.Id }, new
                {
                    mensaje = "Juego/Categoría registrada con éxito.",
                    juego = response
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al guardar un nuevo juego.");
                return StatusCode(500, new { mensaje = "Error interno al guardar el registro." });
            }
        }

        // PUT: api/Juegos/5
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> Put(int id, [FromBody] ActualizarJuegoDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { mensaje = "Datos de actualización inválidos.", detalles = ModelState });
            }

            try
            {
                var juego = await _context.Juegos.FindAsync(id);
                if (juego == null)
                {
                    return NotFound(new { mensaje = "El juego a actualizar no fue encontrado." });
                }

                juego.Nombre = dto.Nombre.Trim();
                juego.ImagenUrl = dto.ImagenUrl?.Trim() ?? string.Empty;

                await _context.SaveChangesAsync();

                var response = new JuegoResponseDto
                {
                    Id = juego.Id,
                    Nombre = juego.Nombre,
                    ImagenUrl = juego.ImagenUrl,
                    CantidadProductosAsociados = await _context.Productos.CountAsync(p => p.JuegoId == juego.Id)
                };

                return Ok(new
                {
                    mensaje = "Juego actualizado con éxito.",
                    juego = response
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar el juego con ID {Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al actualizar los cambios." });
            }
        }

        // DELETE: api/Juegos/5
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var juego = await _context.Juegos.FindAsync(id);
                if (juego == null)
                {
                    return NotFound(new { mensaje = "El juego a eliminar no existe." });
                }

                // Validación previa antes de intentar eliminar en la BD
                var productosVinculados = await _context.Productos
                    .Where(p => p.JuegoId == id)
                    .Select(p => p.Nombre)
                    .ToListAsync();

                if (productosVinculados.Any())
                {
                    return BadRequest(new
                    {
                        mensaje = "No se puede eliminar el juego porque tiene productos asociados en el catálogo.",
                        productos = productosVinculados
                    });
                }

                _context.Juegos.Remove(juego);
                await _context.SaveChangesAsync();

                return Ok(new { mensaje = "Juego eliminado correctamente del catálogo." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al intentar eliminar el juego con ID {Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al eliminar el juego." });
            }
        }
    }
}