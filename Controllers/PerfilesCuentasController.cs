using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PerfilesCuentasController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PerfilesCuentasController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. GET: api/perfilescuentas/producto/{idProducto}
        // Devuelve las pantallas/perfiles asociados a un servicio de streaming específico
        [HttpGet("producto/{idProducto}")]
        public async Task<ActionResult<IEnumerable<PerfilCuenta>>> GetPerfilesPorProducto(int idProducto)
        {
            return await _context.PerfilesCuentas
                .Where(p => p.IdProducto == idProducto)
                .OrderBy(p => p.NombrePerfil)
                .ToListAsync();
        }

        // 2. POST: api/perfilescuentas
        // Inserta un nuevo perfil/pantalla libre al pool de una cuenta mayor
        [HttpPost]
        public async Task<ActionResult<PerfilCuenta>> Post([FromBody] PerfilCuenta perfil)
        {
            if (perfil.IdProducto == 0)
            {
                return BadRequest("El ID del producto base es obligatorio.");
            }

            _context.PerfilesCuentas.Add(perfil);
            await _context.SaveChangesAsync();

            return Ok(perfil);
        }

        // 3. DELETE: api/perfilescuentas/{id}
        // Elimina un perfil si no está ocupado o vendido por motivos de integridad relacional
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var perfil = await _context.PerfilesCuentas.FindAsync(id);
            if (perfil == null)
            {
                return NotFound("El perfil no existe.");
            }

            if (perfil.Ocupado)
            {
                return BadRequest("No se puede eliminar un perfil que se encuentra actualmente asignado a un cliente.");
            }

            _context.PerfilesCuentas.Remove(perfil);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}