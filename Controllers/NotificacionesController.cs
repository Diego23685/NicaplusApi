using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;

namespace NicaplusApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificacionesController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public NotificacionesController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("pendientes")]
    [Authorize]
    public async Task<IActionResult> GetPendientes()
    {
        var hoy = DateTime.Now;
        var alertaFecha = hoy.AddDays(7); // Alerta para renovaciones a 7 días

        var renovaciones = await _context.Suscripciones
            .Where(s => s.FechaVencimiento <= alertaFecha && s.Estado == "Activa")
            .Select(s => new { s.NombreServicio, s.FechaVencimiento, Tipo = "Renovación" }).ToListAsync();

        var tickets = await _context.TicketsSoporte
            .Where(t => t.Estado == "Pendiente")
            .Select(t => new { t.Id, t.TipoTicket, Tipo = "Ticket" }).ToListAsync();

        var stockBajo = await _context.Productos
            .Where(p => p.StockActual <= p.StockMinimo)
            .Select(p => new { p.Nombre, p.StockActual, Tipo = "Inventario" }).ToListAsync();

        var garantias = await _context.GarantiasTickets
            .Where(g => g.Estado == "Pendiente")
            .Select(g => new { g.Id, g.Motivo, Tipo = "Garantía" }).ToListAsync();

        return Ok(new { renovaciones, tickets, stockBajo, garantias });
    }
}