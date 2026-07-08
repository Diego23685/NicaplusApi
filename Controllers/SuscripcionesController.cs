using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SuscripcionesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private static readonly TimeZoneInfo NicaraguaZone = TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");

        public SuscripcionesController(ApplicationDbContext context)
        {
            _context = context;
        }

        private DateTime GetNicaraguaTime()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, NicaraguaZone);
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
            // CORREGIDO: Evaluamos estrictamente con la fecha de hoy en Nicaragua
            var hoyNicaragua = GetNicaraguaTime().Date;
            
            var suscripciones = await _context.Suscripciones
                .Include(s => s.Cliente)
                .Where(s => s.Estado != "Cancelada")
                .ToListAsync();

            bool huboCambios = false;

            var listaConAlertas = suscripciones.Select(s =>
            {
                // Aseguramos que la comparación sea Date contra Date bajo el mismo huso horario
                TimeSpan diferencia = s.FechaVencimiento.Date - hoyNicaragua;
                int diasRestantes = diferencia.Days;

                string alertaFiltro = "Normal";
                
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
                .Include(s => s.PerfilCuenta)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (suscripcion == null)
                return NotFound("Suscripción no encontrada.");

            return Ok(suscripcion);
        }

        // POST: api/suscripciones (Altas iniciales de servicios)
        [HttpPost]
        public async Task<ActionResult<Suscripcion>> Post([FromBody] Suscripcion suscripcion)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var ahoraNicaragua = GetNicaraguaTime();

                // CORREGIDO: Fecha de alta atada a tu zona comercial local
                if (suscripcion.FechaInicio == default)
                {
                    suscripcion.FechaInicio = ahoraNicaragua;
                }

                if (suscripcion.FechaVencimiento == default)
                {
                    suscripcion.FechaVencimiento = suscripcion.FechaInicio.AddDays(30);
                }

                _context.Suscripciones.Add(suscripcion);
                await _context.SaveChangesAsync();

                // 2. Crear venta inicial amarrada al mismo eje temporal
                var venta = new Venta
                {
                    FechaVenta = suscripcion.FechaInicio,
                    IdCliente = suscripcion.IdCliente,
                    IdUsuario = 1, // Fallback controlado
                    IdSuscripcion = suscripcion.Id,
                    Total = suscripcion.CostoRenovacion,
                    MetodoPago = "Efectivo"
                };

                _context.Ventas.Add(venta);
                await _context.SaveChangesAsync();

                // 3. Crear detalle de venta
                if (suscripcion.IdProducto.HasValue)
                {
                    var detalle = new DetalleVenta
                    {
                        IdVenta = venta.Id,
                        IdProducto = suscripcion.IdProducto.Value,
                        Cantidad = 1,
                        PrecioUnitario = suscripcion.CostoRenovacion,
                        SubTotal = suscripcion.CostoRenovacion
                    };

                    _context.DetallesVentas.Add(detalle);
                    await _context.SaveChangesAsync();
                }

                // 4. Registrar movimiento de caja perfectamente sincronizado
                var movimiento = new MovimientoCaja
                {
                    Fecha = suscripcion.FechaInicio,
                    Tipo = "Ingreso",
                    Concepto = "Venta Suscripcion",
                    Monto = suscripcion.CostoRenovacion,
                    Detalle = $"Alta inicial {suscripcion.NombreServicio} | Cliente ID: {suscripcion.IdCliente}",
                    IdVenta = venta.Id
                };

                _context.MovimientosCaja.Add(movimiento);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = suscripcion.Id },
                    new
                    {
                        mensaje = "Suscripción creada correctamente.",
                        idSuscripcion = suscripcion.Id,
                        idVenta = venta.Id
                    });
            }
            catch(Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error creando suscripción: {ex.Message}");
            }
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