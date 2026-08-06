using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.DTOs.Caja;
using NicaplusApi.Models;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CajaController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CajaController> _logger;

        public CajaController(
            ApplicationDbContext context,
            ILogger<CajaController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private static TimeZoneInfo GetNicaraguaTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/Managua");
            }
        }

        private DateTime GetNicaraguaTime()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, GetNicaraguaTimeZone());
        }

        // 1. GET: api/Caja/movimientos (Límite rápido)
        [HttpGet("movimientos")]
        public async Task<IActionResult> GetMovimientos([FromQuery] int limite = 50)
        {
            try
            {
                var movimientos = await _context.MovimientosCaja
                    .AsNoTracking()
                    .OrderByDescending(m => m.Fecha)
                    .Take(limite)
                    .Select(m => new MovimientoCajaResponseDto
                    {
                        Id = m.Id,
                        Tipo = m.Tipo,
                        Concepto = m.Concepto,
                        Monto = m.Monto,
                        Detalle = m.Detalle,
                        Fecha = m.Fecha,
                        EsAutomatico = m.IdVenta.HasValue || m.IdCompraProveedor.HasValue
                    })
                    .ToListAsync();

                return Ok(new { total = movimientos.Count, datos = movimientos });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener la lista de movimientos de caja.");
                return StatusCode(500, new { mensaje = "Error interno al consultar los movimientos de caja." });
            }
        }

        // 2. GET: api/Caja/movimientos/historial (Filtrado por Rango de Fechas)
        [HttpGet("movimientos/historial")]
        public async Task<IActionResult> GetHistorialMovimientos([FromQuery] DateTime desde, [FromQuery] DateTime hasta)
        {
            try
            {
                var fInicio = desde.Date;
                var fFin = hasta.Date.AddDays(1).AddTicks(-1);

                var movimientos = await _context.MovimientosCaja
                    .AsNoTracking()
                    .Where(m => m.Fecha >= fInicio && m.Fecha <= fFin)
                    .OrderByDescending(m => m.Fecha)
                    .Select(m => new MovimientoCajaResponseDto
                    {
                        Id = m.Id,
                        Tipo = m.Tipo,
                        Concepto = m.Concepto,
                        Monto = m.Monto,
                        Detalle = m.Detalle,
                        Fecha = m.Fecha,
                        EsAutomatico = m.IdVenta.HasValue || m.IdCompraProveedor.HasValue
                    })
                    .ToListAsync();

                return Ok(movimientos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar historial por fechas.");
                return StatusCode(500, new { mensaje = "Error interno al consultar el historial." });
            }
        }

        // 3. POST: api/Caja/movimientos
        [HttpPost("movimientos")]
        public async Task<IActionResult> Post([FromBody] CrearMovimientoCajaDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { mensaje = "Datos de movimiento inválidos.", detalles = ModelState });
            }

            try
            {
                DateTime fechaFinal = dto.Fecha.HasValue && dto.Fecha.Value != default
                    ? dto.Fecha.Value
                    : GetNicaraguaTime();

                var movimiento = new MovimientoCaja
                {
                    Tipo = dto.Tipo,
                    Concepto = dto.Concepto,
                    Monto = dto.Monto,
                    Detalle = dto.Detalle ?? string.Empty,
                    Fecha = fechaFinal
                };

                _context.MovimientosCaja.Add(movimiento);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    mensaje = "Movimiento registrado con éxito.",
                    movimiento = new MovimientoCajaResponseDto
                    {
                        Id = movimiento.Id,
                        Tipo = movimiento.Tipo,
                        Concepto = movimiento.Concepto,
                        Monto = movimiento.Monto,
                        Detalle = movimiento.Detalle,
                        Fecha = movimiento.Fecha,
                        EsAutomatico = false
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar un nuevo movimiento de caja.");
                return StatusCode(500, new { mensaje = "Error interno al guardar el movimiento." });
            }
        }

        // 4. PUT: api/Caja/movimientos/5 (Edición de Movimiento Manual)
        [HttpPut("movimientos/{id}")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> PutMovimiento(int id, [FromBody] CrearMovimientoCajaDto dto)
        {
            try
            {
                var movimiento = await _context.MovimientosCaja.FindAsync(id);
                if (movimiento == null)
                    return NotFound(new { mensaje = "El movimiento de caja no existe." });

                // Protección: No permitir editar si el movimiento se generó automáticamente desde una Venta o Compra
                if (movimiento.IdVenta.HasValue || movimiento.IdCompraProveedor.HasValue)
                {
                    return BadRequest(new { mensaje = "No se puede editar directamente un movimiento automático de Venta o Compra. Debe editar la factura o compra correspondiente." });
                }

                movimiento.Tipo = dto.Tipo;
                movimiento.Concepto = dto.Concepto;
                movimiento.Monto = dto.Monto;
                movimiento.Detalle = dto.Detalle ?? string.Empty;
                if (dto.Fecha.HasValue && dto.Fecha.Value != default)
                {
                    movimiento.Fecha = dto.Fecha.Value;
                }

                await _context.SaveChangesAsync();
                return Ok(new { mensaje = "Movimiento de caja actualizado con éxito." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al editar el movimiento #{Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al actualizar el movimiento." });
            }
        }

        // 5. DELETE: api/Caja/movimientos/5 (Eliminación de Movimiento Manual)
        [HttpDelete("movimientos/{id}")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> DeleteMovimiento(int id)
        {
            try
            {
                var movimiento = await _context.MovimientosCaja.FindAsync(id);
                if (movimiento == null)
                    return NotFound(new { mensaje = "El movimiento de caja no existe." });

                if (movimiento.IdVenta.HasValue || movimiento.IdCompraProveedor.HasValue)
                {
                    return BadRequest(new { mensaje = "No se puede eliminar directamente un movimiento automático. Debe anular la Venta o Compra desde su módulo correspondiente." });
                }

                _context.MovimientosCaja.Remove(movimiento);
                await _context.SaveChangesAsync();

                return Ok(new { mensaje = "Movimiento de caja eliminado y balance recalculado." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar el movimiento #{Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al eliminar el movimiento." });
            }
        }

        // 6. GET: api/Caja/reporte-utilidades
        [HttpGet("reporte-utilidades")]
        public async Task<IActionResult> GetReporteUtilidades()
        {
            try
            {
                var hoyNicaragua = GetNicaraguaTime().Date;
                var inicioMesNicaragua = new DateTime(hoyNicaragua.Year, hoyNicaragua.Month, 1);
                var mañanaNicaragua = hoyNicaragua.AddDays(1);

                var movimientosMes = await _context.MovimientosCaja
                    .AsNoTracking()
                    .Where(m => m.Fecha >= inicioMesNicaragua && m.Fecha < mañanaNicaragua)
                    .ToListAsync();

                var ventasMes = await _context.Ventas
                    .AsNoTracking()
                    .Include(v => v.Detalles)
                    .ThenInclude(d => d.Producto)
                    .Where(v => v.FechaVenta >= inicioMesNicaragua && v.FechaVenta < mañanaNicaragua)
                    .ToListAsync();

                var movsHoy = movimientosMes.Where(m => m.Fecha.Date == hoyNicaragua).ToList();
                decimal ingresosDia = movsHoy.Where(m => m.Tipo == "Ingreso").Sum(m => m.Monto);
                decimal egresosDia = movsHoy.Where(m => m.Tipo == "Egreso").Sum(m => m.Monto);
                decimal comprasDia = movsHoy.Where(m => m.Concepto == "Compra Proveedor").Sum(m => m.Monto);
                decimal gastosDia = movsHoy.Where(m => m.Concepto == "Gasto Ordinario").Sum(m => m.Monto);

                decimal utilidadDiaria = ventasMes.Where(v => v.FechaVenta.Date == hoyNicaragua)
                    .SelectMany(v => v.Detalles)
                    .Sum(d => d.Producto != null ? (d.PrecioUnitario - d.Producto.PrecioCosto) * d.Cantidad : d.SubTotal) - gastosDia;

                decimal ingresosMes = movimientosMes.Where(m => m.Tipo == "Ingreso").Sum(m => m.Monto);
                decimal egresosMes = movimientosMes.Where(m => m.Tipo == "Egreso").Sum(m => m.Monto);
                decimal comprasMes = movimientosMes.Where(m => m.Concepto == "Compra Proveedor").Sum(m => m.Monto);
                decimal gastosMes = movimientosMes.Where(m => m.Concepto == "Gasto Ordinario").Sum(m => m.Monto);

                decimal utilidadMensual = ventasMes
                    .SelectMany(v => v.Detalles)
                    .Sum(d => d.Producto != null ? (d.PrecioUnitario - d.Producto.PrecioCosto) * d.Cantidad : d.SubTotal) - gastosMes;

                return Ok(new
                {
                    dia = new
                    {
                        ingresos = ingresosDia,
                        egresos = egresosDia,
                        compras = comprasDia,
                        gastos = gastosDia,
                        utilidad = utilidadDiaria
                    },
                    mes = new
                    {
                        ingresos = ingresosMes,
                        egresos = egresosMes,
                        compras = comprasMes,
                        gastos = gastosMes,
                        utilidad = utilidadMensual
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar el reporte financiero de utilidades.");
                return StatusCode(500, new { mensaje = "Error interno al calcular el reporte de utilidades." });
            }
        }
    }
}