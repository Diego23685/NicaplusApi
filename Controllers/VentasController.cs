using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.Models;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VentasController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public VentasController(ApplicationDbContext context) { _context = context; }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Venta>>> Get() => 
            await _context.Ventas.Include(v => v.Detalles).Include(v => v.Cliente).OrderByDescending(v => v.Id).ToListAsync();

        [HttpPost]
        public async Task<ActionResult<Venta>> Post([FromBody] Venta venta)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // CONTROL DE PROTECCIÓN: Si el objeto viene con fecha por defecto o nula, le inyectamos la hora actual exacta
                if (venta.FechaVenta == default(DateTime) || venta.FechaVenta.Year == 1)
                {
                    venta.FechaVenta = DateTime.Now;
                }

                _context.Ventas.Add(venta);

                // Descontar inventario físico de productos no digitales ni servicios
                foreach (var detalle in venta.Detalles)
                {
                    var prod = await _context.Productos.FindAsync(detalle.IdProducto);
                    if (prod != null && !prod.EsDigital && !prod.RequiereServicio)
                    {
                        if (prod.StockActual < detalle.Cantidad)
                            return BadRequest($"Stock insuficiente para el producto: {prod.Nombre}");
                        
                        prod.StockActual -= detalle.Cantidad;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(venta);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, "Error interno al procesar la transacción financiera.");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] Venta ventaActualizada)
        {
            if (id != ventaActualizada.Id) return BadRequest("IDs no coinciden.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var ventaOriginal = await _context.Ventas.Include(v => v.Detalles)
                    .FirstOrDefaultAsync(v => v.Id == id);
                
                if (ventaOriginal == null) return NotFound("Venta no encontrada.");

                // 1. REVERSIÓN DE STOCK
                foreach (var detalleOrig in ventaOriginal.Detalles)
                {
                    var prod = await _context.Productos.FindAsync(detalleOrig.IdProducto);
                    if (prod != null && !prod.EsDigital && !prod.RequiereServicio)
                    {
                        prod.StockActual += detalleOrig.Cantidad;
                    }
                }

                // 2. APLICAR NUEVOS VALORES FINANCIEROS
                ventaOriginal.MetodoPago = ventaActualizada.MetodoPago;
                ventaOriginal.IdCliente = ventaActualizada.IdCliente;

                // CORREGIDO: Remoción limpia de detalles desde la colección sin requerir DbSet explícito
                _context.RemoveRange(ventaOriginal.Detalles);

                // 3. DESCONTAR EL NUEVO STOCK E INYECTAR NUEVOS DETALLES
                ventaOriginal.Detalles = ventaActualizada.Detalles;
                foreach (var nuevoDetalle in ventaOriginal.Detalles)
                {
                    var prod = await _context.Productos.FindAsync(nuevoDetalle.IdProducto);
                    if (prod != null && !prod.EsDigital && !prod.RequiereServicio)
                    {
                        if (prod.StockActual < nuevoDetalle.Cantidad)
                            return BadRequest($"Stock insuficiente tras reajuste para: {prod.Nombre}");
                        
                        prod.StockActual -= nuevoDetalle.Cantidad;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return NoContent();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, "Fallo al revertir o recalcular stock y flujos de caja.");
            }
        }
    }
}