using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.DTOs;
using System.Security.Claims;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReportesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ReportesController> _logger;

        public ReportesController(
            ApplicationDbContext context,
            ILogger<ReportesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private DateTime GetNicaraguaTime()
        {
            TimeZoneInfo zone;
            try
            {
                zone = TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                zone = TimeZoneInfo.FindSystemTimeZoneById("America/Managua");
            }

            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
        }

        // 1. GET: api/Reportes/personalizado
        [Authorize(Roles = "Administrador,Socio")]
        [HttpGet("personalizado")]
        public async Task<IActionResult> GetReportePersonalizado([FromQuery] DateTime desde, [FromQuery] DateTime hasta)
        {
            try
            {
                var fechaInicio = desde.Date;
                var fechaFin = hasta.Date.AddDays(1).AddTicks(-1);

                // Ejecución optimizada en Servidor de BD
                var totalVentasCount = await _context.Ventas
                    .Where(v => v.FechaVenta >= fechaInicio && v.FechaVenta <= fechaFin)
                    .CountAsync();

                var totalEfectivo = await _context.Ventas
                    .Where(v => v.FechaVenta >= fechaInicio && v.FechaVenta <= fechaFin && v.MetodoPago == "Efectivo")
                    .SumAsync(v => (decimal?)v.Total) ?? 0m;

                var totalTransferencia = await _context.Ventas
                    .Where(v => v.FechaVenta >= fechaInicio && v.FechaVenta <= fechaFin && v.MetodoPago == "Transferencia")
                    .SumAsync(v => (decimal?)v.Total) ?? 0m;

                var totalTarjeta = await _context.Ventas
                    .Where(v => v.FechaVenta >= fechaInicio && v.FechaVenta <= fechaFin && v.MetodoPago == "Tarjeta")
                    .SumAsync(v => (decimal?)v.Total) ?? 0m;

                var totalIngresosExtra = await _context.MovimientosCaja
                    .Where(m => m.Fecha >= fechaInicio && m.Fecha <= fechaFin && m.Tipo == "Ingreso" && m.Concepto != "Venta" && m.Concepto != "Renovacion")
                    .SumAsync(m => (decimal?)m.Monto) ?? 0m;

                var totalGastosFijos = await _context.MovimientosCaja
                    .Where(m => m.Fecha >= fechaInicio && m.Fecha <= fechaFin && (m.Tipo == "Egreso" || m.Concepto == "Gasto Ordinario" || m.Concepto == "Ajuste"))
                    .SumAsync(m => (decimal?)m.Monto) ?? 0m;

                var totalComprasProveedores = await _context.MovimientosCaja
                    .Where(m => m.Fecha >= fechaInicio && m.Fecha <= fechaFin && m.Concepto == "Compra Proveedor")
                    .SumAsync(m => (decimal?)m.Monto) ?? 0m;

                var granTotalFacturado = totalEfectivo + totalTransferencia + totalTarjeta;
                var balanceNetoEfectivoCaja = (granTotalFacturado + totalIngresosExtra) - (totalGastosFijos + totalComprasProveedores);

                var topProductos = await _context.DetallesVentas
                    .AsNoTracking()
                    .Where(d => d.Venta != null && d.Venta.FechaVenta >= fechaInicio && d.Venta.FechaVenta <= fechaFin)
                    .GroupBy(d => new { d.IdProducto, Nombre = d.Producto != null ? d.Producto.Nombre : "Servicio/Producto General" })
                    .Select(g => new TopProductoDto
                    {
                        Producto = g.Key.Nombre,
                        Cantidad = g.Sum(d => d.Cantidad),
                        Subtotal = g.Sum(d => d.SubTotal)
                    })
                    .OrderByDescending(x => x.Cantidad)
                    .Take(5)
                    .ToListAsync();

                var listaTransacciones = await _context.Ventas
                    .AsNoTracking()
                    .Where(v => v.FechaVenta >= fechaInicio && v.FechaVenta <= fechaFin)
                    .OrderByDescending(v => v.FechaVenta)
                    .Select(v => new TransaccionResumenDto
                    {
                        Id = v.Id,
                        Fecha = v.FechaVenta.ToString("yyyy-MM-dd HH:mm"),
                        Operador = v.Usuario != null ? v.Usuario.Nombre : "Sistema",
                        MetodoPago = v.MetodoPago,
                        Total = v.Total
                    })
                    .ToListAsync();

                var resultado = new ReportePersonalizadoDto
                {
                    Rango = $"{fechaInicio:dd/MM/yyyy} al {hasta.Date:dd/MM/yyyy}",
                    VentasTotales = totalVentasCount,
                    Finanzas = new FinanzasResumenDto
                    {
                        Efectivo = totalEfectivo,
                        Transferencia = totalTransferencia,
                        Tarjeta = totalTarjeta,
                        TotalFacturado = granTotalFacturado,
                        GastosOperativos = totalGastosFijos,
                        InversionCompras = totalComprasProveedores,
                        BalanceCajaReal = balanceNetoEfectivoCaja
                    },
                    TopProductos = topProductos,
                    Transacciones = listaTransacciones
                };

                return Ok(resultado);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generando reporte personalizado desde {Desde} hasta {Hasta}", desde, hasta);
                return StatusCode(500, new { mensaje = "Error interno al procesar el reporte personalizado." });
            }
        }

        // 2. GET: api/Reportes/resumen-dashboard
        [HttpGet("resumen-dashboard")]
        public async Task<IActionResult> GetResumenDashboard()
        {
            try
            {
                var hoyNicaragua = GetNicaraguaTime();
                var hoy = hoyNicaragua.Date;
                var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);

                int diasDesdeLunes = ((int)hoy.DayOfWeek - 1 + 7) % 7;
                var inicioSemana = hoy.AddDays(-diasDesdeLunes);
                var finSemana = inicioSemana.AddDays(7);
                var mañana = hoy.AddDays(1);

                // Agregaciones de ventas calculadas directamente por la Base de Datos
                var totalVentaDia = await _context.Ventas
                    .Where(v => v.FechaVenta >= hoy && v.FechaVenta < mañana)
                    .SumAsync(v => (decimal?)v.Total) ?? 0m;

                var totalVentaSemana = await _context.Ventas
                    .Where(v => v.FechaVenta >= inicioSemana && v.FechaVenta < finSemana)
                    .SumAsync(v => (decimal?)v.Total) ?? 0m;

                var totalVentaMes = await _context.Ventas
                    .Where(v => v.FechaVenta >= inicioMes && v.FechaVenta < mañana)
                    .SumAsync(v => (decimal?)v.Total) ?? 0m;

                // Construcción de Flujo Semanal
                var ventasSemanaRaw = await _context.Ventas
                    .AsNoTracking()
                    .Where(v => v.FechaVenta >= inicioSemana && v.FechaVenta < finSemana)
                    .Select(v => new { v.FechaVenta, v.Total })
                    .ToListAsync();

                var ingresosSemana = new decimal[7];
                foreach (var venta in ventasSemanaRaw)
                {
                    int indiceDia = ((int)venta.FechaVenta.DayOfWeek - 1 + 7) % 7;
                    ingresosSemana[indiceDia] += venta.Total;
                }

                // Desglose por Rubros
                var totalDigitales = await _context.DetallesVentas
                    .Where(d => d.Venta != null && d.Venta.FechaVenta >= inicioMes && d.Venta.FechaVenta < mañana && d.Producto != null && d.Producto.EsDigital)
                    .SumAsync(d => (decimal?)d.SubTotal) ?? 0m;

                var totalSoporte = await _context.DetallesVentas
                    .Where(d => d.Venta != null && d.Venta.FechaVenta >= inicioMes && d.Venta.FechaVenta < mañana && d.Producto != null && d.Producto.RequiereServicio)
                    .SumAsync(d => (decimal?)d.SubTotal) ?? 0m;

                var totalFisicos = await _context.DetallesVentas
                    .Where(d => d.Venta != null && d.Venta.FechaVenta >= inicioMes && d.Venta.FechaVenta < mañana && d.Producto != null && !d.Producto.EsDigital && !d.Producto.RequiereServicio)
                    .SumAsync(d => (decimal?)d.SubTotal) ?? 0m;

                var costoMercanciaVendida = await _context.DetallesVentas
                    .Where(d => d.Venta != null && d.Venta.FechaVenta >= inicioMes && d.Venta.FechaVenta < mañana)
                    .SumAsync(d => (decimal?)((d.Producto != null ? d.Producto.PrecioCosto : 0m) * d.Cantidad)) ?? 0m;

                var gastosOperativosMes = await _context.MovimientosCaja
                    .Where(m => m.Fecha >= inicioMes && m.Fecha < mañana && (m.Tipo == "Egreso" || m.Concepto == "Gasto Ordinario" || m.Concepto == "Ajuste"))
                    .SumAsync(m => (decimal?)m.Monto) ?? 0m;

                var utilidadNetaRealMes = totalVentaMes - costoMercanciaVendida - gastosOperativosMes;

                // Contadores y métricas operativas
                var ticketsAbiertos = await _context.OrdenesServicio
                    .CountAsync(o => o.Estado != "Entregado" && o.Estado != "Cancelado");

                var productosMasVendidos = await _context.DetallesVentas
                    .AsNoTracking()
                    .Where(d => d.Venta != null && d.Venta.FechaVenta >= inicioMes && d.Venta.FechaVenta < mañana && d.Producto != null)
                    .GroupBy(d => new { d.IdProducto, d.Producto!.Nombre })
                    .Select(g => new
                    {
                        Nombre = g.Key.Nombre,
                        Cantidad = g.Sum(d => d.Cantidad)
                    })
                    .OrderByDescending(x => x.Cantidad)
                    .Take(5)
                    .ToListAsync();

                var clientesNuevosMes = await _context.Clientes
                    .AsNoTracking()
                    .OrderByDescending(c => c.Id)
                    .Take(5)
                    .Select(c => new { c.Id, c.Nombre, c.Telefono })
                    .ToListAsync();

                var cantidadClientesTotales = await _context.Clientes.CountAsync();

                var productosAlertaStock = await _context.Productos
                    .CountAsync(p => p.StockActual <= p.StockMinimo);

                var renovacionesHoy = await _context.Suscripciones
                    .CountAsync(s => s.Estado == "Activa" && s.FechaVencimiento.Date == hoy);

                var renovacionesVencidas = await _context.Suscripciones
                    .CountAsync(s => s.FechaVencimiento.Date < hoy && s.Estado != "Cancelada");

                var totalCuentasPorCobrar = await _context.CuentasPorCobrar
                    .Where(c => c.Estado == "Pendiente")
                    .SumAsync(c => (decimal?)c.SaldoPendiente) ?? 0m;

                var totalCuentasPorPagar = await _context.CuentasPorPagar
                    .Where(c => c.Estado == "Pendiente")
                    .SumAsync(c => (decimal?)c.SaldoPendiente) ?? 0m;

                var listaDeudores = await _context.CuentasPorCobrar
                    .AsNoTracking()
                    .Where(c => c.Estado == "Pendiente")
                    .Select(c => new DeudorDto
                    {
                        Nombre = c.Cliente != null ? c.Cliente.Nombre : "Cliente Genérico",
                        Telefono = c.Cliente != null ? c.Cliente.Telefono : "N/A",
                        Email = c.Cliente != null ? c.Cliente.Email : "N/A",
                        Saldo = c.SaldoPendiente,
                        FechaVencimiento = c.FechaVencimiento
                    })
                    .OrderByDescending(c => c.Saldo)
                    .Take(5)
                    .ToListAsync();

                var alertas = new List<string>();
                if (productosAlertaStock > 0) 
                    alertas.Add($"Hay {productosAlertaStock} productos con stock igual o inferior al mínimo.");
                if (ticketsAbiertos > 5) 
                    alertas.Add($"Sobrecarga en taller: {ticketsAbiertos} órdenes pendientes.");
                if (totalVentaMes > 0 && (utilidadNetaRealMes / totalVentaMes) < 0.25m) 
                    alertas.Add("ALERTA CRÍTICA: El margen neto del negocio cayó por debajo del 25% deduciendo gastos de caja.");

                return Ok(new
                {
                    VentasDia = totalVentaDia,
                    VentasSemana = totalVentaSemana,
                    VentasMes = totalVentaMes,
                    UtilidadMes = utilidadNetaRealMes,
                    GastosOperativosMes = gastosOperativosMes,
                    RenovacionesHoy = renovacionesHoy,
                    RenovacionesVencidas = renovacionesVencidas,
                    TicketsAbiertos = ticketsAbiertos,
                    CantidadClientesNuevos = cantidadClientesTotales,
                    Rubros = new decimal[] { totalFisicos, totalDigitales, totalSoporte },
                    SemanaFlujo = ingresosSemana,
                    ProductosMasVendidos = productosMasVendidos,
                    UltimosClientes = clientesNuevosMes,
                    Alertas = alertas,
                    TotalCuentasPorCobrar = totalCuentasPorCobrar,
                    TotalCuentasPorPagar = totalCuentasPorPagar,
                    ListaDeudores = listaDeudores
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar el resumen del dashboard.");
                return StatusCode(500, new { mensaje = "Error interno al procesar los datos del dashboard." });
            }
        }

        // 3. GET: api/Reportes/analitica-ejecutiva
        [Authorize(Roles = "Administrador")]
        [HttpGet("analitica-ejecutiva")]
        public async Task<IActionResult> GetAnaliticaEjecutiva()
        {
            try
            {
                var ahoraNicaragua = GetNicaraguaTime();
                var inicioMes = new DateTime(ahoraNicaragua.Year, ahoraNicaragua.Month, 1);

                var gastosDetallados = await _context.MovimientosCaja
                    .AsNoTracking()
                    .Where(m => m.Fecha >= inicioMes && (m.Concepto == "Gasto Ordinario" || m.Tipo == "Egreso"))
                    .Select(m => new { m.Detalle, m.Monto })
                    .ToListAsync();

                var utilidadBruta = await _context.DetallesVentas
                    .Where(d => d.Venta != null && d.Venta.FechaVenta >= inicioMes)
                    .SumAsync(d => (decimal?)((d.PrecioUnitario - (d.Producto != null ? d.Producto.PrecioCosto : 0m)) * d.Cantidad)) ?? 0m;

                // Proyección limpia para el ranking de utilidad
                var rankingUtilidad = await _context.DetallesVentas
                    .AsNoTracking()
                    .Where(d => d.Venta != null && d.Venta.FechaVenta >= inicioMes)
                    .Select(d => new
                    {
                        NombreProducto = d.Producto != null ? d.Producto.Nombre : "Sin Producto / Genérico",
                        Cantidad = d.Cantidad,
                        PrecioUnitario = d.PrecioUnitario,
                        PrecioCosto = d.Producto != null ? d.Producto.PrecioCosto : 0m
                    })
                    .GroupBy(x => x.NombreProducto)
                    .Select(g => new
                    {
                        Servicio = g.Key,
                        UnidadesVendidas = g.Sum(x => x.Cantidad),
                        UtilidadTotal = g.Sum(x => (x.PrecioUnitario - x.PrecioCosto) * x.Cantidad)
                    })
                    .OrderByDescending(x => x.UtilidadTotal)
                    .ToListAsync();

                // === CORRECCIÓN DE LA LÍNEA 339 ===
                // 1. Obtenemos los tickets del mes proyectando solo el Tipo y el Nombre del Cliente (SQL simple)
                var ticketsRaw = await _context.TicketsSoporte
                    .AsNoTracking()
                    .Where(t => t.FechaCreacion >= inicioMes)
                    .Select(t => new
                    {
                        t.TipoTicket,
                        ClienteNombre = t.Cliente != null ? t.Cliente.Nombre : "Cliente Anónimo"
                    })
                    .ToListAsync();

                // 2. Agrupamos y obtenemos los clientes distintos en memoria (LINQ to Objects)
                var detalleProblemas = ticketsRaw
                    .GroupBy(t => t.TipoTicket)
                    .Select(g => new
                    {
                        Motivo = g.Key,
                        Frecuencia = g.Count(),
                        ClientesAfectados = g.Select(x => x.ClienteNombre).Distinct().ToList()
                    })
                    .OrderByDescending(x => x.Frecuencia)
                    .ToList();

                var listaGarantias = await _context.GarantiasTickets
                    .AsNoTracking()
                    .Where(g => g.FechaRepo >= inicioMes)
                    .Select(g => new
                    {
                        g.Motivo,
                        g.CostoReposicion,
                        Cliente = g.Cliente != null ? g.Cliente.Nombre : "Cliente Desconocido",
                        Fecha = g.FechaRepo
                    })
                    .ToListAsync();

                var renovacionesPerdidas = await _context.Suscripciones
                    .AsNoTracking()
                    .Where(s => s.Estado == "Vencida" && s.FechaVencimiento < ahoraNicaragua)
                    .Select(s => new
                    {
                        Cliente = s.Cliente != null ? s.Cliente.Nombre : "Desconocido",
                        s.NombreServicio,
                        s.FechaVencimiento
                    })
                    .ToListAsync();

                return Ok(new
                {
                    ResumenFinanciero = new
                    {
                        UtilidadBruta = utilidadBruta,
                        GastosDesglosados = gastosDetallados,
                        TotalGastos = gastosDetallados.Sum(g => g.Monto)
                    },
                    RankingServicios = rankingUtilidad,
                    ProblemasRecurrentes = detalleProblemas,
                    HistorialGarantias = listaGarantias,
                    RenovacionesPerdidas = renovacionesPerdidas
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar la analítica ejecutiva.");
                return StatusCode(500, new { mensaje = "Error interno al procesar la analítica ejecutiva." });
            }
        }

        // 4. GET: api/Reportes/indicadores
        [HttpGet("indicadores")]
        [Authorize(Roles = "Administrador,Socio")]
        public async Task<IActionResult> GetIndicadores()
        {
            try
            {
                var clientesActivos = await _context.Suscripciones
                    .Where(s => s.Estado == "Activa")
                    .Select(s => s.IdCliente)
                    .Distinct()
                    .CountAsync();

                var totalClientes = await _context.Clientes.CountAsync();
                var clientesInactivos = totalClientes - clientesActivos;

                var renovacionesExitosas = await _context.Suscripciones.CountAsync(s => s.Estado == "Activa");
                var renovacionesPerdidas = await _context.Suscripciones.CountAsync(s => s.Estado == "Cancelada" || s.Estado == "Vencida");

                var serviciosVendidos = await _context.DetallesVentas.CountAsync();

                var totalVentas = await _context.Ventas.SumAsync(v => (decimal?)v.Total) ?? 0m;
                var totalCosto = await _context.DetallesVentas
                    .SumAsync(d => (decimal?)((d.Producto != null ? d.Producto.PrecioCosto : 0m) * d.Cantidad)) ?? 0m;

                var utilidad = totalVentas - totalCosto;

                var proveedorMargen = await _context.Productos
                    .Where(p => !string.IsNullOrEmpty(p.Proveedor))
                    .GroupBy(p => p.Proveedor)
                    .Select(g => new
                    {
                        Proveedor = g.Key,
                        MargenPromedio = g.Average(p => p.PrecioVenta - p.PrecioCosto)
                    })
                    .OrderByDescending(x => x.MargenPromedio)
                    .FirstOrDefaultAsync();

                var proveedorReclamos = await _context.GarantiasTickets
                    .GroupBy(g => g.CuentaAnterior)
                    .Select(g => new { Identificador = g.Key, Total = g.Count() })
                    .OrderByDescending(x => x.Total)
                    .FirstOrDefaultAsync();

                return Ok(new
                {
                    clientesActivos,
                    clientesInactivos,
                    renovacionesExitosas,
                    renovacionesPerdidas,
                    serviciosVendidos,
                    utilidad,
                    proveedorConMayorMargen = proveedorMargen?.Proveedor ?? "N/A",
                    proveedorConMasReclamos = proveedorReclamos?.Identificador ?? "N/A"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al calcular los indicadores clave (KPIs).");
                return StatusCode(500, new { mensaje = "Error interno al calcular los indicadores." });
            }
        }
    }
}