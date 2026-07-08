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
    public class TicketsSoporteController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        
        // Zona horaria estándar para Nicaragua
        private static readonly TimeZoneInfo NicaraguaZone = TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");

        public TicketsSoporteController(ApplicationDbContext context)
        {
            _context = context;
        }

        private DateTime GetNicaraguaTime()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, NicaraguaZone);
        }

        // GET: api/TicketsSoporte
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> Get()
        {
            return Ok(await _context.TicketsSoporte
                .Include(t => t.Cliente)
                .OrderByDescending(t => t.FechaCreacion)
                .Select(t => new {
                    t.Id,
                    t.IdCliente,
                    t.TipoTicket,
                    t.DescripcionFalla,
                    t.Estado,
                    t.FechaCreacion,
                    t.FechaResolucion,
                    t.NotasResolucion,
                    ClienteNombre = t.Cliente != null ? t.Cliente.Nombre : "Genérico",
                    ClienteTelefono = t.Cliente != null ? t.Cliente.Telefono : "Sin número"
                })
                .ToListAsync());
        }

        // POST: api/TicketsSoporte
        [HttpPost]
        public async Task<ActionResult<TicketSoporte>> Post([FromBody] TicketSoporte ticket)
        {
            // Corregido: Fecha de apertura en tiempo real local
            ticket.FechaCreacion = GetNicaraguaTime();
            ticket.Estado = "Pendiente";

            _context.TicketsSoporte.Add(ticket);
            await _context.SaveChangesAsync();

            return Ok(ticket);
        }

        // PUT: api/TicketsSoporte/{id}/estado
        [HttpPut("{id}/estado")]
        public async Task<IActionResult> ActualizarEstado(int id, [FromQuery] string nuevoEstado, [FromBody] string notas)
        {
            var ticket = await _context.TicketsSoporte.FindAsync(id);
            if (ticket == null) return NotFound("El ticket no existe.");

            var estadosValidos = new[] { "Pendiente", "En proceso", "Esperando proveedor", "Resuelto" };
            if (!estadosValidos.Contains(nuevoEstado)) return BadRequest("Estado no válido.");

            ticket.Estado = nuevoEstado;
            ticket.NotasResolucion = notas;

            // Corregido: Cierre de ticket bajo el mismo huso horario local
            if (nuevoEstado == "Resuelto")
            {
                ticket.FechaResolucion = GetNicaraguaTime();
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}