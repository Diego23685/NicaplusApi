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
    public class SuscripcionesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SuscripcionesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/suscripciones
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Suscripcion>>> Get()
        {
            return await _context.Suscripciones
                .Include(s => s.Cliente)
                .Include(s => s.Producto)
                .OrderByDescending(s => s.FechaVencimiento)
                .ToListAsync();
        }

        // GET: api/suscripciones/alertas (Endpoint de Control CRM de Tiempos)
        [HttpGet("alertas")]
        public async Task<IActionResult> GetAlertasRenovacion()
        {
            var hoy = DateTime.Now;
            var suscripciones = await _context.Suscripciones
                .Include(s => s.Cliente)
                .Where(s => s.Estado != "Cancelada")
                .ToListAsync();

            bool huboCambios = false;

            var listaConAlertas = suscripciones.Select(s =>
            {
                TimeSpan diferencia = s.FechaVencimiento.Date - hoy.Date;
                int diasRestantes = diferencia.Days;

                string alertaFiltro = "Normal";
                
                // Lógica de automatización de estados y marcas de vencimiento
                if (diasRestantes < 0)
                {
                    alertaFiltro = "Vencido";
                    if (s.Estado == "Activa")
                    {
                        s.Estado = "Vencida";
                        _context.Entry(s).State = EntityState.Modified;
                        huboCambios = true;
                    }
                }
                else if (diasRestantes == 0) alertaFiltro = "Hoy";
                else if (diasRestantes == 1) alertaFiltro = "1 Dia";
                else if (diasRestantes <= 3) alertaFiltro = "3 Dias";
                else if (diasRestantes <= 7) alertaFiltro = "7 Dias";

                return new
                {
                    s.Id,
                    s.NombreServicio,
                    s.FechaInicio,
                    s.FechaVencimiento,
                    s.CostoRenovacion,
                    s.Estado,
                    s.DetallesCredenciales,
                    DiasRestantes = diasRestantes,
                    AlertaFiltro = alertaFiltro,
                    Cliente = s.Cliente != null ? new { s.Cliente.Nombre, s.Cliente.Telefono } : null
                };
            }).OrderBy(x => x.DiasRestantes).ToList();

            if (huboCambios)
            {
                await _context.SaveChangesAsync();
            }

            return Ok(listaConAlertas);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Suscripcion>> GetById(int id)
        {
            var suscripcion = await _context.Suscripciones
                .Include(s => s.Cliente)
                .Include(s => s.Producto)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (suscripcion == null) return NotFound("Suscripción no encontrada.");
            return Ok(suscripcion);
        }

        [HttpPost]
        public async Task<ActionResult<Suscripcion>> Post([FromBody] Suscripcion suscripcion)
        {
            if (suscripcion.FechaInicio == default)
            {
                suscripcion.FechaInicio = DateTime.UtcNow;
            }

            _context.Suscripciones.Add(suscripcion);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = suscripcion.Id }, suscripcion);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] Suscripcion suscripcionActualizada)
        {
            if (id != suscripcionActualizada.Id) return BadRequest("Los IDs no coinciden.");

            _context.Entry(suscripcionActualizada).State = EntityState.Modified;

            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Suscripciones.AnyAsync(s => s.Id == id)) return NotFound("Suscripción no encontrada.");
                throw;
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var suscripcion = await _context.Suscripciones.FindAsync(id);
            if (suscripcion == null) return NotFound("Suscripción no encontrada.");

            suscripcion.Estado = "Cancelada";
            _context.Entry(suscripcion).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = $"Suscripción #{id} desactivada correctamente (Estado: Cancelada)." });
        }
    }
}