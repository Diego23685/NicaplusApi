using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CajaController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CajaController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Caja/movimientos
        [HttpGet("movimientos")]
        public async Task<ActionResult<IEnumerable<MovimientoCaja>>> GetMovimientos()
        {
            return Ok(await _context.MovimientosCaja.OrderByDescending(m => m.Fecha).ToListAsync());
        }

        // POST: api/Caja/movimientos (Para registrar Gastos o Ingresos manuales)
        [HttpPost("movimientos")]
        public async Task<ActionResult<MovimientoCaja>> Post([FromBody] MovimientoCaja movimiento)
        {
            if (movimiento.Fecha == default) movimiento.Fecha = DateTime.UtcNow;
            _context.MovimientosCaja.Add(movimiento);
            await _context.SaveChangesAsync();
            return Ok(movimiento);
        }

        // GET: api/Caja/reporte-utilidades
        [HttpGet("reporte-utilidades")]
        public async Task<IActionResult> GetReporteUtilidades()
        {
            var hoy = DateTime.Now.Date;
            var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);

            var movimientos = await _context.MovimientosCaja.ToListAsync();
            var ventasDetalles = await _context.Ventas.Include(v => v.Detalles).ThenInclude(d => d.Producto).ToListAsync();

            // 1. CÁLCULOS DEL DÍA
            var movsHoy = movimientos.Where(m => m.Fecha.Date == hoy).ToList();
            decimal ingresosDia = movsHoy.Where(m => m.Tipo == "Ingreso").Sum(m => m.Monto);
            decimal egresosDia = movsHoy.Where(m => m.Tipo == "Egreso").Sum(m => m.Monto);
            decimal comprasDia = movsHoy.Where(m => m.Concepto == "Compra Proveedor").Sum(m => m.Monto);
            decimal gastosDia = movsHoy.Where(m => m.Concepto == "Gasto Ordinario").Sum(m => m.Monto);

            // Utilidad Real Diaria (PrecioVenta - PrecioCosto) de lo facturado hoy menos gastos
            decimal utilidadDiaria = ventasDetalles.Where(v => v.FechaVenta.Date == hoy)
                .SelectMany(v => v.Detalles)
                .Sum(d => d.Producto != null ? (d.PrecioUnitario - d.Producto.PrecioCosto) * d.Cantidad : d.SubTotal) - gastosDia;

            // 2. CÁLCULOS DEL MES
            var movsMes = movimientos.Where(m => m.Fecha.Date >= inicioMes).ToList();
            decimal ingresosMes = movsMes.Where(m => m.Tipo == "Ingreso").Sum(m => m.Monto);
            decimal egresosMes = movsMes.Where(m => m.Tipo == "Egreso").Sum(m => m.Monto);
            decimal comprasMes = movsMes.Where(m => m.Concepto == "Compra Proveedor").Sum(m => m.Monto);
            decimal gastosMes = movsMes.Where(m => m.Concepto == "Gasto Ordinario").Sum(m => m.Monto);

            decimal utilidadMensual = ventasDetalles.Where(v => v.FechaVenta.Date >= inicioMes)
                .SelectMany(v => v.Detalles)
                .Sum(d => d.Producto != null ? (d.PrecioUnitario - d.Producto.PrecioCosto) * d.Cantidad : d.SubTotal) - gastosMes;

            return Ok(new
            {
                Dia = new { Ingresos = ingresosDia, Egresos = egresosDia, Compras = comprasDia, Gastos = gastosDia, Utilidad = utilidadDiaria },
                Mes = new { Ingresos = ingresosMes, Egresos = egresosMes, Compras = comprasMes, Gastos = gastosMes, Utilidad = utilidadMensual }
            });
        }
    }
}