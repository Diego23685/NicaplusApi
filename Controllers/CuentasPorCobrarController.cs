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
    public class CuentasPorCobrarController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        
        // Zona horaria estándar para Nicaragua
        private static readonly TimeZoneInfo NicaraguaZone = TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");

        public CuentasPorCobrarController(ApplicationDbContext context) 
        { 
            _context = context; 
        }

        private DateTime GetNicaraguaTime()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, NicaraguaZone);
        }

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
            // Corregido: Si no trae fecha, se asigna la fecha/hora local de Nicaragua
            if (cxC.FechaEmision == default) 
            {
                cxC.FechaEmision = GetNicaraguaTime();
            }

            cxC.Estado = "Pendiente";
            cxC.SaldoPendiente = cxC.MontoTotal;

            _context.CuentasPorCobrar.Add(cxC);
            await _context.SaveChangesAsync();
            return Ok(cxC);
        }

        // PUT: api/CuentasPorCobrar/5/abonar
        [HttpPut("{id}/abonar")]
        public async Task<IActionResult> Abonar(int id, [FromQuery] decimal montoAbono, [FromQuery] string metodoPago = "Efectivo")
        {
            if (montoAbono <= 0) return BadRequest("El monto del abono debe ser mayor a cero.");

            // Iniciamos una transacción para asegurar que se guarde la deuda Y el movimiento de caja de golpe
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var cuenta = await _context.CuentasPorCobrar
                    .Include(c => c.Cliente)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (cuenta == null) return NotFound("Registro no encontrado.");
                if (cuenta.Estado == "Pagado") return BadRequest("Esta cuenta ya está liquidada.");

                // Validar que no abonen más de lo que deben
                if (montoAbono > cuenta.SaldoPendiente)
                {
                    return BadRequest($"El abono no puede ser mayor al saldo pendiente (${cuenta.SaldoPendiente}).");
                }

                var ahoraNicaragua = GetNicaraguaTime();

                // 1. Modificar saldo de la cuenta por cobrar
                cuenta.SaldoPendiente -= montoAbono;
                if (cuenta.SaldoPendiente <= 0)
                {
                    cuenta.SaldoPendiente = 0;
                    cuenta.Estado = "Pagado";
                }

                // 2. Registrar el ingreso en la caja adaptado estrictamente a tu modelo MovimientoCaja
            var nombreCliente = cuenta.Cliente?.Nombre ?? "Cliente Genérico";
            var movimientoCaja = new MovimientoCaja
            {
                Fecha = ahoraNicaragua,
                Tipo = "Ingreso",
                Monto = montoAbono,
                Concepto = "Abono CxC",
                // Concatenamos todo en la propiedad 'Detalle' que sí existe en tu modelo
                Detalle = $"Abono a cuenta por cobrar ID: {cuenta.Id} | Cliente: {nombreCliente} | Método: {metodoPago}"
            };

            _context.MovimientosCaja.Add(movimientoCaja);
                
                await _context.SaveChangesAsync();
                await transaction.CommitAsync(); // Confirmar cambios en la BD

                return Ok(cuenta);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error interno al procesar el abono: {ex.Message}");
            }
        }
    }
}