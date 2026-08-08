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
        public async Task<IActionResult> GetReportePersonalizado(
            [FromQuery] DateTime desde, 
            [FromQuery] DateTime hasta,
            [FromQuery] int? idCliente = null,
            [FromQuery] string? rubro = null)
        {
            try
            {
                var fechaInicio = desde.Date;
                var fechaFin = hasta.Date.AddDays(1).AddTicks(-1);

                // Base Query de Ventas filtrada por Rango y Cliente opcional
                var queryVentas = _context.Ventas
                    .AsNoTracking()
                    .Where(v => v.FechaVenta >= fechaInicio && v.FechaVenta <= fechaFin);

                if (idCliente.HasValue && idCliente.Value > 0)
                {
                    queryVentas = queryVentas.Where(v => v.IdCliente == idCliente.Value);
                }

                // Base Query de Detalles de Ventas filtrada por Rango, Cliente y Rubro opcional
                var queryDetalles = _context.DetallesVentas
                    .AsNoTracking()
                    .Include(d => d.Producto)
                    .Where(d => d.Venta != null && d.Venta.FechaVenta >= fechaInicio && d.Venta.FechaVenta <= fechaFin);

                if (idCliente.HasValue && idCliente.Value > 0)
                {
                    queryDetalles = queryDetalles.Where(d => d.Venta!.IdCliente == idCliente.Value);
                }

                // Aplicar Filtro de Rubro sobre los Detalles
                if (!string.IsNullOrEmpty(rubro) && rubro != "Todos")
                {
                    switch (rubro.ToLower())
                    {
                        case "videojuegos":
                            queryDetalles = queryDetalles.Where(d => d.Producto != null && d.Producto.JuegoId != null);
                            break;
                        case "streaming":
                            queryDetalles = queryDetalles.Where(d => d.Producto != null && (d.Producto.EsSuscripcion || d.Producto.EsDigital) && d.Producto.JuegoId == null);
                            break;
                        case "tienda":
                            queryDetalles = queryDetalles.Where(d => d.Producto != null && !d.Producto.EsSuscripcion && !d.Producto.EsDigital && d.Producto.JuegoId == null);
                            break;
                    }

                    // Si se filtra por rubro, restringimos la lista de IDs de venta para los totales financieros
                    var ventasValidasIds = await queryDetalles.Select(d => d.IdVenta).Distinct().ToListAsync();
                    queryVentas = queryVentas.Where(v => ventasValidasIds.Contains(v.Id));
                }

                // --- CÁLCULO DE FINANZAS APLICANDO FILTROS ---
                var totalVentasCount = await queryVentas.CountAsync();

                var totalEfectivo = await queryVentas.Where(v => v.MetodoPago == "Efectivo").SumAsync(v => (decimal?)v.Total) ?? 0m;
                var totalTransferencia = await queryVentas.Where(v => v.MetodoPago == "Transferencia").SumAsync(v => (decimal?)v.Total) ?? 0m;
                var totalTarjeta = await queryVentas.Where(v => v.MetodoPago == "Tarjeta").SumAsync(v => (decimal?)v.Total) ?? 0m;
                var totalCredito = await queryVentas.Where(v => v.MetodoPago == "Crédito").SumAsync(v => (decimal?)v.Total) ?? 0m;

                // Movimientos de Caja generales solo se incluyen si no hay filtro de cliente ni de rubro específico
                var totalIngresosExtra = 0m;
                var totalGastosFijos = 0m;
                var totalComprasProveedores = 0m;

                if ((!idCliente.HasValue || idCliente == 0) && (string.IsNullOrEmpty(rubro) || rubro == "Todos"))
                {
                    totalIngresosExtra = await _context.MovimientosCaja
                        .Where(m => m.Fecha >= fechaInicio && m.Fecha <= fechaFin && m.Tipo == "Ingreso" && m.Concepto != "Venta" && m.Concepto != "Renovacion")
                        .SumAsync(m => (decimal?)m.Monto) ?? 0m;

                    totalGastosFijos = await _context.MovimientosCaja
                        .Where(m => m.Fecha >= fechaInicio && m.Fecha <= fechaFin && m.Tipo == "Egreso" && m.Concepto != "Compra Proveedor")
                        .SumAsync(m => (decimal?)m.Monto) ?? 0m;

                    totalComprasProveedores = await _context.MovimientosCaja
                        .Where(m => m.Fecha >= fechaInicio && m.Fecha <= fechaFin && m.Concepto == "Compra Proveedor")
                        .SumAsync(m => (decimal?)m.Monto) ?? 0m;
                }

                var granTotalFacturado = totalEfectivo + totalTransferencia + totalTarjeta;
                var balanceNetoEfectivoCaja = (granTotalFacturado + totalIngresosExtra) - (totalGastosFijos + totalComprasProveedores);

                // --- COSTOS Y UTILIDAD ---
                var costoMercanciaVendida = await queryDetalles
                    .SumAsync(d => (decimal?)((d.Producto != null ? d.Producto.PrecioCosto : 0m) * d.Cantidad)) ?? 0m;

                var utilidadBruta = granTotalFacturado - costoMercanciaVendida;
                var utilidadNetaReal = utilidadBruta - totalGastosFijos;

                var topProductos = await queryDetalles
                    .GroupBy(d => new { d.IdProducto, Nombre = d.Producto != null ? d.Producto.Nombre : "Servicio/Producto General" })
                    .Select(g => new TopProductoDto
                    {
                        Producto = g.Key.Nombre,
                        Cantidad = g.Sum(d => d.Cantidad),
                        Subtotal = g.Sum(d => d.SubTotal)
                    })
                    .OrderByDescending(x => x.Cantidad)
                    .Take(10)
                    .ToListAsync();

                var listaTransacciones = await queryVentas
                    .OrderByDescending(v => v.FechaVenta)
                    .Select(v => new TransaccionResumenDto
                    {
                        Id = v.Id,
                        IdCliente = v.IdCliente,
                        Cliente = v.Cliente != null ? v.Cliente.Nombre : "Mostrador General", 
                        Fecha = v.FechaVenta.ToString("yyyy-MM-dd HH:mm"),
                        Operador = v.Usuario != null ? v.Usuario.Nombre : "Sistema",
                        MetodoPago = v.MetodoPago,
                        Total = v.Total
                    })
                    .ToListAsync();

                var resultado = new
                {
                    Rango = $"{fechaInicio:dd/MM/yyyy} al {hasta.Date:dd/MM/yyyy}",
                    VentasTotales = totalVentasCount,
                    Finanzas = new
                    {
                        Efectivo = totalEfectivo,
                        Transferencia = totalTransferencia,
                        Tarjeta = totalTarjeta,
                        Credito = totalCredito,
                        TotalFacturado = granTotalFacturado,
                        CostoMercancia = costoMercanciaVendida,
                        UtilidadBruta = utilidadBruta,
                        GastosOperativos = totalGastosFijos,
                        UtilidadNeta = utilidadNetaReal,
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

                // CORRECCIÓN EN GetResumenDashboard:
                var gastosOperativosMes = await _context.MovimientosCaja
                    .Where(m => m.Fecha >= inicioMes && m.Fecha < mañana && m.Tipo == "Egreso" && m.Concepto != "Compra Proveedor")
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

        [Authorize(Roles = "Administrador")]
        [HttpGet("analitica-ejecutiva")]
        public async Task<IActionResult> GetAnaliticaEjecutiva(
            [FromQuery] string? tipoFiltro, 
            [FromQuery] int? mes, 
            [FromQuery] int? anio, 
            [FromQuery] DateTime? fechaInicio, 
            [FromQuery] DateTime? fechaFin)
        {
            try
            {
                var ahoraNicaragua = GetNicaraguaTime();
                DateTime inicioPeriodo;
                DateTime finPeriodo;

                switch (tipoFiltro?.ToLower())
                {
                    case "hoy":
                        inicioPeriodo = ahoraNicaragua.Date;
                        finPeriodo = inicioPeriodo.AddDays(1).AddTicks(-1);
                        break;

                    case "semana":
                        int diasLunes = ((int)ahoraNicaragua.DayOfWeek - 1 + 7) % 7;
                        inicioPeriodo = ahoraNicaragua.Date.AddDays(-diasLunes);
                        finPeriodo = inicioPeriodo.AddDays(7).AddTicks(-1);
                        break;

                    case "anio":
                        int anioFiltro = anio.HasValue && anio.Value >= 2020 ? anio.Value : ahoraNicaragua.Year;
                        inicioPeriodo = new DateTime(anioFiltro, 1, 1, 0, 0, 0);
                        finPeriodo = new DateTime(anioFiltro, 12, 31, 23, 59, 59, 999);
                        break;

                    case "rango":
                        if (!fechaInicio.HasValue || !fechaFin.HasValue)
                            return BadRequest("Debe especificar fechaInicio y fechaFin para el filtro de rango.");
                        inicioPeriodo = fechaInicio.Value.Date;
                        finPeriodo = fechaFin.Value.Date.AddDays(1).AddTicks(-1);
                        break;

                    case "mes":
                    default:
                        int mesFiltro = mes.HasValue && mes.Value >= 1 && mes.Value <= 12 ? mes.Value : ahoraNicaragua.Month;
                        int anioMes = anio.HasValue && anio.Value >= 2020 ? anio.Value : ahoraNicaragua.Year;
                        inicioPeriodo = new DateTime(anioMes, mesFiltro, 1);
                        finPeriodo = inicioPeriodo.AddMonths(1).AddTicks(-1);
                        break;
                }

                var gastosDetallados = await _context.MovimientosCaja
                    .AsNoTracking()
                    .Where(m => m.Fecha >= inicioPeriodo && m.Fecha <= finPeriodo && (m.Concepto == "Gasto Ordinario" || m.Tipo == "Egreso"))
                    .Select(m => new { detalle = m.Detalle, monto = m.Monto })
                    .ToListAsync();

                var utilidadBruta = await _context.DetallesVentas
                    .Where(d => d.Venta != null && d.Venta.FechaVenta >= inicioPeriodo && d.Venta.FechaVenta <= finPeriodo)
                    .SumAsync(d => (decimal?)((d.PrecioUnitario - (d.Producto != null ? d.Producto.PrecioCosto : 0m)) * d.Cantidad)) ?? 0m;

                var totalGastos = gastosDetallados.Sum(g => g.monto);
                var utilidadNeta = utilidadBruta - totalGastos;

                var rankingUtilidad = await _context.DetallesVentas
                    .AsNoTracking()
                    .Where(d => d.Venta != null && d.Venta.FechaVenta >= inicioPeriodo && d.Venta.FechaVenta <= finPeriodo)
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

                var listaGarantias = await _context.GarantiasTickets
                    .AsNoTracking()
                    .Where(g => g.FechaRepo >= inicioPeriodo && g.FechaRepo <= finPeriodo)
                    .Select(g => new { g.CostoReposicion, g.Motivo, Fecha = g.FechaRepo })
                    .ToListAsync();

                var renovacionesPerdidas = await _context.Suscripciones
                    .AsNoTracking()
                    .Where(s => s.Estado == "Vencida" && s.FechaVencimiento >= inicioPeriodo && s.FechaVencimiento <= finPeriodo)
                    .Select(s => new
                    {
                        nombre = s.Cliente != null ? s.Cliente.Nombre : "Desconocido",
                        s.NombreServicio,
                        s.FechaVencimiento
                    })
                    .ToListAsync();

                return Ok(new
                {
                    rango = $"{inicioPeriodo:dd/MM/yyyy} al {finPeriodo:dd/MM/yyyy}",
                    resumenFinanciero = new
                    {
                        utilidadBruta = utilidadBruta,
                        gastosTotales = totalGastos,
                        utilidadNeta = utilidadNeta,
                        gastosDesglosados = gastosDetallados
                    },
                    rankingServicios = rankingUtilidad,
                    historialGarantias = listaGarantias,
                    renovacionesPerdidas = renovacionesPerdidas
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