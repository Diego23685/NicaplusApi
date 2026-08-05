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
    public class ProductsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(
            ApplicationDbContext context,
            ILogger<ProductsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // 1. GET: api/Products (Panel de Administración y Caja POS)
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetProductos()
        {
            try
            {
                var productos = await _context.Productos
                    .AsNoTracking()
                    .Include(p => p.Categoria)
                    .Include(p => p.Juego)
                    .OrderByDescending(p => p.Id)
                    .Select(p => new ProductoAdminResponseDto
                    {
                        Id = p.Id,
                        Nombre = p.Nombre,
                        Descripcion = p.Descripcion,
                        PrecioVenta = p.PrecioVenta,
                        PrecioCosto = p.PrecioCosto,
                        StockActual = p.StockActual,
                        StockMinimo = p.StockMinimo,
                        ImagenUrl = p.ImagenUrl,
                        EsDigital = p.EsDigital,
                        ControlaStock = p.ControlaStock,
                        RequiereServicio = p.RequiereServicio,
                        VisibleEnCatalogo = p.VisibleEnCatalogo,
                        EsSuscripcion = p.EsSuscripcion,
                        DiasDuracion = p.DiasDuracion,
                        GarantiaDias = p.GarantiaDias,
                        Proveedor = p.Proveedor,
                        Estado = p.Estado,
                        CategoriaId = p.CategoriaId,
                        CategoriaNombre = p.Categoria != null ? p.Categoria.Nombre : null,
                        JuegoId = p.JuegoId,
                        JuegoNombre = p.Juego != null ? p.Juego.Nombre : null,

                        // 👈 Obtenemos el ID de la primera credencial
                        PrimerPerfilId = p.EsDigital
                            ? _context.PerfilesCuentas
                                .Where(pc => pc.IdProducto == p.Id && !pc.Ocupado && pc.EstadoPerfil == "Disponible")
                                .OrderBy(pc => pc.Id)
                                .Select(pc => (int?)pc.Id)
                                .FirstOrDefault()
                            : null,

                        // 👈 Obtenemos el texto formateado de la primera credencial
                        MetadataDigital = p.EsDigital 
                            ? _context.PerfilesCuentas
                                .Where(pc => pc.IdProducto == p.Id && !pc.Ocupado && pc.EstadoPerfil == "Disponible")
                                .OrderBy(pc => pc.Id)
                                .Select(pc => $"Cuenta: {pc.CorreoCuenta} | Pass: {pc.PasswordCuenta} | PIN: {pc.PIN}")
                                .FirstOrDefault()
                            : null
                    })
                    .ToListAsync();

                return Ok(productos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar la lista general de productos.");
                return StatusCode(500, new { mensaje = "Error interno al obtener los productos." });
            }
        }

        // GET: api/Products/5/siguiente-credencial?ignorados=12,15
        [HttpGet("{id}/siguiente-credencial")]
        [Authorize]
        public async Task<IActionResult> GetSiguienteCredencial(int id, [FromQuery] string? ignorados = null)
        {
            try
            {
                var idsIgnorados = string.IsNullOrWhiteSpace(ignorados)
                    ? new List<int>()
                    : ignorados.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => int.TryParse(s.Trim(), out int val) ? val : 0)
                            .Where(v => v > 0)
                            .ToList();

                var credencial = await _context.PerfilesCuentas
                    .AsNoTracking()
                    .Where(pc => pc.IdProducto == id && !pc.Ocupado && pc.EstadoPerfil == "Disponible")
                    .Where(pc => !idsIgnorados.Contains(pc.Id))
                    .OrderBy(pc => pc.Id)
                    .Select(pc => new
                    {
                        disponible = true,
                        idPerfil = pc.Id,
                        metadataDigital = $"Cuenta: {pc.CorreoCuenta} | Pass: {pc.PasswordCuenta} | PIN: {pc.PIN}"
                    })
                    .FirstOrDefaultAsync();

                if (credencial == null)
                {
                    return Ok(new { disponible = false, metadataDigital = (string?)null, idPerfil = 0 });
                }

                return Ok(credencial);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar la siguiente credencial para el producto {Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al consultar la credencial." });
            }
        }

        // 2. GET: api/Products/catalogo (Público para Tienda / POS)
        [HttpGet("catalogo")]
        [AllowAnonymous]
        public async Task<IActionResult> GetCatalogoPublico()
        {
            try
            {
                var stockPerfilesPool = await _context.PerfilesCuentas
                    .AsNoTracking()
                    .Where(pc => !pc.Ocupado && pc.EstadoPerfil == "Disponible")
                    .GroupBy(pc => pc.IdProducto)
                    .Select(g => new { IdProducto = g.Key, TotalDisponibles = g.Count() })
                    .ToDictionaryAsync(x => x.IdProducto, x => x.TotalDisponibles);

                var productos = await _context.Productos
                    .AsNoTracking()
                    .Include(p => p.Categoria)
                    .Include(p => p.Juego)
                    .Where(p => p.VisibleEnCatalogo && p.Estado == "Activo")
                    .OrderBy(p => p.Nombre)
                    .ToListAsync();

                var catalogo = productos.Select(p => new ProductoCatalogoResponseDto
                {
                    Id = p.Id,
                    Nombre = p.Nombre,
                    Descripcion = p.Descripcion,
                    PrecioVenta = p.PrecioVenta,
                    ImagenUrl = p.ImagenUrl,
                    EsDigital = p.EsDigital,
                    EsSuscripcion = p.EsSuscripcion,
                    DiasDuracion = p.DiasDuracion,
                    VisibleEnCatalogo = p.VisibleEnCatalogo,
                    CategoriaNombre = p.Categoria?.Nombre,
                    JuegoNombre = p.Juego?.Nombre,
                    StockActual = p.EsSuscripcion
                        ? (stockPerfilesPool.TryGetValue(p.Id, out int stockCalculado) ? stockCalculado : 0)
                        : p.StockActual
                }).ToList();

                return Ok(catalogo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar el catálogo público de productos.");
                return StatusCode(500, new { mensaje = "Error interno al recuperar el catálogo." });
            }
        }

        // 3. GET: api/Products/alertas-stock
        [HttpGet("alertas-stock")]
        [Authorize]
        public async Task<IActionResult> GetAlertasStock()
        {
            try
            {
                var alertas = await _context.Productos
                    .AsNoTracking()
                    .Where(p => p.ControlaStock && !p.EsDigital && !p.RequiereServicio && p.StockActual <= p.StockMinimo)
                    .OrderBy(p => p.StockActual)
                    .Select(p => new ProductoAdminResponseDto
                    {
                        Id = p.Id,
                        Nombre = p.Nombre,
                        StockActual = p.StockActual,
                        StockMinimo = p.StockMinimo,
                        PrecioVenta = p.PrecioVenta,
                        Estado = p.Estado
                    })
                    .ToListAsync();

                return Ok(alertas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar alertas de inventario crítico.");
                return StatusCode(500, new { mensaje = "Error interno al obtener las alertas de stock." });
            }
        }

        // 4. POST: api/Products
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateProducto([FromBody] CrearProductoDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { mensaje = "Datos del producto incompletos o incorrectos.", detalles = ModelState });
            }

            try
            {
                var producto = new Producto
                {
                    Nombre = dto.Nombre.Trim(),
                    Descripcion = dto.Descripcion?.Trim() ?? string.Empty,
                    PrecioVenta = dto.PrecioVenta,
                    PrecioCosto = dto.PrecioCosto,
                    StockActual = (dto.EsDigital || dto.RequiereServicio || !dto.ControlaStock) ? 0 : dto.StockActual,
                    StockMinimo = (dto.EsDigital || dto.RequiereServicio || !dto.ControlaStock) ? 0 : dto.StockMinimo,
                    ImagenUrl = dto.ImagenUrl?.Trim() ?? string.Empty,
                    EsDigital = dto.EsDigital,
                    ControlaStock = dto.ControlaStock,
                    RequiereServicio = dto.RequiereServicio,
                    VisibleEnCatalogo = dto.VisibleEnCatalogo,
                    EsSuscripcion = dto.EsSuscripcion,
                    DiasDuracion = dto.DiasDuracion,
                    GarantiaDias = dto.GarantiaDias,
                    Proveedor = dto.Proveedor?.Trim() ?? string.Empty,
                    Estado = string.IsNullOrWhiteSpace(dto.Estado) ? "Activo" : dto.Estado.Trim(),
                    CategoriaId = dto.CategoriaId,
                    JuegoId = dto.JuegoId
                };

                _context.Productos.Add(producto);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    mensaje = "Producto registrado con éxito.",
                    productoId = producto.Id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear producto {Nombre}", dto.Nombre);
                return StatusCode(500, new { mensaje = "Error interno al registrar el nuevo producto." });
            }
        }

        // 5. PUT: api/Products/5
        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> UpdateProducto(int id, [FromBody] ActualizarProductoDto dto)
        {
            if (id != dto.Id)
            {
                return BadRequest(new { mensaje = "El ID enviado en la ruta no coincide con el cuerpo del modelo." });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new { mensaje = "Datos de actualización inválidos.", detalles = ModelState });
            }

            try
            {
                var productoExistente = await _context.Productos.FindAsync(id);
                if (productoExistente == null)
                {
                    return NotFound(new { mensaje = "El producto que intenta actualizar no existe." });
                }

                productoExistente.Nombre = dto.Nombre.Trim();
                productoExistente.Descripcion = dto.Descripcion?.Trim() ?? string.Empty;
                productoExistente.PrecioVenta = dto.PrecioVenta;
                productoExistente.PrecioCosto = dto.PrecioCosto;
                productoExistente.ImagenUrl = dto.ImagenUrl?.Trim() ?? string.Empty;
                productoExistente.EsDigital = dto.EsDigital;
                productoExistente.ControlaStock = dto.ControlaStock;
                productoExistente.RequiereServicio = dto.RequiereServicio;
                productoExistente.VisibleEnCatalogo = dto.VisibleEnCatalogo;
                productoExistente.EsSuscripcion = dto.EsSuscripcion;
                productoExistente.DiasDuracion = dto.DiasDuracion;
                productoExistente.GarantiaDias = dto.GarantiaDias;
                productoExistente.Proveedor = dto.Proveedor?.Trim() ?? string.Empty;
                productoExistente.Estado = dto.Estado.Trim();
                productoExistente.CategoriaId = dto.CategoriaId;
                productoExistente.JuegoId = dto.JuegoId;

                if (dto.EsDigital || dto.RequiereServicio || !dto.ControlaStock)
                {
                    productoExistente.StockMinimo = 0;
                    productoExistente.StockActual = 0;
                }
                else
                {
                    productoExistente.StockActual = dto.StockActual;
                    productoExistente.StockMinimo = dto.StockMinimo;
                }

                await _context.SaveChangesAsync();

                return Ok(new { mensaje = "Producto actualizado correctamente.", productoId = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar el producto ID {Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al actualizar el producto." });
            }
        }

        // 6. DELETE: api/Products/5
        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> DeleteProducto(int id)
        {
            try
            {
                var producto = await _context.Productos.FindAsync(id);
                if (producto == null)
                {
                    return NotFound(new { mensaje = "El producto solicitado no existe." });
                }

                var tieneVentas = await _context.DetallesVentas.AnyAsync(d => d.IdProducto == id);
                var tieneSuscripciones = await _context.Suscripciones.AnyAsync(s => s.IdProducto == id);

                if (tieneVentas || tieneSuscripciones)
                {
                    producto.VisibleEnCatalogo = false;
                    producto.Estado = "Pausado";
                    await _context.SaveChangesAsync();

                    return Ok(new
                    {
                        mensaje = "El producto posee registros asociados. Se ha pausado y ocultado del catálogo público para preservar el historial."
                    });
                }

                _context.Productos.Remove(producto);
                await _context.SaveChangesAsync();

                return Ok(new { mensaje = "Producto eliminado definitivamente de la base de datos." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar el producto ID {Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al intentar eliminar el producto." });
            }
        }
    }
}