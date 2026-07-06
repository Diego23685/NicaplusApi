using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.Models;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CuentasPorCobrarController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public CuentasPorCobrarController(ApplicationDbContext context) { _context = context; }

        // GET: api/CuentasPorCobrar
        [HttpGet]
        public async Task<ActionResult> Get([FromQuery] string estado = "Todos")
        {
            var query = _context.CuentasPorCobrar
                .Include(c => c.Cliente)
                .AsQueryable();

            if (estado != "Todos")
            {
                query = query.Where(c => c.Estado == estado);
            }

            return Ok(await query.OrderByDescending(c => c.FechaVencimiento).ToListAsync());
        }

        // POST: api/CuentasPorCobrar (Creación Manual de Deuda externa a una venta)
        [HttpPost]
        public async Task<ActionResult> Post([FromBody] CuentaPorCobrar cxC)
        {
            if (cxC.FechaEmision == default) cxC.FechaEmision = DateTime.UtcNow;
            cxC.Estado = "Pendiente";
            cxC.SaldoPendiente = cxC.MontoTotal;

            _context.CuentasPorCobrar.Add(cxC);
            await _context.SaveChangesAsync();
            return Ok(cxC);
        }

        // PUT: api/CuentasPorCobrar/5/abonar (Para registrar pagos desde la vista)
        [HttpPut("{id}/abonar")]
        public async Task<IActionResult> Abonar(int id, [FromQuery] decimal montoAbono)
        {
            var cuenta = await _context.CuentasPorCobrar.FindAsync(id);
            if (cuenta == null) return NotFound("Registro no encontrado.");

            if (cuenta.Estado == "Pagado") return BadRequest("Esta cuenta ya está liquidada.");

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