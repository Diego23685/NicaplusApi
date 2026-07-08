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
        
        // Zona horaria estándar para Nicaragua
        private static readonly TimeZoneInfo NicaraguaZone = TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");

        public ProveedoresController(ApplicationDbContext context)
        {
            _context = context;
        }

        private DateTime GetNicaraguaTime()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, NicaraguaZone);
        }

        // GET: api/Proveedores
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Proveedor>>> Get()
        {
            return Ok(await _context.Proveedores.OrderBy(p => p.RazonSocial).ToListAsync());
        }

        // GET: api/Proveedores/analisis-rendimiento (Métricas CRM optimizadas en Base de Datos)
        [HttpGet("analisis-rendimiento")]
        public async Task<IActionResult> GetAnalisisRendimiento()
        {
            // CORREGIDO: La proyección Select mapea y calcula directo en SQL, no en memoria RAM.
            var reporte = await _context.Proveedores
                .Select(p => new
                {
                    p.Id,
                    p.RazonSocial,
                    p.Telefono,
                    TotalOrdenes = _context.ComprasProveedores.Count(c => c.IdProveedor == p.Id),
                    TotalInvertido = _context.ComprasProveedores.Where(c => c.IdProveedor == p.Id).Sum(c => c.TotalCompra),
                    
                    TiempoRespuestaPromedio = _context.ComprasProveedores.Where(c => c.IdProveedor == p.Id).Any()
                        ? _context.ComprasProveedores.Where(c => c.IdProveedor == p.Id).Average(c => c.TiempoEntregaRealDias)
                        : 0,

                    MargenGananciaHistorico = _context.ComprasProveedores
                        .Where(c => c.IdProveedor == p.Id)
                        .SelectMany(c => c.Detalles)
                        .Sum(d => d.Producto != null ? (d.Producto.PrecioVenta - d.CostoUnitario) * d.Cantidad : 0)
                })
                .ToListAsync();

            // Evaluamos el Score de Confiabilidad y redondeos en memoria sobre el resultado final optimizado
            var resultadoFinal = reporte.Select(r =>
            {
                double scoreConfiabilidad = 100;
                if (r.TiempoRespuestaPromedio > 5) scoreConfiabilidad -= 20;
                if (r.TiempoRespuestaPromedio > 10) scoreConfiabilidad -= 30;
                if (r.TotalOrdenes == 0) scoreConfiabilidad = 0;

                return new
                {
                    r.Id,
                    r.RazonSocial,
                    r.Telefono,
                    r.TotalOrdenes,
                    r.TotalInvertido,
                    r.MargenGananciaHistorico,
                    TiempoRespuestaPromedio = Math.Round(r.TiempoRespuestaPromedio, 1),
                    ScoreConfiabilidad = scoreConfiabilidad
                };
            }).OrderByDescending(r => r.MargenGananciaHistorico).ToList();

            return Ok(resultadoFinal);
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

            try { await _context.SaveChangesAsync(); }
            catch { return NotFound(); }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var proveedor = await _context.Proveedores.FindAsync(id);
            if (proveedor == null) return NotFound();

            var comprasAsociadas = await _context.ComprasProveedores
                .Where(c => c.IdProveedor == id)
                .Select(c => new { c.Id, Fecha = c.FechaCompra, Total = c.TotalCompra })
                .ToListAsync();

            if (comprasAsociadas.Any())
            {
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

        // POST: api/Proveedores/compras
        [HttpPost("compras")]
        public async Task<IActionResult> RegistrarCompra([FromBody] CompraProveedor compra)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var ahoraNicaragua = GetNicaraguaTime();

                // CORREGIDO: Seteamos la hora exacta de Nicaragua para la compra física
                compra.FechaCompra = ahoraNicaragua;
                _context.ComprasProveedores.Add(compra);

                foreach (var detalle in compra.Detalles)
                {
                    var producto = await _context.Productos.FindAsync(detalle.IdProducto);
                    if (producto != null)
                    {
                        producto.StockActual += detalle.Cantidad;
                        producto.PrecioCosto = detalle.CostoUnitario;
                        producto.GarantiaDias = detalle.GarantiaDiasPactada;
                        producto.Proveedor = (await _context.Proveedores.FindAsync(compra.IdProveedor))?.RazonSocial ?? producto.Proveedor;
                    }
                }

                // CORREGIDO: El egreso contable se amarra a la misma línea temporal de Nicaragua
                var egresoCaja = new MovimientoCaja
                {
                    Fecha = ahoraNicaragua,
                    Tipo = "Egreso",
                    Concepto = "Compra Proveedor",
                    Monto = compra.TotalCompra,
                    Detalle = $"Adquisición de lote a Proveedor ID: {compra.IdProveedor}",
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
                var errorDetalle = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, $"Error en transaccion de inventario: {errorDetalle}");
            }
        }
    }
}