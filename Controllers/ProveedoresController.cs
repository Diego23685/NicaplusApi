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
    [Authorize]
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

        // NUEVO: GET: api/Proveedores/compras (Historial de Compras)
        [HttpGet("compras")]
        public async Task<IActionResult> GetHistorialCompras()
        {
            try
            {
                var compras = await _context.ComprasProveedores
                    .AsNoTracking()
                    .Include(c => c.Proveedor)
                    .Include(c => c.Detalles!)
                        .ThenInclude(d => d.Producto)
                    .OrderByDescending(c => c.Id)
                    .Select(c => new CompraResumenDto
                    {
                        Id = c.Id,
                        IdProveedor = c.IdProveedor,
                        ProveedorNombre = c.Proveedor != null ? c.Proveedor.RazonSocial : "Proveedor General",
                        FechaCompra = c.FechaCompra.ToString("yyyy-MM-dd HH:mm:ss"),
                        TotalCompra = c.TotalCompra,
                        Observaciones = c.Observaciones, // Incluir observaciones en el resumen
                        Detalles = c.Detalles.Select(d => new DetalleCompraResumenDto
                        {
                            Id = d.Id,
                            IdProducto = d.IdProducto,
                            ProductoNombre = d.Producto != null ? d.Producto.Nombre : "Producto General",
                            Cantidad = d.Cantidad,
                            CostoUnitario = d.CostoUnitario,
                            SubTotal = d.Cantidad * d.CostoUnitario,
                            GarantiaDiasPactada = d.GarantiaDiasPactada
                        }).ToList()
                    })
                    .ToListAsync();

                return Ok(compras);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener historial de compras.");
                return StatusCode(500, new { mensaje = "Error interno al obtener el historial de compras." });
            }
        }

        [HttpGet("analisis-rendimiento")]
        public async Task<IActionResult> GetAnalisisRendimiento()
        {
            try
            {
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

                var proveedores = await _context.Proveedores
                    .AsNoTracking()
                    .Select(p => new { p.Id, p.RazonSocial, p.Telefono })
                    .ToListAsync();

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
                return StatusCode(500, new { mensaje = "Error interno al generar el informe." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CrearProveedorDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { mensaje = "Datos incompletos.", detalles = ModelState });

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

                return Ok(new { mensaje = "Proveedor registrado exitosamente.", proveedor });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar el proveedor.");
                return StatusCode(500, new { mensaje = "Error interno al guardar." });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] ActualizarProveedorDto dto)
        {
            if (id != dto.Id)
                return BadRequest(new { mensaje = "El ID en la URL no coincide con el cuerpo." });

            if (!ModelState.IsValid)
                return BadRequest(new { mensaje = "Datos de actualización inválidos." });

            try
            {
                var proveedorExistente = await _context.Proveedores.FindAsync(id);
                if (proveedorExistente == null)
                    return NotFound(new { mensaje = "El proveedor solicitado no existe." });

                proveedorExistente.RazonSocial = dto.RazonSocial.Trim();
                proveedorExistente.Ruc = dto.Ruc?.Trim() ?? string.Empty;
                proveedorExistente.Telefono = dto.Telefono?.Trim() ?? string.Empty;
                proveedorExistente.Email = dto.Email?.Trim() ?? string.Empty;

                await _context.SaveChangesAsync();
                return Ok(new { mensaje = "Información actualizada correctamente.", proveedorId = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar proveedor.");
                return StatusCode(500, new { mensaje = "Error interno al actualizar." });
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var proveedor = await _context.Proveedores.FindAsync(id);
                if (proveedor == null)
                    return NotFound(new { mensaje = "El proveedor solicitado no existe." });

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
                _logger.LogError(ex, "Error al eliminar proveedor.");
                return StatusCode(500, new { mensaje = "Error interno al eliminar." });
            }
        }

        [HttpPost("compras")]
        public async Task<IActionResult> RegistrarCompra([FromBody] RegistrarCompraProveedorDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { mensaje = "Los datos de la compra son inválidos.", detalles = ModelState });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var proveedor = await _context.Proveedores.FindAsync(dto.IdProveedor);
                if (proveedor == null)
                    return BadRequest(new { mensaje = "El proveedor especificado no existe." });

                var ahoraNicaragua = GetNicaraguaTime();

                var nuevaCompra = new CompraProveedor
                {
                    IdProveedor = dto.IdProveedor,
                    FechaCompra = ahoraNicaragua,
                    TotalCompra = dto.TotalCompra,
                    Observaciones = dto.Observaciones?.Trim(),
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

                    if (producto.ControlaStock && !producto.EsDigital)
                    {
                        producto.StockActual += item.Cantidad;
                        if (producto.Estado == "Agotado" && producto.StockActual > 0) producto.Estado = "Activo";
                    }

                    producto.PrecioCosto = item.CostoUnitario;
                    producto.GarantiaDias = item.GarantiaDiasPactada;
                    producto.Proveedor = proveedor.RazonSocial;

                    // NUEVO: Si se envió un nuevo PrecioVenta, se actualiza el catálogo al instante
                    if (item.NuevoPrecioVenta.HasValue && item.NuevoPrecioVenta.Value > 0)
                    {
                        producto.PrecioVenta = item.NuevoPrecioVenta.Value;
                    }

                    nuevaCompra.Detalles.Add(new DetalleCompraProveedor
                    {
                        IdProducto = item.IdProducto,
                        Cantidad = item.Cantidad,
                        CostoUnitario = item.CostoUnitario,
                        GarantiaDiasPactada = item.GarantiaDiasPactada
                    });
                }

                var egresoCaja = new MovimientoCaja
                {
                    Fecha = ahoraNicaragua,
                    Tipo = "Egreso",
                    Concepto = "Compra Proveedor",
                    Monto = dto.TotalCompra,
                    Detalle = $"Adquisición a proveedor: {proveedor.RazonSocial} (ID: {proveedor.Id})",
                    CompraProveedor = nuevaCompra
                };

                _context.MovimientosCaja.Add(egresoCaja);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    mensaje = "Compra registrada, stock incrementado y egreso contable generado.",
                    compraId = nuevaCompra.Id,
                    total = nuevaCompra.TotalCompra
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al procesar la compra.");
                return StatusCode(500, new { mensaje = "Error interno al procesar la compra." });
            }
        }

        // PUT: api/Proveedores/compras/5 (Edición Reversiva de Compra)
        [HttpPut("compras/{id}")]
        public async Task<IActionResult> EditarCompra(int id, [FromBody] RegistrarCompraProveedorDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { mensaje = "Datos inválidos.", detalles = ModelState });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var compraOriginal = await _context.ComprasProveedores
                    .Include(c => c.Detalles)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (compraOriginal == null)
                    return NotFound(new { mensaje = "La orden de compra no existe." });

                var proveedor = await _context.Proveedores.FindAsync(dto.IdProveedor);
                if (proveedor == null)
                    return BadRequest(new { mensaje = "El proveedor especificado no existe." });

                // 1. Revertir el stock de la compra anterior
                foreach (var detalleOrig in compraOriginal.Detalles)
                {
                    var prod = await _context.Productos.FindAsync(detalleOrig.IdProducto);
                    if (prod != null && prod.ControlaStock && !prod.EsDigital)
                    {
                        prod.StockActual -= detalleOrig.Cantidad;
                        if (prod.StockActual <= 0)
                        {
                            prod.StockActual = 0;
                            prod.Estado = "Agotado";
                        }
                    }
                }

                // Limpiar detalles anteriores
                _context.DetallesComprasProveedores.RemoveRange(compraOriginal.Detalles);
                await _context.SaveChangesAsync();

                // 2. Aplicar los nuevos datos
                compraOriginal.IdProveedor = dto.IdProveedor;
                compraOriginal.TotalCompra = dto.TotalCompra;
                compraOriginal.Observaciones = dto.Observaciones?.Trim();
                compraOriginal.Detalles = new List<DetalleCompraProveedor>();

                foreach (var item in dto.Detalles)
                {
                    var producto = await _context.Productos.FindAsync(item.IdProducto);
                    if (producto == null)
                    {
                        await transaction.RollbackAsync();
                        return BadRequest(new { mensaje = $"El producto con ID {item.IdProducto} no existe." });
                    }

                    if (producto.ControlaStock && !producto.EsDigital)
                    {
                        producto.StockActual += item.Cantidad;
                        if (producto.Estado == "Agotado" && producto.StockActual > 0) 
                            producto.Estado = "Activo";
                    }

                    producto.PrecioCosto = item.CostoUnitario;
                    producto.GarantiaDias = item.GarantiaDiasPactada;
                    producto.Proveedor = proveedor.RazonSocial;

                    if (item.NuevoPrecioVenta.HasValue && item.NuevoPrecioVenta.Value > 0)
                    {
                        producto.PrecioVenta = item.NuevoPrecioVenta.Value;
                    }

                    compraOriginal.Detalles.Add(new DetalleCompraProveedor
                    {
                        IdProducto = item.IdProducto,
                        Cantidad = item.Cantidad,
                        CostoUnitario = item.CostoUnitario,
                        GarantiaDiasPactada = item.GarantiaDiasPactada
                    });
                }

                // 3. Sincronizar el egreso de Caja asociado
                var egresoCaja = await _context.MovimientosCaja.FirstOrDefaultAsync(m => m.IdCompraProveedor == id);
                if (egresoCaja != null)
                {
                    egresoCaja.Monto = dto.TotalCompra;
                    egresoCaja.Detalle = $"Adquisición (Editada) a proveedor: {proveedor.RazonSocial} (ID: {proveedor.Id})";
                    _context.MovimientosCaja.Update(egresoCaja);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { mensaje = $"Orden de compra #{id} actualizada exitosamente. Inventario y egreso en caja recalculados." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al editar la compra #{Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al actualizar la compra." });
            }
        }

        // NUEVO: DELETE: api/Proveedores/compras/5 (Anulación / Reversión de Compra)
        [HttpDelete("compras/{id}")]
        public async Task<IActionResult> AnularCompra(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var compra = await _context.ComprasProveedores
                    .Include(c => c.Detalles)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (compra == null)
                    return NotFound(new { mensaje = "La orden de compra no existe." });

                // 1. Revertir Stock
                foreach (var detalle in compra.Detalles)
                {
                    var prod = await _context.Productos.FindAsync(detalle.IdProducto);
                    if (prod != null && prod.ControlaStock && !prod.EsDigital)
                    {
                        prod.StockActual -= detalle.Cantidad;
                        if (prod.StockActual <= 0)
                        {
                            prod.StockActual = 0;
                            prod.Estado = "Agotado";
                        }
                    }
                }

                // 2. Anular / Eliminar Movimiento de Caja asociado
                var egresoCaja = await _context.MovimientosCaja.FirstOrDefaultAsync(m => m.IdCompraProveedor == id);
                if (egresoCaja != null)
                {
                    _context.MovimientosCaja.Remove(egresoCaja);
                }

                // 3. Eliminar Detalles y Registro de Compra
                _context.DetallesComprasProveedores.RemoveRange(compra.Detalles);
                _context.ComprasProveedores.Remove(compra);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { mensaje = $"Orden de compra #{id} anulada. Se descontó el stock y se revirtió el egreso en caja." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al anular la compra #{Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al anular la orden de compra." });
            }
        }
    }
}