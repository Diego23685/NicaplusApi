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

        // Zona horaria estándar para Nicaragua
        private static readonly TimeZoneInfo NicaraguaZone = TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");

        public GarantiasTicketsController(ApplicationDbContext context)
        {
            _context = context;
        }

        private DateTime GetNicaraguaTime()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, NicaraguaZone);
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
            var ahoraNicaragua = GetNicaraguaTime();

            // Corregido: Forzar fecha local de Nicaragua
            if (garantia.FechaRepo == default)
            {
                garantia.FechaRepo = ahoraNicaragua;
            }

            // Transacción para asegurar consistencia con caja si hay pérdidas por reposición
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                _context.GarantiasTickets.Add(garantia);
                await _context.SaveChangesAsync();

                // Si la garantía te costó dinero (p.ej. tuviste que comprar otra pantalla/perfil para cumplir), se cae la caja
                if (garantia.CostoReposicion > 0)
                {
                    // Intentamos cargar el nombre del cliente para dejar un rastro claro en caja
                    var cliente = await _context.Clientes.FindAsync(garantia.IdCliente);
                    var clienteNombre = cliente?.Nombre ?? "Genérico";

                    var movimientoCaja = new MovimientoCaja
                    {
                        Fecha = ahoraNicaragua,
                        Tipo = "Egreso",
                        Monto = garantia.CostoReposicion,
                        Concepto = "Gasto Ordinario", // Afecta directamente tu cálculo de utilidad diaria en CajaController
                        Detalle = $"Pérdida por Garantía Ticket ID: {garantia.Id} | Cliente: {clienteNombre} | Motivo: {garantia.Motivo}"
                    };

                    _context.MovimientosCaja.Add(movimientoCaja);
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
                return Ok(garantia);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno al procesar el ticket de garantía: {ex.Message}");
            }
        }
    }
}