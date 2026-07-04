using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReportesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ReportesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("personalizado")]
        public async Task<IActionResult> GetReportePersonalizado([FromQuery] DateTime desde, [FromQuery] DateTime hasta)
        {
            // Forzar que el rango cubra desde las 00:00:00 del primer día hasta las 23:59:59 del último
            var fechaInicio = desde.Date;
            var fechaFin = hasta.Date.AddDays(1).AddTicks(-1);

            // 1. Obtener las ventas en el rango de fecha
            var ventas = await _context.Ventas
                .Include(v => v.Usuario)
                .Where(v => v.FechaVenta >= fechaInicio && v.FechaVenta <= fechaFin)
                .OrderByDescending(v => v.FechaVenta)
                .ToListAsync();

            // 2. Cálculos financieros
            var totalEfectivo = ventas.Where(v => v.MetodoPago == "Efectivo").Sum(v => v.Total);
            var totalTransferencia = ventas.Where(v => v.MetodoPago == "Transferencia").Sum(v => v.Total);
            var totalTarjeta = ventas.Where(v => v.MetodoPago == "Tarjeta").Sum(v => v.Total);
            var granTotal = totalEfectivo + totalTransferencia + totalTarjeta;

            // 3. Top Productos del período
            var topProductos = await _context.DetallesVentas
                .Include(d => d.Producto)
                .Where(d => d.Venta!.FechaVenta >= fechaInicio && d.Venta!.FechaVenta <= fechaFin)
                .GroupBy(d => new { d.IdProducto, d.Producto!.Nombre })
                .Select(g => new
                {
                    Producto = g.Key.Nombre,
                    Cantidad = g.Sum(d => d.Cantidad),
                    Subtotal = g.Sum(d => d.SubTotal)
                })
                .OrderByDescending(x => x.Cantidad)
                .Take(5)
                .ToListAsync();

            // 4. Mapear listado de transacciones limpio para la tabla del PDF
            var listaTransacciones = ventas.Select(v => new {
                v.Id,
                Fecha = v.FechaVenta.ToString("yyyy-MM-dd HH:mm"),
                Operador = v.Usuario?.Nombre ?? "Sistema",
                v.MetodoPago,
                v.Total
            }).ToList();

            return Ok(new
            {
                Rango = $"{fechaInicio:dd/MM/yyyy} al {fechaFin:dd/MM/yyyy}",
                VentasTotales = ventas.Count,
                Finanzas = new { Efectivo = totalEfectivo, Transferencia = totalTransferencia, Tarjeta = totalTarjeta, Total = granTotal },
                TopProductos = topProductos,
                Transacciones = listaTransacciones
            });
        }

        [HttpGet("resumen-dashboard")]
        public async Task<IActionResult> GetResumenDashboard()
        {
            var hoy = DateTime.UtcNow;
            var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);

            // --- LÓGICA NUEVA: CALCULAR INGRESOS DE LA SEMANA ACTUAL (LUNES A DOMINGO) ---
            // En .NET, DayOfWeek.Sunday es 0, Monday es 1... Convertimos para que Lunes sea 0 y Domingo 6
            int diasDesdeLunes = ((int)hoy.DayOfWeek - 1 + 7) % 7;
            var inicioSemana = hoy.Date.AddDays(-diasDesdeLunes); // Lunes a las 00:00
            var finSemana = inicioSemana.AddDays(7);              // Próximo lunes a las 00:00

            // Consultar las ventas de la semana
            var ventasSemana = await _context.Ventas
                .Where(v => v.FechaVenta >= inicioSemana && v.FechaVenta < finSemana)
                .ToListAsync();

            // Inicializar los 7 días de la semana en 0 (0: Lun, 1: Mar, ..., 6: Dom)
            var ingresosSemana = new decimal[7];
            foreach (var venta in ventasSemana)
            {
                int indiceDia = ((int)venta.FechaVenta.DayOfWeek - 1 + 7) % 7;
                ingresosSemana[indiceDia] += venta.Total;
            }
            // -----------------------------------------------------------------------------

            // Totales del mes (Existente)
            var detallesVentasMes = await _context.DetallesVentas
                .Include(d => d.Producto)
                .Where(d => d.Venta!.FechaVenta >= inicioMes)
                .ToListAsync();

            var totalDigitales = detallesVentasMes.Where(d => d.Producto!.EsDigital).Sum(d => d.SubTotal);
            var totalSoporte = detallesVentasMes.Where(d => d.Producto!.RequiereServicio).Sum(d => d.SubTotal);            var totalFisicos = detallesVentasMes.Where(d => !d.Producto!.EsDigital && !d.Producto!.RequiereServicio).Sum(d => d.SubTotal);

            var ventasMes = await _context.Ventas.Where(v => v.FechaVenta >= inicioMes).ToListAsync();
            var totalMes = ventasMes.Sum(v => v.Total);
            var costoMes = detallesVentasMes.Sum(d => d.Producto!.PrecioCosto * d.Cantidad);

            var ultimosProductos = await _context.Productos.OrderByDescending(p => p.Id).Take(3).ToListAsync();
            var ultimosClientes = await _context.Clientes.OrderByDescending(c => c.Id).Take(3).ToListAsync();

            return Ok(new
            {
                TotalVendidoMes = totalMes,
                TotalCostoMes = costoMes,
                CantidadVentasMes = ventasMes.Count,
                UltimosProductos = ultimosProductos,
                UltimosClientes = ultimosClientes,
                Rubros = new decimal[] { totalFisicos, totalDigitales, totalSoporte },
                SemanaFlujo = ingresosSemana // <-- NUEVO: Mandamos los ingresos reales por día
            });
        }
    }
}