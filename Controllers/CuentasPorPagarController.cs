using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.Models;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CuentasPorPagarController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public CuentasPorPagarController(ApplicationDbContext context) { _context = context; }

        // GET: api/CuentasPorPagar
        [HttpGet]
        public async Task<ActionResult> Get([FromQuery] string estado = "Todos")
        {
            // CORREGIDO: Se incluye la relación del proveedor para que no llegue nulo al frontend
            var query = _context.CuentasPorPagar
                .Include(p => p.Proveedor)
                .AsQueryable();

            if (estado != "Todos")
            {
                query = query.Where(c => c.Estado == estado);
            }

            return Ok(await query.OrderByDescending(c => c.FechaVencimiento).ToListAsync());
        }

        // POST: api/CuentasPorPagar
        [HttpPost]
        public async Task<ActionResult> Post([FromBody] CuentaPorPagar cxP)
        {
            // CORREGIDO: Se cambia FechaEmision por FechaRegistro para coincidir con tu modelo
            if (cxP.FechaRegistro == default) cxP.FechaRegistro = DateTime.UtcNow;
            cxP.Estado = "Pendiente";
            cxP.SaldoPendiente = cxP.MontoTotal;

            _context.CuentasPorPagar.Add(cxP);
            await _context.SaveChangesAsync();
            return Ok(cxP);
        }

        // PUT: api/CuentasPorPagar/5/abonar
        [HttpPut("{id}/abonar")]
        public async Task<IActionResult> Abonar(int id, [FromQuery] decimal montoAbono)
        {
            var cuenta = await _context.CuentasPorPagar.FindAsync(id);
            if (cuenta == null) return NotFound("Registro no encontrado.");

            if (cuenta.Estado == "Pagado") return BadRequest("Esta cuenta ya está liquidada con el proveedor.");

            cuenta.SaldoPendiente -= montoAbono;
            if (cuenta.SaldoPendiente <= 0)
            {
                cuenta.SaldoPendiente = 0;
                cuenta.Estado = "Pagado";
            }

            await _context.SaveChangesAsync();
            return Ok(cuenta);
        }
    }
}