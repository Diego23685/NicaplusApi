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
    public class TicketsSoporteController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<TicketsSoporteController> _logger;

        public TicketsSoporteController(
            ApplicationDbContext context,
            ILogger<TicketsSoporteController> logger)
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

        // GET: api/TicketsSoporte
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                var tickets = await _context.TicketsSoporte
                    .AsNoTracking()
                    .Include(t => t.Cliente)
                    .OrderByDescending(t => t.FechaCreacion)
                    .Select(t => new TicketSoporteResponseDto
                    {
                        Id = t.Id,
                        IdCliente = t.IdCliente,
                        TipoTicket = t.TipoTicket,
                        DescripcionFalla = t.DescripcionFalla,
                        Estado = t.Estado,
                        FechaCreacion = t.FechaCreacion,
                        FechaResolucion = t.FechaResolucion,
                        NotasResolucion = t.NotasResolucion,
                        ClienteNombre = t.Cliente != null ? t.Cliente.Nombre : "Genérico",
                        ClienteTelefono = t.Cliente != null ? t.Cliente.Telefono : "Sin número"
                    })
                    .ToListAsync();

                return Ok(tickets);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al listar los tickets de soporte.");
                return StatusCode(500, new { mensaje = "Error interno al obtener el historial de tickets." });
            }
        }

        // GET: api/TicketsSoporte/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var ticket = await _context.TicketsSoporte
                    .AsNoTracking()
                    .Include(t => t.Cliente)
                    .Where(t => t.Id == id)
                    .Select(t => new TicketSoporteResponseDto
                    {
                        Id = t.Id,
                        IdCliente = t.IdCliente,
                        TipoTicket = t.TipoTicket,
                        DescripcionFalla = t.DescripcionFalla,
                        Estado = t.Estado,
                        FechaCreacion = t.FechaCreacion,
                        FechaResolucion = t.FechaResolucion,
                        NotasResolucion = t.NotasResolucion,
                        ClienteNombre = t.Cliente != null ? t.Cliente.Nombre : "Genérico",
                        ClienteTelefono = t.Cliente != null ? t.Cliente.Telefono : "Sin número"
                    })
                    .FirstOrDefaultAsync();

                if (ticket == null)
                    return NotFound(new { mensaje = "El ticket de soporte no existe." });

                return Ok(ticket);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener el ticket #{Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al consultar el ticket." });
            }
        }

        // POST: api/TicketsSoporte
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CrearTicketDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var clienteExiste = await _context.Clientes.AnyAsync(c => c.Id == dto.IdCliente);
                if (!clienteExiste)
                    return BadRequest(new { mensaje = "El cliente especificado no existe." });

                var nuevoTicket = new TicketSoporte
                {
                    IdCliente = dto.IdCliente,
                    TipoTicket = dto.TipoTicket.Trim(),
                    DescripcionFalla = dto.DescripcionFalla.Trim(),
                    Estado = "Pendiente",
                    FechaCreacion = GetNicaraguaTime()
                };

                _context.TicketsSoporte.Add(nuevoTicket);
                await _context.SaveChangesAsync();

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = nuevoTicket.Id },
                    new
                    {
                        mensaje = "Ticket de soporte registrado correctamente.",
                        idTicket = nuevoTicket.Id
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registrando ticket de soporte para el cliente {IdCliente}", dto.IdCliente);
                return StatusCode(500, new { mensaje = "Error interno al procesar el ticket de soporte." });
            }
        }

        // PUT: api/TicketsSoporte/5/estado
        [HttpPut("{id}/estado")]
        public async Task<IActionResult> ActualizarEstado(int id, [FromBody] CambiarEstadoTicketDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var ticket = await _context.TicketsSoporte.FindAsync(id);
                if (ticket == null)
                    return NotFound(new { mensaje = "El ticket de soporte no existe." });

                var estadosValidos = new[] { "Pendiente", "En proceso", "Esperando proveedor", "Resuelto", "Cancelado" };
                if (!estadosValidos.Contains(dto.NuevoEstado, StringComparer.OrdinalIgnoreCase))
                    return BadRequest(new { mensaje = $"Estado no válido. Los estados permitidos son: {string.Join(", ", estadosValidos)}" });

                ticket.Estado = dto.NuevoEstado.Trim();
                ticket.NotasResolucion = dto.NotasResolucion?.Trim() ?? string.Empty;

                if (string.Equals(ticket.Estado, "Resuelto", StringComparison.OrdinalIgnoreCase))
                {
                    ticket.FechaResolucion = GetNicaraguaTime();
                }
                else
                {
                    ticket.FechaResolucion = null; // Re-apertura limpia si cambia a otro estado
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    mensaje = $"Ticket #{id} actualizado a estado '{ticket.Estado}'.",
                    fechaResolucion = ticket.FechaResolucion
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error actualizando el estado del ticket #{Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al actualizar el estado del ticket." });
            }
        }
    }
}