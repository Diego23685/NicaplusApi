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
    [Authorize] // Protegemos la gestión de tickets y reclamos de clientes
    public class GarantiasTicketsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<GarantiasTicketsController> _logger;

        public GarantiasTicketsController(
            ApplicationDbContext context,
            ILogger<GarantiasTicketsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private static TimeZoneInfo GetNicaraguaTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/Managua");
            }
        }

        private DateTime GetNicaraguaTime()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, GetNicaraguaTimeZone());
        }

        // GET: api/GarantiasTickets
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                var tickets = await _context.GarantiasTickets
                    .AsNoTracking()
                    .Include(g => g.Cliente)
                    .Include(g => g.Responsable)
                    .OrderByDescending(g => g.FechaRepo)
                    .Select(g => new GarantiaTicketResponseDto
                    {
                        Id = g.Id,
                        IdCliente = g.IdCliente,
                        ClienteNombre = g.Cliente != null ? g.Cliente.Nombre : "Cliente Genérico",
                        ClienteTelefono = g.Cliente != null ? g.Cliente.Telefono : string.Empty,
                        IdUsuarioResponsable = g.IdUsuarioResponsable,
                        ResponsableNombre = g.Responsable != null ? g.Responsable.Nombre : "Sistema / Admin",
                        IdProducto = g.IdProducto,
                        Motivo = g.Motivo,
                        CuentaAnterior = g.CuentaAnterior,
                        CuentaNueva = g.CuentaNueva,
                        CostoReposicion = g.CostoReposicion,
                        FechaRepo = g.FechaRepo,
                        Estado = g.Estado
                    })
                    .ToListAsync();

                return Ok(tickets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener el listado de tickets de garantía.");
                return StatusCode(500, new { mensaje = "Error interno al consultar los tickets de garantía." });
            }
        }

        // GET: api/GarantiasTickets/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var ticket = await _context.GarantiasTickets
                    .AsNoTracking()
                    .Include(g => g.Cliente)
                    .Include(g => g.Responsable)
                    .FirstOrDefaultAsync(g => g.Id == id);

                if (ticket == null)
                {
                    return NotFound(new { mensaje = "El ticket de garantía no fue encontrado." });
                }

                var response = new GarantiaTicketResponseDto
                {
                    Id = ticket.Id,
                    IdCliente = ticket.IdCliente,
                    ClienteNombre = ticket.Cliente != null ? ticket.Cliente.Nombre : "Cliente Genérico",
                    ClienteTelefono = ticket.Cliente != null ? ticket.Cliente.Telefono : string.Empty,
                    IdUsuarioResponsable = ticket.IdUsuarioResponsable,
                    ResponsableNombre = ticket.Responsable != null ? ticket.Responsable.Nombre : "Sistema / Admin",
                    IdProducto = ticket.IdProducto,
                    Motivo = ticket.Motivo,
                    CuentaAnterior = ticket.CuentaAnterior,
                    CuentaNueva = ticket.CuentaNueva,
                    CostoReposicion = ticket.CostoReposicion,
                    FechaRepo = ticket.FechaRepo,
                    Estado = ticket.Estado
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener el ticket de garantía con ID {Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al consultar el ticket." });
            }
        }

        // POST: api/GarantiasTickets
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CrearGarantiaTicketDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { mensaje = "Datos del ticket incompletos o inválidos.", detalles = ModelState });
            }

            var ahoraNicaragua = GetNicaraguaTime();
            DateTime fechaFinal = dto.FechaRepo.HasValue && dto.FechaRepo.Value != default
                ? dto.FechaRepo.Value
                : ahoraNicaragua;

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var cliente = await _context.Clientes.FindAsync(dto.IdCliente);
                if (cliente == null)
                {
                    return BadRequest(new { mensaje = "El cliente especificado no existe." });
                }

                var responsable = await _context.Usuarios.FindAsync(dto.IdUsuarioResponsable);
                if (responsable == null)
                {
                    return BadRequest(new { mensaje = "El usuario responsable especificado no existe." });
                }

                var garantia = new GarantiaTicket
                {
                    IdCliente = dto.IdCliente,
                    IdUsuarioResponsable = dto.IdUsuarioResponsable,
                    IdProducto = dto.IdProducto,
                    Motivo = dto.Motivo.Trim(),
                    CuentaAnterior = dto.CuentaAnterior.Trim(),
                    CuentaNueva = dto.CuentaNueva.Trim(),
                    CostoReposicion = dto.CostoReposicion,
                    FechaRepo = fechaFinal,
                    Estado = string.IsNullOrWhiteSpace(dto.Estado) ? "Pendiente" : dto.Estado.Trim()
                };

                _context.GarantiasTickets.Add(garantia);
                await _context.SaveChangesAsync();

                // Si la reposición implicó un costo para el negocio, se registra la pérdida/egreso directo en Caja
                if (garantia.CostoReposicion > 0)
                {
                    var movimientoCaja = new MovimientoCaja
                    {
                        Fecha = ahoraNicaragua,
                        Tipo = "Egreso",
                        Monto = garantia.CostoReposicion,
                        Concepto = "Gasto Ordinario", // Se descuenta en las métricas de utilidad operativa
                        Detalle = $"Pérdida por Garantía Ticket ID: {garantia.Id} | Cliente: {cliente.Nombre} | Motivo: {garantia.Motivo}"
                    };

                    _context.MovimientosCaja.Add(movimientoCaja);
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                var response = new GarantiaTicketResponseDto
                {
                    Id = garantia.Id,
                    IdCliente = garantia.IdCliente,
                    ClienteNombre = cliente.Nombre,
                    ClienteTelefono = cliente.Telefono,
                    IdUsuarioResponsable = garantia.IdUsuarioResponsable,
                    ResponsableNombre = responsable.Nombre,
                    IdProducto = garantia.IdProducto,
                    Motivo = garantia.Motivo,
                    CuentaAnterior = garantia.CuentaAnterior,
                    CuentaNueva = garantia.CuentaNueva,
                    CostoReposicion = garantia.CostoReposicion,
                    FechaRepo = garantia.FechaRepo,
                    Estado = garantia.Estado
                };

                return CreatedAtAction(nameof(GetById), new { id = garantia.Id }, new
                {
                    mensaje = "Ticket de garantía procesado y registrado correctamente.",
                    ticket = response
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al procesar el ticket de garantía.");
                return StatusCode(500, new { mensaje = "Error interno al procesar el ticket de garantía." });
            }
        }
    }
}