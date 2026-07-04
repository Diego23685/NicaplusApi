using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.Models;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriasController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public CategoriasController(ApplicationDbContext context) { _context = context; }

        [HttpGet] 
        public async Task<ActionResult<IEnumerable<Categoria>>> Get() => await _context.Categorias.ToListAsync();

        [HttpPost]
        public async Task<ActionResult<Categoria>> Post([FromBody] Categoria cat)
        {
            _context.Categorias.Add(cat);
            await _context.SaveChangesAsync();
            return Ok(cat);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] Categoria cat)
        {
            if (id != cat.Id) return BadRequest("El ID de la categoría no coincide.");

            _context.Entry(cat).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CategoriaExists(id)) return NotFound();
                throw;
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var cat = await _context.Categorias.FindAsync(id);
            if (cat == null) return NotFound();

            try
            {
                _context.Categorias.Remove(cat);
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (DbUpdateException)
            {
                // Obtenemos la lista de productos que están bloqueando la eliminación
                var productosVinculados = await _context.Productos
                    .Where(p => p.CategoriaId == id)
                    .Select(p => p.Nombre)
                    .ToListAsync();

                return BadRequest(new {
                    mensaje = "No se puede eliminar la categoría porque tiene productos asociados.",
                    productos = productosVinculados
                });
            }
        }

        private bool CategoriaExists(int id) => _context.Categorias.Any(e => e.Id == id);
    }
}