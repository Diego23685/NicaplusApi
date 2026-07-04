using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.Models;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClientesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ClientesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. Obtener todos los clientes (Para el selector de la Caja POS y Taller)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Cliente>>> GetClientes()
        {
            return await _context.Clientes.OrderBy(c => c.Nombre).ToListAsync();
        }

        // 2. Registrar un nuevo cliente (Maneja el flujo de Caja y la creación en caliente desde Taller)
        [HttpPost]
        public async Task<ActionResult<Cliente>> CrearCliente([FromBody] Cliente cliente)
        {
            if (string.IsNullOrWhiteSpace(cliente.Nombre) || string.IsNullOrWhiteSpace(cliente.Telefono))
            {
                return BadRequest("El nombre y el teléfono son campos obligatorios.");
            }

            // Evitar duplicados por número de teléfono
            var clienteExistente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.Telefono == cliente.Telefono);

            if (clienteExistente != null)
            {
                // Si ya existe, devolvemos el cliente actual para que el frontend lo asocie sin quebrarse
                return Ok(clienteExistente);
            }

            cliente.PuntosAcumulados = 0; // Asegurar consistencia de puntos iniciales

            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetClientes), new { id = cliente.Id }, cliente);
        }

        // 3. Actualizar datos de un cliente existente (PUT)
        [HttpPut("{id}")]
        public async Task<IActionResult> ActualizarCliente(int id, [FromBody] Cliente cliente)
        {
            if (id != cliente.Id)
            {
                return BadRequest("El ID del cliente no coincide con la petición.");
            }

            if (string.IsNullOrWhiteSpace(cliente.Nombre) || string.IsNullOrWhiteSpace(cliente.Telefono))
            {
                return BadRequest("El nombre y el teléfono son obligatorios.");
            }

            _context.Entry(cliente).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Clientes.AnyAsync(c => c.Id == id))
                {
                    return NotFound("Cliente no encontrado.");
                }
                throw;
            }

            return NoContent();
        }

        // 4. Eliminar un cliente de la base de datos (DELETE)
        [HttpDelete("{id}")]
        public async Task<IActionResult> EliminarCliente(int id)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null)
            {
                return NotFound("Cliente no encontrado.");
            }

            // Nota de consistencia: Si el cliente tiene ventas u órdenes asociadas, 
            // MySQL bloqueará la eliminación por llave foránea a menos que se maneje en cascada o nulo.
            _context.Clientes.Remove(cliente);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}