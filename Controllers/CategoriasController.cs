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
    [Authorize] // Protegemos los endpoints para administradores/operadores
    public class CategoriasController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CategoriasController> _logger;

        public CategoriasController(
            ApplicationDbContext context,
            ILogger<CategoriasController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/Categorias
        [HttpGet]
        [AllowAnonymous] // Permitir consulta pública para la tienda online si es necesario
        public async Task<IActionResult> Get()
        {
            try
            {
                var categorias = await _context.Categorias
                    .AsNoTracking()
                    .OrderBy(c => c.Nombre)
                    .Select(c => new CategoriaResponseDto
                    {
                        Id = c.Id,
                        Nombre = c.Nombre
                    })
                    .ToListAsync();

                return Ok(categorias);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar las categorías.");
                return StatusCode(500, new { mensaje = "Error interno al obtener el catálogo de categorías." });
            }
        }

        // GET: api/Categorias/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var cat = await _context.Categorias
                    .AsNoTracking()
                    .Where(c => c.Id == id)
                    .Select(c => new CategoriaResponseDto
                    {
                        Id = c.Id,
                        Nombre = c.Nombre
                    })
                    .FirstOrDefaultAsync();

                if (cat == null)
                {
                    return NotFound(new { mensaje = "La categoría solicitada no existe." });
                }

                return Ok(cat);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener la categoría con ID {Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al consultar la categoría." });
            }
        }

        // POST: api/Categorias
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CrearCategoriaDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { mensaje = "Datos de categoría inválidos.", detalles = ModelState });
            }

            try
            {
                var nombreLimpio = dto.Nombre.Trim();

                if (await _context.Categorias.AnyAsync(c => c.Nombre.ToLower() == nombreLimpio.ToLower()))
                {
                    return BadRequest(new { mensaje = "Ya existe una categoría con el mismo nombre." });
                }

                var nuevaCategoria = new Categoria
                {
                    Nombre = nombreLimpio
                };

                _context.Categorias.Add(nuevaCategoria);
                await _context.SaveChangesAsync();

                var response = new CategoriaResponseDto
                {
                    Id = nuevaCategoria.Id,
                    Nombre = nuevaCategoria.Nombre
                };

                return CreatedAtAction(nameof(GetById), new { id = nuevaCategoria.Id }, new { mensaje = "Categoría creada exitosamente.", categoria = response });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear nueva categoría.");
                return StatusCode(500, new { mensaje = "Error interno al registrar la categoría." });
            }
        }

        // PUT: api/Categorias/5
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] CrearCategoriaDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { mensaje = "Datos de categoría inválidos.", detalles = ModelState });
            }

            try
            {
                var categoria = await _context.Categorias.FindAsync(id);
                if (categoria == null)
                {
                    return NotFound(new { mensaje = "La categoría a actualizar no existe." });
                }

                var nombreLimpio = dto.Nombre.Trim();

                // Verificar que no duplique a OTRA categoría existente
                if (await _context.Categorias.AnyAsync(c => c.Nombre.ToLower() == nombreLimpio.ToLower() && c.Id != id))
                {
                    return BadRequest(new { mensaje = "Ya existe otra categoría con el mismo nombre." });
                }

                categoria.Nombre = nombreLimpio;
                await _context.SaveChangesAsync();

                return Ok(new { mensaje = "Categoría actualizada correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar la categoría con ID {Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al modificar la categoría." });
            }
        }

        // DELETE: api/Categorias/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var cat = await _context.Categorias.FindAsync(id);
                if (cat == null)
                {
                    return NotFound(new { mensaje = "La categoría no existe." });
                }

                // 1. Verificación previa en lugar de esperar la excepción de la DB
                var productosVinculados = await _context.Productos
                    .AsNoTracking()
                    .Where(p => p.CategoriaId == id)
                    .Select(p => p.Nombre)
                    .ToListAsync();

                if (productosVinculados.Any())
                {
                    return BadRequest(new
                    {
                        mensaje = "No se puede eliminar la categoría porque tiene productos asociados.",
                        totalProductos = productosVinculados.Count,
                        productos = productosVinculados
                    });
                }

                _context.Categorias.Remove(cat);
                await _context.SaveChangesAsync();

                return Ok(new { mensaje = "Categoría eliminada con éxito." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al intentar eliminar la categoría con ID {Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al eliminar la categoría." });
            }
        }
    }
}