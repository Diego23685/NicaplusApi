using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Administrador")] // Solo el Admin puede ver el historial
    public class AuditoriaController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AuditoriaController(ApplicationDbContext context) => _context = context;

        [HttpGet]
        public async Task<IActionResult> GetLogs()
        {
            // Traemos los últimos 100 registros ordenados por fecha
            var logs = await _context.LogsAuditoria
                .OrderByDescending(l => l.FechaRegistro)
                .Take(100)
                .ToListAsync();
            return Ok(logs);
        }
    }
}