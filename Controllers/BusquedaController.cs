using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BusquedaController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BusquedaController> _logger;

        public BusquedaController(
            ApplicationDbContext context,
            ILogger<BusquedaController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("universal")]
        public async Task<IActionResult> Buscar([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(new { mensaje = "El término de búsqueda no puede estar vacío." });
            }

            try
            {
                var queryLimpia = query.Trim().ToLower();

                // 1. Búsqueda de Clientes (Optimizada en 1 sola consulta SQL usando proyecciones y paginado límite)
                var clientes = await _context.Clientes
                    .AsNoTracking()
                    .Where(c => c.Nombre.ToLower().Contains(queryLimpia) || c.Telefono.Contains(queryLimpia))
                    .Take(25) // Evitamos sobrecargas limitando a 25 coincidencias
                    .Select(c => new
                    {
                        Tipo = "Cliente",
                        c.Id,
                        c.Nombre,
                        c.Telefono,
                        HistorialCompras = _context.Ventas
                            .Where(v => v.IdCliente == c.Id)
                            .OrderByDescending(v => v.FechaVenta)
                            .Take(10) // Traemos solo las 10 compras más recientes
                            .Select(v => new { v.Id, v.FechaVenta, v.Total })
                            .ToList(),
                        ServiciosActivos = _context.Suscripciones
                            .Where(s => s.IdCliente == c.Id && s.Estado == "Activa")
                            .Select(s => new { s.Id, s.FechaInicio, s.FechaVencimiento, s.Estado })
                            .ToList()
                    })
                    .ToListAsync();

                // 2. Búsqueda de Cuentas / Perfiles
                var perfilesCuentas = await _context.PerfilesCuentas
                    .AsNoTracking()
                    .Where(p => (p.Producto != null && p.Producto.Nombre.ToLower().Contains(queryLimpia)) 
                             || p.CorreoCuenta.ToLower().Contains(queryLimpia))
                    .Take(25)
                    .Select(p => new
                    {
                        Tipo = "Cuenta/Perfil",
                        Servicio = p.Producto != null ? p.Producto.Nombre : "Sin producto asignado",
                        p.NombrePerfil,
                        p.CorreoCuenta,
                        p.Ocupado,
                        Clave = p.PasswordCuenta,
                        p.PIN
                    })
                    .ToListAsync();

                return Ok(new
                {
                    totalResultados = clientes.Count + perfilesCuentas.Count,
                    clientes,
                    cuentas = perfilesCuentas
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al realizar la búsqueda universal con el término '{Query}'", query);
                return StatusCode(500, new 
                { 
                    mensaje = "Error interno al procesar la búsqueda universal.",
                    detalles = "Intente refinando el término de búsqueda." 
                });
            }
        }
    }
}