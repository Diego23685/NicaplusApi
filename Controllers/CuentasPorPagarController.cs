using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CuentasPorPagarController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        
        // Zona horaria estándar para Nicaragua
        private static readonly TimeZoneInfo NicaraguaZone = TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");

        public CuentasPorPagarController(ApplicationDbContext context) { _context = context; }

        private DateTime GetNicaraguaTime()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, NicaraguaZone);
        }

        // GET: api/CuentasPorPagar
        [HttpGet]
        public async Task<ActionResult> Get([FromQuery] string estado = "Todos")
        {
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
            // Corregido: Seteo de fecha local del negocio
            if (cxP.FechaRegistro == default) 
            {
                cxP.FechaRegistro = GetNicaraguaTime();
            }
            
            cxP.Estado = "Pendiente";
            cxP.SaldoPendiente = cxP.MontoTotal;

            _context.CuentasPorPagar.Add(cxP);
            await _context.SaveChangesAsync();
            return Ok(cxP);
        }

        // PUT: api/CuentasPorPagar/5/abonar
        [HttpPut("{id}/abonar")]
        public async Task<IActionResult> Abonar(int id, [FromQuery] decimal montoAbono, [FromQuery] string metodoPago = "Efectivo")
        {
            if (montoAbono <= 0) return BadRequest("El monto del abono debe ser mayor a cero.");

            // Transacción atómica: Asegura abono en CxP y salida de Caja juntas
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Incluimos Proveedor para obtener la Razón Social para el Detalle de caja
                var cuenta = await _context.CuentasPorPagar
                    .Include(p => p.Proveedor)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (cuenta == null) return NotFound("Registro no encontrado.");
                if (cuenta.Estado == "Pagado") return BadRequest("Esta cuenta ya está liquidada con el proveedor.");

                if (montoAbono > cuenta.SaldoPendiente)
                {
                    return BadRequest($"El abono no puede exceder el saldo pendiente (${cuenta.SaldoPendiente}).");
                }

                var ahoraNicaragua = GetNicaraguaTime();

                // 1. Modificar saldos de la cuenta por pagar
                cuenta.SaldoPendiente -= montoAbono;
                if (cuenta.SaldoPendiente <= 0)
                {
                    cuenta.SaldoPendiente = 0;
                    cuenta.Estado = "Pagado";
                }

                // 2. CORREGIDO: Registrar la salida de dinero real (Egreso) en la caja
                var proveedorNombre = cuenta.Proveedor?.RazonSocial ?? "Proveedor Genérico";
                var movimientoCaja = new MovimientoCaja
                {
                    Fecha = ahoraNicaragua,
                    Tipo = "Egreso",
                    Monto = montoAbono,
                    Concepto = "Gasto Ordinario", // Vincula directo a tu lógica de CajaController
                    Detalle = $"Abono a proveedor: {proveedorNombre} | CxP ID: {cuenta.Id} | Método: {metodoPago}"
                };

                _context.MovimientosCaja.Add(movimientoCaja);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(cuenta);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno al procesar abono a proveedor: {ex.Message}");
            }
        }
    }
}