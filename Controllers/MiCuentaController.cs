using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.DTOs;
using System.Security.Claims;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Requiere token JWT activo de cliente
    public class MiCuentaController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MiCuentaController> _logger;

        public MiCuentaController(
            ApplicationDbContext context,
            ILogger<MiCuentaController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Helper privado defensivo para extraer de forma segura el ID del cliente autenticado.
        /// </summary>
        private bool TryObtenerIdCliente(out int idCliente, out IActionResult? errorResult)
        {
            idCliente = 0;
            errorResult = null;

            var tipoClaim = User.FindFirst("TipoUsuario")?.Value;

            if (!string.Equals(tipoClaim, "Cliente", StringComparison.OrdinalIgnoreCase))
            {
                errorResult = StatusCode(403, new { mensaje = "El recurso es exclusivo para clientes registrados." });
                return false;
            }

            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(idClaim) || !int.TryParse(idClaim, out idCliente))
            {
                errorResult = Unauthorized(new { mensaje = "Token de autenticación inválido o desactualizado." });
                return false;
            }

            return true;
        }

        // GET: api/MiCuenta/perfil
        [HttpGet("perfil")]
        public async Task<IActionResult> Perfil()
        {
            if (!TryObtenerIdCliente(out int idCliente, out var error)) return error!;

            try
            {
                var cliente = await _context.Clientes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == idCliente);

                if (cliente == null)
                {
                    return NotFound(new { mensaje = "No se encontró el perfil de usuario solicitado." });
                }

                var response = new PerfilClienteResponseDto
                {
                    Id = cliente.Id,
                    Nombre = cliente.Nombre,
                    Telefono = cliente.Telefono ?? string.Empty,
                    Email = cliente.Email ?? string.Empty,
                    FechaRegistro = cliente.FechaRegistro,
                    PuntosAcumulados = cliente.PuntosAcumulados,
                    Etiquetas = cliente.Etiquetas ?? string.Empty
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener el perfil del cliente con ID {Id}", idCliente);
                return StatusCode(500, new { mensaje = "Error interno al consultar el perfil de usuario." });
            }
        }

        // GET: api/MiCuenta/mis-compras
        [HttpGet("mis-compras")]
        public async Task<IActionResult> MisCompras()
        {
            if (!TryObtenerIdCliente(out int idCliente, out var error)) return error!;

            try
            {
                var compras = await _context.Ventas
                    .AsNoTracking()
                    .Where(v => v.IdCliente == idCliente)
                    .OrderByDescending(v => v.FechaVenta)
                    .Select(v => new CompraClienteResponseDto
                    {
                        Id = v.Id,
                        FechaVenta = v.FechaVenta,
                        Total = v.Total,
                        MetodoPago = v.MetodoPago,
                        Productos = v.Detalles.Select(d => new DetalleCompraDto
                        {
                            IdProducto = d.IdProducto,
                            NombreProducto = d.Producto != null ? d.Producto.Nombre : "Producto Genérico",
                            Cantidad = d.Cantidad,
                            PrecioUnitario = d.PrecioUnitario,
                            SubTotal = d.SubTotal
                        }).ToList()
                    })
                    .ToListAsync();

                return Ok(compras);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener el historial de compras del cliente con ID {Id}", idCliente);
                return StatusCode(500, new { mensaje = "Error interno al consultar el historial de compras." });
            }
        }

        // GET: api/MiCuenta/mis-suscripciones
        [HttpGet("mis-suscripciones")]
        public async Task<IActionResult> MisSuscripciones()
        {
            if (!TryObtenerIdCliente(out int idCliente, out var error)) return error!;

            try
            {
                var suscripciones = await _context.Suscripciones
                    .AsNoTracking()
                    .Where(s => s.IdCliente == idCliente)
                    .OrderByDescending(s => s.FechaVencimiento)
                    .Select(s => new SuscripcionClienteResponseDto
                    {
                        Id = s.Id,
                        NombreServicio = s.NombreServicio,
                        TipoSuscripcion = s.TipoSuscripcion,
                        FechaInicio = s.FechaInicio,
                        FechaVencimiento = s.FechaVencimiento,
                        Estado = s.Estado,
                        CostoRenovacion = s.CostoRenovacion,
                        NombreProducto = s.Producto != null ? s.Producto.Nombre : null,
                        Perfil = s.PerfilCuenta == null ? null : new PerfilCuentaAsignadaDto
                        {
                            NombrePerfil = s.PerfilCuenta.NombrePerfil,
                            PIN = s.PerfilCuenta.PIN ?? string.Empty,
                            CorreoCuenta = s.PerfilCuenta.CorreoCuenta ?? string.Empty,
                            PasswordCuenta = s.PerfilCuenta.PasswordCuenta ?? string.Empty
                        }
                    })
                    .ToListAsync();

                return Ok(suscripciones);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar las suscripciones del cliente con ID {Id}", idCliente);
                return StatusCode(500, new { mensaje = "Error interno al obtener las suscripciones del usuario." });
            }
        }

        // GET: api/MiCuenta/dashboard
        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            if (!TryObtenerIdCliente(out int idCliente, out var error)) return error!;

            try
            {
                var cliente = await _context.Clientes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == idCliente);

                if (cliente == null)
                {
                    return NotFound(new { mensaje = "No se encontró la cuenta de cliente." });
                }

                var totalCompras = await _context.Ventas
                    .AsNoTracking()
                    .Where(v => v.IdCliente == idCliente)
                    .CountAsync();

                var suscripcionesActivas = await _context.Suscripciones
                    .AsNoTracking()
                    .Where(s => s.IdCliente == idCliente && s.Estado == "Activa")
                    .CountAsync();

                var ticketsAbiertos = await _context.TicketsSoporte
                    .AsNoTracking()
                    .Where(t => t.IdCliente == idCliente && t.Estado != "Cerrado")
                    .CountAsync();

                var garantiasActivas = await _context.GarantiasTickets
                    .AsNoTracking()
                    .Where(g => g.IdCliente == idCliente && g.Estado != "Finalizada")
                    .CountAsync();

                var proximaRenovacion = await _context.Suscripciones
                    .AsNoTracking()
                    .Where(s => s.IdCliente == idCliente && s.Estado == "Activa")
                    .OrderBy(s => s.FechaVencimiento)
                    .Select(s => new ProximaRenovacionDto
                    {
                        Id = s.Id,
                        NombreServicio = s.NombreServicio,
                        FechaVencimiento = s.FechaVencimiento,
                        CostoRenovacion = s.CostoRenovacion
                    })
                    .FirstOrDefaultAsync();

                var ultimasCompras = await _context.Ventas
                    .AsNoTracking()
                    .Where(v => v.IdCliente == idCliente)
                    .OrderByDescending(v => v.FechaVenta)
                    .Take(5)
                    .Select(v => new ResumenCompraDto
                    {
                        Id = v.Id,
                        FechaVenta = v.FechaVenta,
                        Total = v.Total
                    })
                    .ToListAsync();

                var dashboard = new DashboardClienteResponseDto
                {
                    NombreCliente = cliente.Nombre,
                    EmailCliente = cliente.Email ?? string.Empty,
                    PuntosAcumulados = cliente.PuntosAcumulados,
                    TotalCompras = totalCompras,
                    SuscripcionesActivas = suscripcionesActivas,
                    TicketsAbiertos = ticketsAbiertos,
                    GarantiasActivas = garantiasActivas,
                    ProximaRenovacion = proximaRenovacion,
                    UltimasCompras = ultimasCompras
                };

                return Ok(dashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar el dashboard para el cliente con ID {Id}", idCliente);
                return StatusCode(500, new { mensaje = "Error interno al obtener los datos del panel principal." });
            }
        }
    }
}