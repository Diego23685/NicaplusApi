using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProveedoresController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ProveedoresController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Proveedores
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Proveedor>>> Get()
        {
            return Ok(await _context.Proveedores.OrderBy(p => p.RazonSocial).ToListAsync());
        }

        // GET: api/Proveedores/analisis-rendimiento (Métricas CRM de rentabilidad y cumplimiento)
        [HttpGet("analisis-rendimiento")]
        public async Task<IActionResult> GetAnalisisRendimiento()
        {
            var proveedores = await _context.Proveedores.ToListAsync();
            var compras = await _context.ComprasProveedores.Include(c => c.Detalles).ThenInclude(d => d.Producto).ToListAsync();

            var reporte = proveedores.Select(p =>
            {
                var comprasDelProveedor = compras.Where(c => c.IdProveedor == p.Id).ToList();
                
                int totalOrdenes = comprasDelProveedor.Count;
                decimal totalInvertido = comprasDelProveedor.Sum(c => c.TotalCompra);
                
                // Promedio de días que tarda en responder/entregar
                double tiempoRespuestaPromedio = totalOrdenes > 0 
                    ? comprasDelProveedor.Average(c => c.TiempoEntregaRealDias) 
                    : 0;

                // Margen promedio generado por los productos de este proveedor
                decimal margenGananciaHistorico = comprasDelProveedor
                    .SelectMany(c => c.Detalles)
                    .Sum(d => d.Producto != null ? (d.Producto.PrecioVenta - d.CostoUnitario) * d.Cantidad : 0);

                // Confiabilidad estimada en porcentaje base
                double scoreConfiabilidad = 100;
                if (tiempoRespuestaPromedio > 5) scoreConfiabilidad -= 20;
                if (tiempoRespuestaPromedio > 10) scoreConfiabilidad -= 30;
                if (totalOrdenes == 0) scoreConfiabilidad = 0;

                return new
                {
                    p.Id,
                    p.RazonSocial,
                    p.Telefono,
                    TotalOrdenes = totalOrdenes,
                    TotalInvertido = totalInvertido,
                    MargenGananciaHistorico = margenGananciaHistorico,
                    TiempoRespuestaPromedio = Math.Round(tiempoRespuestaPromedio, 1),
                    ScoreConfiabilidad = scoreConfiabilidad // % Confiabilidad
                };
            }).OrderByDescending(r => r.MargenGananciaHistorico).ToList();

            return Ok(reporte);
        }

        // POST: api/Proveedores (Crear Proveedor)
        [HttpPost]
        public async Task<ActionResult<Proveedor>> Post([FromBody] Proveedor proveedor)
        {
            _context.Proveedores.Add(proveedor);
            await _context.SaveChangesAsync();
            return Ok(proveedor);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] Proveedor proveedor)
        {
            if (id != proveedor.Id)
                return BadRequest();

            _context.Entry(proveedor).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch
            {
                return NotFound();
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var proveedor = await _context.Proveedores.FindAsync(id);

            if (proveedor == null)
                return NotFound();

            // En lugar de AnyAsync, buscamos las compras reales para enviarlas al cliente
            var comprasAsociadas = await _context.ComprasProveedores
                .Where(c => c.IdProveedor == id)
                .Select(c => new 
                {
                    c.Id,
                    Fecha = c.FechaCompra,
                    Total = c.TotalCompra
                })
                .ToListAsync();

            if (comprasAsociadas.Any())
            {
                // Retornamos un objeto estructurado con el mensaje y la lista de conflictos
                return BadRequest(new
                {
                    error = "Restricción de integridad",
                    mensaje = "No puede eliminarse el proveedor porque tiene compras registradas en el sistema.",
                    compras = comprasAsociadas
                });
            }

            _context.Proveedores.Remove(proveedor);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // POST: api/Proveedores/compras (Registrar Ingreso/Compra Física que suma stock)
        // POST: api/Proveedores/compras (Registrar Ingreso/Compra Física que suma stock)
        [HttpPost("compras")]
        public async Task<IActionResult> RegistrarCompra([FromBody] CompraProveedor compra)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                compra.FechaCompra = DateTime.UtcNow;
                _context.ComprasProveedores.Add(compra);

                foreach (var detalle in compra.Detalles)
                {
                    var producto = await _context.Productos.FindAsync(detalle.IdProducto);
                    if (producto != null)
                    {
                        // Lógica de Negocio: Sumar al stock físico e inyectar el nuevo costo de adquisición
                        producto.StockActual += detalle.Cantidad;
                        producto.PrecioCosto = detalle.CostoUnitario;
                        producto.GarantiaDias = detalle.GarantiaDiasPactada;
                        producto.Proveedor = (await _context.Proveedores.FindAsync(compra.IdProveedor))?.RazonSocial ?? producto.Proveedor;
                    }
                }

                // Registrar de manera automatizada el Egreso en la Caja Contable por reabastecimiento de Lote
                var egresoCaja = new MovimientoCaja
                {
                    Fecha = compra.FechaCompra,
                    Tipo = "Egreso",
                    Concepto = "Compra Proveedor",
                    Monto = compra.TotalCompra,
                    Detalle = $"Adquisición de lote a Proveedor ID: {compra.IdProveedor}",
                    
                    //  CORRECCIÓN: Enlazamos el objeto completo en lugar del ID primitivo 'compra.Id'
                    CompraProveedor = compra 
                };
                _context.MovimientosCaja.Add(egresoCaja);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(compra);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                // Tip pro: Usar ex.InnerException?.Message ayuda a ver el error real de MySQL si vuelve a pasar algo
                var errorDetalle = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, $"Error en transaccion de inventario: {errorDetalle}");
            }
        }
    }
}