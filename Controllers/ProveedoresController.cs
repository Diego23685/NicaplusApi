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
    [Authorize] // Exclusivo para gestión administrativa
    public class ProveedoresController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProveedoresController> _logger;

        public ProveedoresController(
            ApplicationDbContext context,
            ILogger<ProveedoresController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private DateTime GetNicaraguaTime()
        {
            TimeZoneInfo zone;
            try
            {
                zone = TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                zone = TimeZoneInfo.FindSystemTimeZoneById("America/Managua");
            }

            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
        }

        // 1. GET: api/Proveedores
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                var proveedores = await _context.Proveedores
                    .AsNoTracking()
                    .OrderBy(p => p.RazonSocial)
                    .ToListAsync();

                return Ok(proveedores);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener la lista de proveedores.");
                return StatusCode(500, new { mensaje = "Error interno al recuperar los proveedores." });
            }
        }

        // 2. GET: api/Proveedores/analisis-rendimiento
        [HttpGet("analisis-rendimiento")]
        public async Task<IActionResult> GetAnalisisRendimiento()
        {
            try
            {
                // 1. Obtenemos las compras y sus detalles agregados agrupados por proveedor
                var comprasPorProveedor = await _context.ComprasProveedores
                    .AsNoTracking()
                    .GroupBy(c => c.IdProveedor)
                    .Select(g => new
                    {
                        IdProveedor = g.Key,
                        TotalOrdenes = g.Count(),
                        TotalInvertido = g.Sum(c => c.TotalCompra),
                        TiempoRespuestaPromedio = g.Average(c => (double?)c.TiempoEntregaRealDias) ?? 0.0,
                        MargenGananciaHistorico = g.SelectMany(c => c.Detalles)
                            .Sum(d => d.Producto != null ? (d.Producto.PrecioVenta - d.CostoUnitario) * d.Cantidad : 0m)
                    })
                    .ToListAsync();

                // 2. Obtenemos la lista general de proveedores
                var proveedores = await _context.Proveedores
                    .AsNoTracking()
                    .Select(p => new { p.Id, p.RazonSocial, p.Telefono })
                    .ToListAsync();

                // 3. Cruzamos y calculamos el Score de Confiabilidad en memoria
                var resultadoFinal = proveedores.Select(p =>
                {
                    var stats = comprasPorProveedor.FirstOrDefault(c => c.IdProveedor == p.Id);

                    int totalOrdenes = stats?.TotalOrdenes ?? 0;
                    decimal totalInvertido = stats?.TotalInvertido ?? 0m;
                    double tiempoPromedio = stats != null ? Math.Round(stats.TiempoRespuestaPromedio, 1) : 0.0;
                    decimal margenHistorico = stats?.MargenGananciaHistorico ?? 0m;

                    double score = 100.0;
                    if (tiempoPromedio > 5) score -= 20;
                    if (tiempoPromedio > 10) score -= 30;
                    if (totalOrdenes == 0) score = 0;

                    return new RendimientoProveedorResponseDto
                    {
                        Id = p.Id,
                        RazonSocial = p.RazonSocial,
                        Telefono = p.Telefono,
                        TotalOrdenes = totalOrdenes,
                        TotalInvertido = totalInvertido,
                        MargenGananciaHistorico = margenHistorico,
                        TiempoRespuestaPromedio = tiempoPromedio,
                        ScoreConfiabilidad = Math.Max(0, score)
                    };
                })
                .OrderByDescending(r => r.MargenGananciaHistorico)
                .ToList();

                return Ok(resultadoFinal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al calcular el análisis de rendimiento de proveedores.");
                return StatusCode(500, new { mensaje = "Error interno al generar el informe CRM de proveedores." });
            }
        }

        // 3. POST: api/Proveedores
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CrearProveedorDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { mensaje = "Datos del proveedor incompletos o incorrectos.", detalles = ModelState });
            }

            try
            {
                var proveedor = new Proveedor
                {
                    RazonSocial = dto.RazonSocial.Trim(),
                    Ruc = dto.Ruc?.Trim() ?? string.Empty,
                    Telefono = dto.Telefono?.Trim() ?? string.Empty,
                    Email = dto.Email?.Trim() ?? string.Empty
                };

                _context.Proveedores.Add(proveedor);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    mensaje = "Proveedor registrado exitosamente.",
                    proveedor
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar el proveedor {RazonSocial}", dto.RazonSocial);
                return StatusCode(500, new { mensaje = "Error interno al guardar el proveedor." });
            }
        }

        // 4. PUT: api/Proveedores/5
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] ActualizarProveedorDto dto)
        {
            if (id != dto.Id)
            {
                return BadRequest(new { mensaje = "El ID en la URL no coincide con el cuerpo del modelo." });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new { mensaje = "Datos de actualización inválidos.", detalles = ModelState });
            }

            try
            {
                var proveedorExistente = await _context.Proveedores.FindAsync(id);
                if (proveedorExistente == null)
                {
                    return NotFound(new { mensaje = "El proveedor solicitado no existe." });
                }

                proveedorExistente.RazonSocial = dto.RazonSocial.Trim();
                proveedorExistente.Ruc = dto.Ruc?.Trim() ?? string.Empty;
                proveedorExistente.Telefono = dto.Telefono?.Trim() ?? string.Empty;
                proveedorExistente.Email = dto.Email?.Trim() ?? string.Empty;

                await _context.SaveChangesAsync();

                return Ok(new { mensaje = "Información del proveedor actualizada correctamente.", proveedorId = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar el proveedor ID {Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al actualizar los datos del proveedor." });
            }
        }

        // 5. DELETE: api/Proveedores/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var proveedor = await _context.Proveedores.FindAsync(id);
                if (proveedor == null)
                {
                    return NotFound(new { mensaje = "El proveedor solicitado no existe." });
                }

                var comprasAsociadas = await _context.ComprasProveedores
                    .AsNoTracking()
                    .Where(c => c.IdProveedor == id)
                    .Select(c => new { c.Id, Fecha = c.FechaCompra, Total = c.TotalCompra })
                    .ToListAsync();

                if (comprasAsociadas.Count > 0)
                {
                    return BadRequest(new
                    {
                        error = "Restricción de integridad referencial",
                        mensaje = "No es posible eliminar el proveedor debido a que posee compras registradas.",
                        totalCompras = comprasAsociadas.Count,
                        compras = comprasAsociadas
                    });
                }

                _context.Proveedores.Remove(proveedor);
                await _context.SaveChangesAsync();

                return Ok(new { mensaje = "Proveedor eliminado correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar el proveedor ID {Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al eliminar el proveedor." });
            }
        }

        // 6. POST: api/Proveedores/compras
        [HttpPost("compras")]
        public async Task<IActionResult> RegistrarCompra([FromBody] RegistrarCompraProveedorDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { mensaje = "Los datos de la compra son inválidos.", detalles = ModelState });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var proveedor = await _context.Proveedores.FindAsync(dto.IdProveedor);
                if (proveedor == null)
                {
                    return BadRequest(new { mensaje = "El proveedor especificado no existe." });
                }

                var ahoraNicaragua = GetNicaraguaTime();

                var nuevaCompra = new CompraProveedor
                {
                    IdProveedor = dto.IdProveedor,
                    FechaCompra = ahoraNicaragua,
                    TotalCompra = dto.TotalCompra,
                    Detalles = new List<DetalleCompraProveedor>()
                };

                _context.ComprasProveedores.Add(nuevaCompra);

                foreach (var item in dto.Detalles)
                {
                    var producto = await _context.Productos.FindAsync(item.IdProducto);
                    if (producto == null)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { mensaje = $"El producto con ID {item.IdProducto} no existe." });
                    }

                    // Aumento de inventario físico y actualización de costos
                    if (producto.ControlaStock && !producto.EsDigital)
                    {
                        producto.StockActual += item.Cantidad;
                    }

                    producto.PrecioCosto = item.CostoUnitario;
                    producto.GarantiaDias = item.GarantiaDiasPactada;
                    producto.Proveedor = proveedor.RazonSocial;

                    nuevaCompra.Detalles.Add(new DetalleCompraProveedor
                    {
                        IdProducto = item.IdProducto,
                        Cantidad = item.Cantidad,
                        CostoUnitario = item.CostoUnitario,
                        GarantiaDiasPactada = item.GarantiaDiasPactada
                    });
                }

                // Generación automática del egreso contable en caja
                var egresoCaja = new MovimientoCaja
                {
                    Fecha = ahoraNicaragua,
                    Tipo = "Egreso",
                    Concepto = "Compra Proveedor",
                    Monto = dto.TotalCompra,
                    Detalle = $"Adquisición de mercancía/lote a proveedor: {proveedor.RazonSocial} (ID: {proveedor.Id})",
                    CompraProveedor = nuevaCompra
                };

                _context.MovimientosCaja.Add(egresoCaja);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    mensaje = "Compra registrada y procesada correctamente en inventario y caja.",
                    compraId = nuevaCompra.Id,
                    total = nuevaCompra.TotalCompra
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error crítico durante la transacción de compra a proveedor.");
                return StatusCode(500, new { mensaje = "Error interno al procesar la compra e ingresar el stock." });
            }
        }
    }
}