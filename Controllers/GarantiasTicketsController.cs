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
    public class GarantiasTicketsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public GarantiasTicketsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/GarantiasTickets
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> Get()
        {
            return Ok(await _context.GarantiasTickets
                .Include(g => g.Cliente)
                .Include(g => g.Responsable)
                .OrderByDescending(g => g.FechaRepo)
                .Select(g => new {
                    g.Id,
                    g.IdCliente,
                    g.IdUsuarioResponsable,
                    g.Motivo,
                    g.FechaRepo,
                    g.CuentaAnterior,
                    g.CuentaNueva,
                    g.CostoReposicion,
                    ClienteNombre = g.Cliente != null ? g.Cliente.Nombre : "Genérico",
                    ResponsableNombre = g.Responsable != null ? g.Responsable.Nombre : "Admin"
                })
                .ToListAsync());
        }

        // POST: api/GarantiasTickets
        [HttpPost]
        public async Task<ActionResult<GarantiaTicket>> Post([FromBody] GarantiaTicket garantia)
        {
            if (garantia.FechaRepo == default) garantia.FechaRepo = DateTime.UtcNow;

            _context.GarantiasTickets.Add(garantia);
            await _context.SaveChangesAsync();

            return Ok(garantia);
        }
    }
}