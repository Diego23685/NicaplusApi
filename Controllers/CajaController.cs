using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.DTOs.Caja;
using NicaplusApi.Models;
using System.Runtime.InteropServices;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Protegemos el módulo contable
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

        /// <summary>
        /// Obtiene la zona horaria de Nicaragua compatible tanto con Windows como con Linux/Docker.
        /// </summary>
        private static TimeZoneInfo GetNicaraguaTimeZone()
        {
            try
            {
                // Identificador para Windows
                return TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                // Identificador IANA para Linux / macOS / Docker
                return TimeZoneInfo.FindSystemTimeZoneById("America/Managua");
            }
        }

        private DateTime GetNicaraguaTime()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, GetNicaraguaTimeZone());
        }

        #region Endpoints

        // GET: api/Caja/movimientos
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
                        Fecha = m.Fecha
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

        // POST: api/Caja/movimientos
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
                    ? dto.Fecha.Value.Date
                    : GetNicaraguaTime();

                var movimiento = new MovimientoCaja
                {
                    Tipo = dto.Tipo,
                    Concepto = dto.Concepto,
                    Monto = dto.Monto,
                    Detalle = dto.Detalle ?? string.Empty, // Aseguramos que no sea nulo
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
                        Fecha = movimiento.Fecha
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar un nuevo movimiento de caja.");
                return StatusCode(500, new { mensaje = "Error interno al guardar el movimiento." });
            }
        }

        // GET: api/Caja/reporte-utilidades
        [HttpGet("reporte-utilidades")]
        public async Task<IActionResult> GetReporteUtilidades()
        {
            try
            {
                // 1. Puntos cronológicos en horario de Nicaragua
                var hoyNicaragua = GetNicaraguaTime().Date;
                var inicioMesNicaragua = new DateTime(hoyNicaragua.Year, hoyNicaragua.Month, 1);
                var mañanaNicaragua = hoyNicaragua.AddDays(1);

                // 2. Filtrado optimizado en BD sin Change Tracking
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

                // 3. Cálculos del Día
                var movsHoy = movimientosMes.Where(m => m.Fecha.Date == hoyNicaragua).ToList();
                decimal ingresosDia = movsHoy.Where(m => m.Tipo == "Ingreso").Sum(m => m.Monto);
                decimal egresosDia = movsHoy.Where(m => m.Tipo == "Egreso").Sum(m => m.Monto);
                decimal comprasDia = movsHoy.Where(m => m.Concepto == "Compra Proveedor").Sum(m => m.Monto);
                decimal gastosDia = movsHoy.Where(m => m.Concepto == "Gasto Ordinario").Sum(m => m.Monto);

                decimal utilidadDiaria = ventasMes.Where(v => v.FechaVenta.Date == hoyNicaragua)
                    .SelectMany(v => v.Detalles)
                    .Sum(d => d.Producto != null ? (d.PrecioUnitario - d.Producto.PrecioCosto) * d.Cantidad : d.SubTotal) - gastosDia;

                // 4. Cálculos del Mes
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

        #endregion
    }
}