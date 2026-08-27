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
                var stockPerfilesPool = await _context.PerfilesCuentas
                    .AsNoTracking()
                    .Where(pc => !pc.Ocupado && pc.EstadoPerfil == "Disponible")
                    .GroupBy(pc => pc.IdProducto)
                    .Select(g => new { IdProducto = g.Key, TotalDisponibles = g.Count() })
                    .ToDictionaryAsync(x => x.IdProducto, x => x.TotalDisponibles);

                var stockCodigosPool = await _context.CodigosDigitales
                    .AsNoTracking()
                    .Where(c => !c.Vendido && c.Estado == "Disponible")
                    .GroupBy(c => c.IdProducto)
                    .Select(g => new { IdProducto = g.Key, TotalDisponibles = g.Count() })
                    .ToDictionaryAsync(x => x.IdProducto, x => x.TotalDisponibles);

                var productos = await _context.Productos
                    .AsNoTracking()
                    .Include(p => p.Categoria)
                    .Include(p => p.Juego)
                    .Include(p => p.Variaciones)
                    .OrderByDescending(p => p.Id)
                    .ToListAsync();

                var respuesta = productos.Select(p => new ProductoAdminResponseDto
                {
                    Id = p.Id,
                    Nombre = p.Nombre,
                    Descripcion = p.Descripcion,
                    PrecioVenta = p.PrecioVenta,
                    PrecioCosto = p.PrecioCosto,
                    StockMinimo = p.StockMinimo,
                    ImagenUrl = p.ImagenUrl,
                    EsDigital = p.EsDigital,
                    EsCodigoDigital = p.EsCodigoDigital,
                    ControlaStock = p.ControlaStock,
                    RequiereServicio = p.RequiereServicio,
                    VisibleEnCatalogo = p.VisibleEnCatalogo,
                    EsSuscripcion = p.EsSuscripcion,
                    DiasDuracion = p.DiasDuracion,
                    GarantiaDias = p.GarantiaDias,
                    Proveedor = p.Proveedor,
                    Estado = p.Estado,
                    CategoriaId = p.CategoriaId,
                    CategoriaNombre = p.Categoria?.Nombre,
                    JuegoId = p.JuegoId,
                    JuegoNombre = p.Juego?.Nombre,

                    TieneVariaciones = p.TieneVariaciones,
                    Variaciones = p.Variaciones.Select(v => new VariacionProductoDto
                    {
                        Id = v.Id,
                        ProductoPadreId = v.ProductoPadreId,
                        SKU = v.SKU,
                        Color = v.Color,
                        Almacenamiento = v.Almacenamiento,
                        RAM = v.RAM,
                        Talla = v.Talla,
                        NombreVariacion = v.NombreVariacion,
                        PrecioVenta = v.PrecioVenta,
                        PrecioCosto = v.PrecioCosto,
                        StockActual = v.StockActual,
                        StockMinimo = v.StockMinimo,
                        ImagenUrl = v.ImagenUrl,
                        Estado = v.Estado
                    }).ToList(),

                    StockActual = p.TieneVariaciones
                        ? p.Variaciones.Sum(v => v.StockActual)
                        : (p.EsSuscripcion
                            ? (stockPerfilesPool.TryGetValue(p.Id, out int sSusc) ? sSusc : 0)
                            : (p.EsCodigoDigital
                                ? (stockCodigosPool.TryGetValue(p.Id, out int sCod) ? sCod : 0)
                                : p.StockActual)),

                    PrimerPerfilId = p.EsSuscripcion
                        ? _context.PerfilesCuentas
                            .Where(pc => pc.IdProducto == p.Id && !pc.Ocupado && pc.EstadoPerfil == "Disponible")
                            .OrderBy(pc => pc.Id)
                            .Select(pc => (int?)pc.Id)
                            .FirstOrDefault()
                        : (p.EsCodigoDigital
                            ? _context.CodigosDigitales
                                .Where(c => c.IdProducto == p.Id && !c.Vendido && c.Estado == "Disponible")
                                .OrderBy(c => c.Id)
                                .Select(c => (int?)c.Id)
                                .FirstOrDefault()
                            : null),

                    MetadataDigital = p.EsSuscripcion
                        ? _context.PerfilesCuentas
                            .Where(pc => pc.IdProducto == p.Id && !pc.Ocupado && pc.EstadoPerfil == "Disponible")
                            .OrderBy(pc => pc.Id)
                            .Select(pc => $"Perfil: {pc.NombrePerfil} | Cuenta: {pc.CorreoCuenta} | Pass: {pc.PasswordCuenta} | PIN: {pc.PIN}")
                            .FirstOrDefault()
                        : (p.EsCodigoDigital
                            ? _context.CodigosDigitales
                                .Where(c => c.IdProducto == p.Id && !c.Vendido && c.Estado == "Disponible")
                                .OrderBy(c => c.Id)
                                .Select(c => $"CÓDIGO: {c.Clave}")
                                .FirstOrDefault()
                            : null)
                }).ToList();

                return Ok(respuesta);
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
                var producto = await _context.Productos.FindAsync(id);
                if (producto == null)
                {
                    return NotFound(new { mensaje = "El producto no existe." });
                }

                var idsIgnorados = string.IsNullOrWhiteSpace(ignorados)
                    ? new List<int>()
                    : ignorados.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => int.TryParse(s.Trim(), out int val) ? val : 0)
                            .Where(v => v > 0)
                            .ToList();

                // 1. CÓDIGO DIGITAL
                if (producto.EsCodigoDigital)
                {
                    var codigoDisponible = await _context.CodigosDigitales
                        .AsNoTracking()
                        .Where(c => c.IdProducto == id && !c.Vendido && c.Estado == "Disponible")
                        .Where(c => !idsIgnorados.Contains(c.Id))
                        .OrderBy(c => c.Id)
                        .Select(c => new
                        {
                            disponible = true,
                            idPerfil = c.Id,
                            metadataDigital = $"CÓDIGO: {c.Clave}"
                        })
                        .FirstOrDefaultAsync();

                    if (codigoDisponible == null)
                    {
                        return Ok(new { disponible = false, metadataDigital = (string?)null, idPerfil = 0 });
                    }

                    return Ok(codigoDisponible);
                }

                // 2. SUSCRIPCIÓN STREAMING
                if (producto.EsSuscripcion)
                {
                    var credencial = await _context.PerfilesCuentas
                        .AsNoTracking()
                        .Where(pc => pc.IdProducto == id && !pc.Ocupado && pc.EstadoPerfil == "Disponible")
                        .Where(pc => !idsIgnorados.Contains(pc.Id))
                        .OrderBy(pc => pc.Id)
                        .Select(pc => new
                        {
                            disponible = true,
                            idPerfil = pc.Id,
                            metadataDigital = $"Perfil: {pc.NombrePerfil} | Cuenta: {pc.CorreoCuenta} | Pass: {pc.PasswordCuenta} | PIN: {pc.PIN}"
                        })
                        .FirstOrDefaultAsync();

                    if (credencial == null)
                    {
                        return Ok(new { disponible = false, metadataDigital = (string?)null, idPerfil = 0 });
                    }

                    return Ok(credencial);
                }

                return Ok(new { disponible = false, metadataDigital = (string?)null, idPerfil = 0 });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar la siguiente credencial para el producto {Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al consultar la credencial." });
            }
        }

        // GET: api/Products/catalogo (Público)
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

                var stockCodigosPool = await _context.CodigosDigitales
                    .AsNoTracking()
                    .Where(c => !c.Vendido && c.Estado == "Disponible")
                    .GroupBy(c => c.IdProducto)
                    .Select(g => new { IdProducto = g.Key, TotalDisponibles = g.Count() })
                    .ToDictionaryAsync(x => x.IdProducto, x => x.TotalDisponibles);

                var productos = await _context.Productos
                    .AsNoTracking()
                    .Include(p => p.Categoria)
                    .Include(p => p.Juego)
                    .Include(p => p.Variaciones)
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
                    EsCodigoDigital = p.EsCodigoDigital,
                    EsSuscripcion = p.EsSuscripcion,
                    DiasDuracion = p.DiasDuracion,
                    VisibleEnCatalogo = p.VisibleEnCatalogo,
                    CategoriaNombre = p.Categoria?.Nombre,
                    JuegoNombre = p.Juego?.Nombre,
                    TieneVariaciones = p.TieneVariaciones,
                    Variaciones = p.TieneVariaciones && p.Variaciones != null
                        ? p.Variaciones
                            .Where(v => v.Estado == "Activo")
                            .Select(v => new VariacionProductoDto
                            {
                                Id = v.Id,
                                ProductoPadreId = v.ProductoPadreId,
                                SKU = v.SKU,
                                Color = v.Color,
                                Almacenamiento = v.Almacenamiento,
                                RAM = v.RAM,
                                Talla = v.Talla,
                                NombreVariacion = v.NombreVariacion,
                                PrecioVenta = v.PrecioVenta,
                                PrecioCosto = v.PrecioCosto,
                                StockActual = v.StockActual,
                                StockMinimo = v.StockMinimo,
                                ImagenUrl = v.ImagenUrl,
                                Estado = v.Estado
                            }).ToList()
                        : new List<VariacionProductoDto>(),
                    StockActual = p.TieneVariaciones
                        ? (p.Variaciones != null ? p.Variaciones.Sum(v => v.StockActual) : 0)
                        : (p.EsSuscripcion 
                            ? (stockPerfilesPool.TryGetValue(p.Id, out int stockSuscripcion) ? stockSuscripcion : 0)
                            : (p.EsCodigoDigital 
                                ? (stockCodigosPool.TryGetValue(p.Id, out int stockCodigos) ? stockCodigos : 0)
                                : p.StockActual))
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
                    .Include(p => p.Variaciones)
                    .Where(p => p.ControlaStock && !p.EsDigital && !p.RequiereServicio && 
                           (p.TieneVariaciones ? p.Variaciones.Any(v => v.StockActual <= v.StockMinimo) : p.StockActual <= p.StockMinimo))
                    .OrderBy(p => p.StockActual)
                    .Select(p => new ProductoAdminResponseDto
                    {
                        Id = p.Id,
                        Nombre = p.Nombre,
                        StockActual = p.TieneVariaciones ? p.Variaciones.Sum(v => v.StockActual) : p.StockActual,
                        StockMinimo = p.StockMinimo,
                        PrecioVenta = p.PrecioVenta,
                        Estado = p.Estado,
                        TieneVariaciones = p.TieneVariaciones
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
                    StockActual = (dto.EsDigital || dto.RequiereServicio || !dto.ControlaStock || dto.TieneVariaciones) ? 0 : dto.StockActual,
                    StockMinimo = (dto.EsDigital || dto.RequiereServicio || !dto.ControlaStock || dto.TieneVariaciones) ? 0 : dto.StockMinimo,
                    ImagenUrl = dto.ImagenUrl?.Trim() ?? string.Empty,
                    EsDigital = dto.EsDigital,
                    EsCodigoDigital = dto.EsCodigoDigital,
                    ControlaStock = dto.ControlaStock,
                    RequiereServicio = dto.RequiereServicio,
                    VisibleEnCatalogo = dto.VisibleEnCatalogo,
                    EsSuscripcion = dto.EsSuscripcion,
                    DiasDuracion = dto.DiasDuracion,
                    GarantiaDias = dto.GarantiaDias,
                    Proveedor = dto.Proveedor?.Trim() ?? string.Empty,
                    Estado = string.IsNullOrWhiteSpace(dto.Estado) ? "Activo" : dto.Estado.Trim(),
                    CategoriaId = dto.CategoriaId,
                    JuegoId = dto.JuegoId,
                    TieneVariaciones = dto.TieneVariaciones
                };

                if (dto.TieneVariaciones && dto.Variaciones != null && dto.Variaciones.Any())
                {
                    string nombrePrefijo = !string.IsNullOrEmpty(dto.Nombre) && dto.Nombre.Length >= 3 
                        ? dto.Nombre.Substring(0, 3).ToUpper() 
                        : (dto.Nombre?.ToUpper() ?? "PRD");

                    foreach (var vDto in dto.Variaciones)
                    {
                        string fallbackNombre = vDto.NombreVariacion ?? "Variación";
                        
                        producto.Variaciones.Add(new VariacionProducto
                        {
                            SKU = string.IsNullOrWhiteSpace(vDto.SKU) ? $"{nombrePrefijo}-{fallbackNombre}" : (vDto.SKU ?? string.Empty),
                            Color = vDto.Color ?? fallbackNombre,
                            Almacenamiento = vDto.Almacenamiento ?? string.Empty,
                            RAM = vDto.RAM ?? string.Empty,
                            Talla = vDto.Talla ?? string.Empty,
                            NombreVariacion = fallbackNombre,
                            PrecioVenta = vDto.PrecioVenta > 0 ? vDto.PrecioVenta : dto.PrecioVenta,
                            PrecioCosto = vDto.PrecioCosto > 0 ? vDto.PrecioCosto : dto.PrecioCosto,
                            StockActual = vDto.StockActual,
                            StockMinimo = vDto.StockMinimo > 0 ? vDto.StockMinimo : 2,
                            ImagenUrl = string.IsNullOrWhiteSpace(vDto.ImagenUrl) ? (dto.ImagenUrl ?? string.Empty) : (vDto.ImagenUrl ?? string.Empty),
                            Estado = "Activo"
                        });
                    }
                }

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
                var productoExistente = await _context.Productos
                    .Include(p => p.Variaciones)
                    .FirstOrDefaultAsync(p => p.Id == id);

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
                productoExistente.EsCodigoDigital = dto.EsCodigoDigital;
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
                productoExistente.TieneVariaciones = dto.TieneVariaciones;

                if (dto.EsDigital || dto.RequiereServicio || !dto.ControlaStock || dto.TieneVariaciones)
                {
                    productoExistente.StockMinimo = 0;
                    productoExistente.StockActual = 0;
                }
                else
                {
                    productoExistente.StockActual = dto.StockActual;
                    productoExistente.StockMinimo = dto.StockMinimo;
                }

                if (dto.TieneVariaciones && dto.Variaciones != null)
                {
                    var idsNuevos = dto.Variaciones.Where(v => v.Id > 0).Select(v => v.Id).ToList();
                    var variacionesAEliminar = productoExistente.Variaciones.Where(v => !idsNuevos.Contains(v.Id)).ToList();
                    
                    _context.VariacionesProductos.RemoveRange(variacionesAEliminar);

                    string nombrePrefijoUpdate = !string.IsNullOrEmpty(dto.Nombre) && dto.Nombre.Length >= 3 
                        ? dto.Nombre.Substring(0, 3).ToUpper() 
                        : (dto.Nombre?.ToUpper() ?? "PRD");

                    foreach (var vDto in dto.Variaciones)
                    {
                        string fallbackNombre = vDto.NombreVariacion ?? "Variación";

                        if (vDto.Id > 0)
                        {
                            var vExistente = productoExistente.Variaciones.FirstOrDefault(v => v.Id == vDto.Id);
                            if (vExistente != null)
                            {
                                vExistente.NombreVariacion = fallbackNombre;
                                vExistente.Color = vDto.Color ?? fallbackNombre;
                                vExistente.Almacenamiento = vDto.Almacenamiento ?? string.Empty;
                                vExistente.RAM = vDto.RAM ?? string.Empty;
                                vExistente.PrecioVenta = vDto.PrecioVenta;
                                vExistente.PrecioCosto = vDto.PrecioCosto;
                                vExistente.StockActual = vDto.StockActual;
                            }
                        }
                        else
                        {
                            productoExistente.Variaciones.Add(new VariacionProducto
                            {
                                SKU = string.IsNullOrWhiteSpace(vDto.SKU) ? $"{nombrePrefijoUpdate}-{fallbackNombre}" : (vDto.SKU ?? string.Empty),
                                NombreVariacion = fallbackNombre,
                                Color = vDto.Color ?? fallbackNombre,
                                Almacenamiento = vDto.Almacenamiento ?? string.Empty,
                                RAM = vDto.RAM ?? string.Empty,
                                PrecioVenta = vDto.PrecioVenta,
                                PrecioCosto = vDto.PrecioCosto,
                                StockActual = vDto.StockActual,
                                Estado = "Activo"
                            });
                        }
                    }
                }
                else if (!dto.TieneVariaciones && productoExistente.Variaciones.Any())
                {
                    _context.VariacionesProductos.RemoveRange(productoExistente.Variaciones);
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

        // 6. PUT: api/Products/5/variaciones-stock
        [HttpPut("{id}/variaciones-stock")]
        [Authorize]
        public async Task<IActionResult> UpdateStockVariaciones(int id, [FromBody] List<VariacionProductoDto> variacionesDto)
        {
            try
            {
                foreach (var vDto in variacionesDto)
                {
                    var variacionDb = await _context.VariacionesProductos.FindAsync(vDto.Id);
                    if (variacionDb != null)
                    {
                        variacionDb.StockActual = vDto.StockActual;
                    }
                }

                await _context.SaveChangesAsync();
                return Ok(new { mensaje = "Stock de variaciones actualizado correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar stock de variaciones del producto ID {Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al actualizar unidades." });
            }
        }

        // 7. DELETE: api/Products/5
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
                var tieneCompras = await _context.DetallesComprasProveedores.AnyAsync(d => d.IdProducto == id);

                if (tieneVentas || tieneSuscripciones || tieneCompras)
                {
                    producto.VisibleEnCatalogo = false;
                    producto.Estado = "Pausado";
                    await _context.SaveChangesAsync();

                    return Ok(new
                    {
                        mensaje = "El producto posee transacciones o compras asociadas. Se ha pausado y ocultado del catálogo público."
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

        // GET: api/Products/5/historial-ventas
        [HttpGet("{id}/historial-ventas")]
        [Authorize]
        public async Task<IActionResult> GetHistorialVentasProducto(int id)
        {
            try
            {
                var productoExiste = await _context.Productos.AnyAsync(p => p.Id == id);
                if (!productoExiste)
                {
                    return NotFound(new { mensaje = "El producto no existe." });
                }

                var historial = await _context.DetallesVentas
                    .AsNoTracking()
                    .Where(d => d.IdProducto == id && d.Venta != null)
                    .OrderByDescending(d => d.Venta!.FechaVenta)
                    .Select(d => new
                    {
                        VentaId = d.IdVenta,
                        Fecha = d.Venta!.FechaVenta.ToString("yyyy-MM-dd HH:mm"),
                        ClienteId = d.Venta.IdCliente,
                        ClienteNombre = d.Venta.Cliente != null ? d.Venta.Cliente.Nombre : "Mostrador General",
                        ClienteTelefono = d.Venta.Cliente != null ? d.Venta.Cliente.Telefono : "N/A",
                        Cantidad = d.Cantidad,
                        PrecioUnitario = d.PrecioUnitario,
                        SubTotal = d.SubTotal,
                        MetodoPago = d.Venta.MetodoPago,
                        Operador = d.Venta.Usuario != null ? d.Venta.Usuario.Nombre : "Sistema"
                    })
                    .ToListAsync();

                return Ok(historial);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener historial de ventas del producto {Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al consultar el historial." });
            }
        }
    }
}