using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using System.Security.Claims;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/micuenta")]
    [Authorize]
    public class MiCuentaController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public MiCuentaController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int ObtenerIdCliente()
        {
            var tipo = User.FindFirst("TipoUsuario")?.Value;

            if (tipo != "Cliente")
                throw new UnauthorizedAccessException();

            return int.Parse(
                User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        }

        [HttpGet("perfil")]
        public async Task<IActionResult> Perfil()
        {
            int idCliente = ObtenerIdCliente();

            var cliente = await _context.Clientes
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == idCliente);

            if (cliente == null)
                return NotFound();

            return Ok(new
            {
                cliente.Id,
                cliente.Nombre,
                cliente.Telefono,
                cliente.Email,
                cliente.FechaRegistro,
                cliente.PuntosAcumulados,
                cliente.Etiquetas
            });
        }

        [HttpGet("mis-compras")]
        public async Task<IActionResult> MisCompras()
        {
            int idCliente = ObtenerIdCliente();

            var compras = await _context.Ventas
                .Where(v => v.IdCliente == idCliente)
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.Producto)
                .OrderByDescending(v => v.FechaVenta)
                .Select(v => new
                {
                    v.Id,
                    v.FechaVenta,
                    v.Total,
                    v.MetodoPago,

                    Productos = v.Detalles.Select(d => new
                    {
                        d.IdProducto,
                        Nombre = d.Producto.Nombre,
                        d.Cantidad,
                        d.PrecioUnitario,
                        d.SubTotal
                    })
                })
                .ToListAsync();

            return Ok(compras);
        }

        [HttpGet("mis-suscripciones")]
        public async Task<IActionResult> MisSuscripciones()
        {
            int idCliente = ObtenerIdCliente();

            var suscripciones = await _context.Suscripciones
                .Where(s => s.IdCliente == idCliente)
                .Include(s => s.Producto)
                .Include(s => s.PerfilCuenta)
                .OrderByDescending(s => s.FechaVencimiento)
                .Select(s => new
                {
                    s.Id,
                    s.NombreServicio,
                    s.TipoSuscripcion,
                    s.FechaInicio,
                    s.FechaVencimiento,
                    s.Estado,
                    s.CostoRenovacion,

                    Producto = s.Producto == null
                        ? null
                        : s.Producto.Nombre,

                    Perfil = s.PerfilCuenta == null
                        ? null
                        : new
                        {
                            s.PerfilCuenta.NombrePerfil,
                            s.PerfilCuenta.PIN,
                            s.PerfilCuenta.CorreoCuenta,
                            s.PerfilCuenta.PasswordCuenta
                        }
                })
                .ToListAsync();

            return Ok(suscripciones);
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            int idCliente = ObtenerIdCliente();

            var cliente = await _context.Clientes
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == idCliente);

            if (cliente == null)
                return NotFound();

            var compras = await _context.Ventas
                .Where(v => v.IdCliente == idCliente)
                .CountAsync();

            var suscripcionesActivas = await _context.Suscripciones
                .Where(s =>
                    s.IdCliente == idCliente &&
                    s.Estado == "Activa")
                .CountAsync();

            var tickets = await _context.TicketsSoporte
                .Where(t =>
                    t.IdCliente == idCliente &&
                    t.Estado != "Cerrado")
                .CountAsync();

            var garantias = await _context.GarantiasTickets
                .Where(g =>
                    g.IdCliente == idCliente &&
                    g.Estado != "Finalizada")
                .CountAsync();

            var proximaRenovacion = await _context.Suscripciones
                .Where(s =>
                    s.IdCliente == idCliente &&
                    s.Estado == "Activa")
                .OrderBy(s => s.FechaVencimiento)
                .Select(s => new
                {
                    s.Id,
                    s.NombreServicio,
                    s.FechaVencimiento,
                    s.CostoRenovacion
                })
                .FirstOrDefaultAsync();

            var ultimasCompras = await _context.Ventas
                .Where(v => v.IdCliente == idCliente)
                .OrderByDescending(v => v.FechaVenta)
                .Take(5)
                .Select(v => new
                {
                    v.Id,
                    v.FechaVenta,
                    v.Total
                })
                .ToListAsync();

            return Ok(new
            {
                Cliente = new
                {
                    cliente.Nombre,
                    cliente.Email,
                    cliente.PuntosAcumulados
                },

                Estadisticas = new
                {
                    Compras = compras,
                    SuscripcionesActivas = suscripcionesActivas,
                    TicketsAbiertos = tickets,
                    GarantiasActivas = garantias
                },

                ProximaRenovacion = proximaRenovacion,

                UltimasCompras = ultimasCompras
            });
        }
    }
}