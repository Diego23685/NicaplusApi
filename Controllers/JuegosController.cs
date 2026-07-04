using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.Models;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JuegosController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public JuegosController(ApplicationDbContext context) { _context = context; }

        [HttpGet] 
        public async Task<ActionResult<IEnumerable<Juego>>> Get() => await _context.Juegos.ToListAsync();

        [HttpPost]
        public async Task<ActionResult<Juego>> Post([FromBody] Juego juego)
        {
            _context.Juegos.Add(juego);
            await _context.SaveChangesAsync();
            return Ok(juego);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] Juego juego)
        {
            if (id != juego.Id) return BadRequest("El ID del juego no coincide.");

            _context.Entry(juego).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!JuegoExists(id)) return NotFound();
                throw;
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var juego = await _context.Juegos.FindAsync(id);
            if (juego == null) return NotFound();

            try
            {
                _context.Juegos.Remove(juego);
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (DbUpdateException)
            {
                // Obtenemos la lista de productos que están bloqueando la eliminación
                var productosVinculados = await _context.Productos
                    .Where(p => p.JuegoId == id)
                    .Select(p => p.Nombre)
                    .ToListAsync();

                // Retornamos un objeto detallado al frontend
                return BadRequest(new {
                    mensaje = "No se puede eliminar el juego porque tiene productos asociados.",
                    productos = productosVinculados
                });
            }
        }

        private bool JuegoExists(int id) => _context.Juegos.Any(e => e.Id == id);
    }
}