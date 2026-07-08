using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.Models;
using NicaplusApi.Models.Requests;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RenovacionesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public RenovacionesController(ApplicationDbContext context)
        {
            _context = context;
        }


        // GET: api/renovaciones
        // Historial completo de renovaciones
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var renovaciones = await _context.Renovaciones
                .Include(r => r.Cliente)
                .Include(r => r.Suscripcion)
                .OrderByDescending(r => r.FechaPago)
                .ToListAsync();

            return Ok(renovaciones);
        }

        // POST api/renovaciones/cancelar

        [HttpPost("cancelar")]
        public async Task<IActionResult> Cancelar(
            [FromBody] CancelarSuscripcionRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var suscripcion = await _context.Suscripciones
                    .Include(s => s.PerfilCuenta)
                    .FirstOrDefaultAsync(s => s.Id == request.IdSuscripcion);


                if (suscripcion == null)
                    return NotFound("La suscripción no existe.");


                if (suscripcion.Estado == "Cancelada")
                    return BadRequest("La suscripción ya está cancelada.");



                var cancelacion = new Cancelacion
                {
                    IdSuscripcion = suscripcion.Id,
                    IdCliente = suscripcion.IdCliente,
                    Motivo = request.Motivo,
                    FechaCancelacion = DateTime.UtcNow
                };


                // Cambiar estado
                suscripcion.Estado = "Cancelada";



                // Liberar perfil si existe
                if (suscripcion.PerfilCuenta != null)
                {
                    suscripcion.PerfilCuenta.Ocupado = false;
                    suscripcion.PerfilCuenta.IdClienteAsignado = null;
                    suscripcion.PerfilCuenta.FechaLiberacion = DateTime.UtcNow;
                }



                _context.Cancelaciones.Add(cancelacion);


                await _context.SaveChangesAsync();

                await transaction.CommitAsync();


                return Ok(new
                {
                    mensaje = "Servicio cancelado correctamente."
                });

            }
            catch(Exception ex)
            {
                await transaction.RollbackAsync();

                return StatusCode(500,
                    $"Error cancelando servicio: {ex.Message}");
            }
        }

        // GET: api/renovaciones/suscripcion/{idSuscripcion}
        // Historial de una suscripción específica
        [HttpGet("suscripcion/{idSuscripcion}")]
        public async Task<IActionResult> GetPorSuscripcion(int idSuscripcion)
        {
            var renovaciones = await _context.Renovaciones
                .Where(r => r.IdSuscripcion == idSuscripcion)
                .Include(r => r.Suscripcion)
                .Include(r => r.Cliente)
                .OrderByDescending(r => r.FechaPago)
                .Select(r => new
                {
                    r.Id,
                    r.IdSuscripcion,
                    Cliente = r.Cliente != null 
                        ? r.Cliente.Nombre 
                        : "Cliente desconocido",

                    Servicio = r.Suscripcion != null
                        ? r.Suscripcion.NombreServicio
                        : "Servicio desconocido",

                    r.Monto,
                    r.FechaPago,
                    r.FechaAnterior,
                    r.NuevaFechaVencimiento,
                    r.MetodoPago,
                    r.Observacion
                })
                .ToListAsync();


            return Ok(renovaciones);
        }

        // POST: api/renovaciones
        // Procesa una renovación completa
        [HttpPost]
        public async Task<IActionResult> Renovar([FromBody] Renovacion renovacion)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var suscripcion = await _context.Suscripciones
                    .Include(s => s.Cliente)
                    .FirstOrDefaultAsync(s => s.Id == renovacion.IdSuscripcion);


                if (suscripcion == null)
                    return NotFound("La suscripción no existe.");


                if (suscripcion.Estado == "Cancelada")
                    return BadRequest("Esta suscripción está cancelada y no puede renovarse.");



                // Guardamos la fecha anterior
                renovacion.IdCliente = suscripcion.IdCliente;

                renovacion.FechaAnterior = suscripcion.FechaVencimiento;

                if (renovacion.FechaPago == default)
                {
                    renovacion.FechaPago = DateTime.UtcNow;
                }


                // Nueva fecha (+30 días)
                renovacion.NuevaFechaVencimiento =
                    suscripcion.FechaVencimiento.AddDays(30);



                // 1. Guardar historial de renovación
                _context.Renovaciones.Add(renovacion);


                // 2. Actualizar suscripción
                suscripcion.FechaVencimiento =
                    renovacion.NuevaFechaVencimiento;

                suscripcion.Estado = "Activa";

                _context.Suscripciones.Update(suscripcion);


                // Guardamos para obtener el Id de Renovacion
                await _context.SaveChangesAsync();


                // 3. Crear movimiento de caja relacionado

                var movimientoCaja = new MovimientoCaja
                {
                    Fecha = renovacion.FechaPago,
                    Tipo = "Ingreso",
                    Concepto = "Renovacion",
                    Monto = renovacion.Monto,
                    Detalle =
                    $"Renovación {suscripcion.NombreServicio} - Cliente ID {suscripcion.IdCliente}",

                    IdRenovacion = renovacion.Id
                };


                _context.MovimientosCaja.Add(movimientoCaja);


                // Guardar movimiento caja
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();


                return Ok(new
                {
                    mensaje = "Renovación procesada correctamente.",
                    nuevaFechaVencimiento = renovacion.NuevaFechaVencimiento
                });

            }
            catch(Exception ex)
            {
                await transaction.RollbackAsync();

                return StatusCode(500, 
                    $"Error procesando renovación: {ex.Message}");
            }
        }
    }
}