using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.Models;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ProductsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. Obtener todos los productos (Para el panel de administración)
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Producto>>> GetProductos()
        {
            return await _context.Productos.ToListAsync();
        }

        // 2. Obtener catálogo público (Para la tienda online / WhatsApp)
        [HttpGet("catalogo")]
        public async Task<ActionResult<IEnumerable<Producto>>> GetCatalogoPublico()
        {
            return await _context.Productos
                .Where(p => p.VisibleEnCatalogo && (!p.RequiereServicio || p.EsDigital || p.StockActual > 0))
                .ToListAsync();
        }

        // 3. Obtener alertas de Stock Bajo
        [HttpGet("alertas-stock")]
        public async Task<ActionResult<IEnumerable<Producto>>> GetAlertasStock()
        {
            return await _context.Productos
                .Where(p => !p.EsDigital && !p.RequiereServicio && p.StockActual <= p.StockMinimo)
                .ToListAsync();
        }

        // 4. Crear un producto nuevo (Físico, Digital o Servicio)
        [HttpPost]
        public async Task<ActionResult<Producto>> CreateProducto([FromBody] Producto producto)
        {
            if (producto.EsDigital || producto.RequiereServicio)
            {
                // Conservamos la asignación automática pero respetando si el cliente maneja stock de pantallas
                producto.StockMinimo = 0; 
            }

            _context.Productos.Add(producto);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetProductos), new { id = producto.Id }, producto);
        }

        // 5. Actualizar producto existente
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProducto(int id, [FromBody] Producto producto)
        {
            if (id != producto.Id)
            {
                return BadRequest("El ID del producto no coincide.");
            }

            _context.Entry(producto).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Productos.AnyAsync(p => p.Id == id))
                {
                    return NotFound("Producto no encontrado.");
                }
                throw;
            }

            return NoContent();
        }

        // 6. Eliminar producto
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProducto(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null) return NotFound();

            // Verificamos si tiene historial para decidir si borrar físico o lógico
            var tieneVentas = await _context.DetallesVentas.AnyAsync(d => d.IdProducto == id);
            var tieneSuscripciones = await _context.Suscripciones.AnyAsync(s => s.IdProducto == id);

            if (tieneVentas || tieneSuscripciones)
            {
                // Si tiene historial, no destruimos datos contables: hacemos borrado lógico
                producto.VisibleEnCatalogo = false;
                // Si agregaste el campo Activo: producto.Activo = false;
                _context.Productos.Update(producto);
                await _context.SaveChangesAsync();
                return Ok(new { mensaje = "El producto tiene historial comercial. Se ha ocultado del catálogo y desactivado para nuevas ventas." });
            }

            // Si está completamente limpio, se puede borrar físicamente
            _context.Productos.Remove(producto);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}