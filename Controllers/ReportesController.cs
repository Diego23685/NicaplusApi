using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization; 

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReportesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private static readonly TimeZoneInfo NicaraguaZone = TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");

        public ReportesController(ApplicationDbContext context)
        {
            _context = context;
        }

        private DateTime GetNicaraguaTime()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, NicaraguaZone);
        }

        [Authorize(Roles = "Administrador,Socio")]
        [HttpGet("personalizado")]
        public async Task<IActionResult> GetReportePersonalizado([FromQuery] DateTime desde, [FromQuery] DateTime hasta)
        {
            // Forzamos que los límites operen bajo las fechas absolutas solicitadas
            var fechaInicio = desde.Date;
            var fechaFin = hasta.Date.AddDays(1).AddTicks(-1);

            var ventas = await _context.Ventas
                .Include(v => v.Usuario)
                .Where(v => v.FechaVenta >= fechaInicio && v.FechaVenta <= fechaFin)
                .OrderByDescending(v => v.FechaVenta)
                .ToListAsync();

            var movimientosCaja = await _context.MovimientosCaja
                .Where(m => m.Fecha >= fechaInicio && m.Fecha <= fechaFin)
                .ToListAsync();

            var totalEfectivo = ventas.Where(v => v.MetodoPago == "Efectivo").Sum(v => v.Total);
            var totalTransferencia = ventas.Where(v => v.MetodoPago == "Transferencia").Sum(v => v.Total);
            var totalTarjeta = ventas.Where(v => v.MetodoPago == "Tarjeta").Sum(v => v.Total);
            
            var totalIngresosExtra = movimientosCaja.Where(m => m.Tipo == "Ingreso" && m.Concepto != "Venta").Sum(m => m.Monto);
            var totalGastosFijos = movimientosCaja.Where(m => m.Concepto == "Gasto Ordinario" || m.Concepto == "Ajuste").Sum(m => m.Monto);
            var totalComprasProveedores = movimientosCaja.Where(m => m.Concepto == "Compra Proveedor").Sum(m => m.Monto);

            var granTotalFacturado = totalEfectivo + totalTransferencia + totalTarjeta;
            var balanceNetoEfectivoCaja = (granTotalFacturado + totalIngresosExtra) - (totalGastosFijos + totalComprasProveedores);

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

            var listaTransacciones = ventas.Select(v => new {
                v.Id,
                Fecha = v.FechaVenta.ToString("yyyy-MM-dd HH:mm"),
                Operador = v.Usuario?.Nombre ?? "Sistema",
                v.MetodoPago,
                v.Total
            }).ToList();

            return Ok(new
            {
                Rango = $"{fechaInicio:dd/MM/yyyy} al {hasta.Date:dd/MM/yyyy}",
                VentasTotales = ventas.Count,
                Finanzas = new { 
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
            });
        }

        [HttpGet("resumen-dashboard")]
        public async Task<IActionResult> GetResumenDashboard()
        {
            // CORREGIDO: Todo el cálculo del tiempo corre con el huso de Nicaragua
            var hoyNicaragua = GetNicaraguaTime();
            var hoy = hoyNicaragua.Date; 
            var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);

            int diasDesdeLunes = ((int)hoy.DayOfWeek - 1 + 7) % 7;
            var inicioSemana = hoy.AddDays(-diasDesdeLunes);
            var finSemana = inicioSemana.AddDays(7);
            var mañana = hoy.AddDays(1);

            // Filtrados directos y acotados en Base de Datos para cuidar la RAM
            var ventasMes = await _context.Ventas
                .Where(v => v.FechaVenta >= inicioMes && v.FechaVenta < mañana)
                .ToListAsync();

            var movimientosCajaMes = await _context.MovimientosCaja
                .Where(m => m.Fecha >= inicioMes && m.Fecha < mañana)
                .ToListAsync();

            var ventasSemana = ventasMes.Where(v => v.FechaVenta >= inicioSemana && v.FechaVenta < finSemana).ToList();
            var ventasDia = ventasMes.Where(v => v.FechaVenta >= hoy && v.FechaVenta < mañana).ToList();

            var totalVentaDia = ventasDia.Sum(v => v.Total);
            var totalVentaSemana = ventasSemana.Sum(v => v.Total);
            var totalVentaMes = ventasMes.Sum(v => v.Total);

            var ingresosSemana = new decimal[7];
            foreach (var venta in ventasSemana)
            {
                int indiceDia = ((int)venta.FechaVenta.DayOfWeek - 1 + 7) % 7;
                ingresosSemana[indiceDia] += venta.Total;
            }

            var detallesVentasMes = await _context.DetallesVentas
                .Include(d => d.Producto)
                .Where(d => d.Venta!.FechaVenta >= inicioMes && d.Venta!.FechaVenta < mañana)
                .ToListAsync();

            var totalDigitales = detallesVentasMes.Where(d => d.Producto != null && d.Producto.EsDigital).Sum(d => d.SubTotal);
            var totalSoporte = detallesVentasMes.Where(d => d.Producto != null && d.Producto.RequiereServicio).Sum(d => d.SubTotal);
            var totalFisicos = detallesVentasMes.Where(d => d.Producto != null && !d.Producto.EsDigital && !d.Producto.RequiereServicio).Sum(d => d.SubTotal);

            var costoMercanciaVendida = detallesVentasMes.Sum(d => (d.Producto?.PrecioCosto ?? 0) * d.Cantidad);

            var gastosOperativosMes = movimientosCajaMes
                .Where(m => m.Concepto == "Gasto Ordinario" || m.Concepto == "Ajuste")
                .Sum(m => m.Monto);

            var utilidadNetaRealMes = totalVentaMes - costoMercanciaVendida - gastosOperativosMes;

            var ticketsAbiertos = await _context.OrdenesServicio
                .CountAsync(o => o.Estado != "Entregado" && o.Estado != "Cancelado");

            var productosMasVendidos = detallesVentasMes
                .Where(d => d.Producto != null)
                .GroupBy(d => new { d.IdProducto, d.Producto!.Nombre })
                .Select(g => new
                {
                    Nombre = g.Key.Nombre,
                    Cantidad = g.Sum(d => d.Cantidad)
                })
                .OrderByDescending(x => x.Cantidad)
                .Take(5)
                .ToList();

            var clientesNuevosMes = await _context.Clientes
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
                .SumAsync(c => c.SaldoPendiente);

            var totalCuentasPorPagar = await _context.CuentasPorPagar
                .Where(c => c.Estado == "Pendiente")
                .SumAsync(c => c.SaldoPendiente);

            var listaDeudores = await _context.CuentasPorCobrar
                .Where(c => c.Estado == "Pendiente")
                .Include(c => c.Cliente)
                .Select(c => new {
                    Nombre = c.Cliente != null ? c.Cliente.Nombre : "Cliente Genérico",
                    Telefono = c.Cliente != null ? c.Cliente.Telefono : "N/A",
                    Email = c.Cliente != null ? c.Cliente.Email : "N/A",
                    Saldo = c.SaldoPendiente,
                    Vence = c.FechaVencimiento
                })
                .OrderByDescending(c => c.Saldo)
                .Take(5)
                .ToListAsync();

            var alertas = new List<string>();
            if (productosAlertaStock > 0) alertas.Add($"Hay {productosAlertaStock} productos con stock igual o inferior al mínimo.");
            if (ticketsAbiertos > 5) alertas.Add($"Sobrecarga en taller: {ticketsAbiertos} órdenes pendientes.");
            if (totalVentaMes > 0 && (utilidadNetaRealMes / totalVentaMes) < 0.25m) alertas.Add("ALERTA CRÍTICA: El margen neto del negocio cayó por debajo del 25% deduciendo gastos de caja.");

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

        [Authorize(Roles = "Administrador")]
        [HttpGet("analitica-ejecutiva")]
        public async Task<IActionResult> GetAnaliticaEjecutiva()
        {
            var ahoraNicaragua = GetNicaraguaTime();
            var inicioMes = new DateTime(ahoraNicaragua.Year, ahoraNicaragua.Month, 1);
            
            var ventasMes = await _context.Ventas.Include(v => v.Detalles).ThenInclude(d => d.Producto)
                .Where(v => v.FechaVenta >= inicioMes).ToListAsync();
            
            var gastosDetallados = await _context.MovimientosCaja
                .Where(m => m.Fecha >= inicioMes && m.Concepto == "Gasto Ordinario")
                .Select(m => new { m.Detalle, m.Monto })
                .ToListAsync();

            var rankingUtilidad = ventasMes.SelectMany(v => v.Detalles)
                .GroupBy(d => d.Producto?.Nombre ?? "Sin Producto")
                .Select(g => new { 
                    Servicio = g.Key, 
                    UnidadesVendidas = g.Sum(d => d.Cantidad),
                    UtilidadTotal = g.Sum(d => (d.PrecioUnitario - (d.Producto?.PrecioCosto ?? 0)) * d.Cantidad) 
                })
                .OrderByDescending(x => x.UtilidadTotal)
                .ToList();

            var detalleProblemas = await _context.TicketsSoporte
                .Include(t => t.Cliente)
                .Where(t => t.FechaCreacion >= inicioMes)
                .GroupBy(t => t.TipoTicket)
                .Select(g => new {
                    Motivo = g.Key,
                    Frecuencia = g.Count(),
                    ClientesAfectados = g.Select(t => t.Cliente!.Nombre).Distinct().ToList()
                })
                .OrderByDescending(x => x.Frecuencia)
                .ToListAsync();

            var listaGarantias = await _context.GarantiasTickets
                .Include(g => g.Cliente)
                .Where(g => g.FechaRepo >= inicioMes)
                .Select(g => new {
                    g.Motivo,
                    g.CostoReposicion,
                    Cliente = g.Cliente!.Nombre,
                    Fecha = g.FechaRepo
                })
                .ToListAsync();

            return Ok(new
            {
                ResumenFinanciero = new {
                    UtilidadBruta = ventasMes.SelectMany(v => v.Detalles).Sum(d => (d.PrecioUnitario - (d.Producto?.PrecioCosto ?? 0)) * d.Cantidad),
                    GastosDesglosados = gastosDetallados,
                    TotalGastos = gastosDetallados.Sum(g => g.Monto)
                },
                RankingServicios = rankingUtilidad,
                ProblemasRecurrentes = detalleProblemas,
                HistorialGarantias = listaGarantias,
                RenovacionesPerdidas = await _context.Suscripciones
                    .Include(s => s.Cliente)
                    .Where(s => s.Estado == "Vencida" && s.FechaVencimiento < ahoraNicaragua) // Corregido huso horario
                    .Select(s => new { s.Cliente!.Nombre, s.NombreServicio, s.FechaVencimiento })
                    .ToListAsync()
            });
        }

        [HttpGet("indicadores")]
        [Authorize(Roles = "Administrador,Socio")]
        public async Task<IActionResult> GetIndicadores()
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
            
            var totalVentas = await _context.Ventas.SumAsync(v => v.Total);
            var totalCosto = await _context.DetallesVentas
                .Include(d => d.Producto)
                .SumAsync(d => (d.Producto != null ? d.Producto.PrecioCosto : 0) * d.Cantidad);
            
            var utilidad = totalVentas - totalCosto;

            var proveedorMargen = await _context.Productos
                .Where(p => !string.IsNullOrEmpty(p.Proveedor))
                .GroupBy(p => p.Proveedor)
                .Select(g => new {
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

            return Ok(new {
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
    }
}