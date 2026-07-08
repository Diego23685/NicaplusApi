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
        
        // Zona horaria estándar para Nicaragua (CST - UTC-6)
        private static readonly TimeZoneInfo NicaraguaZone = TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");

        public CajaController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Función auxiliar para obtener la hora exacta de Nicaragua
        private DateTime GetNicaraguaTime()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, NicaraguaZone);
        }

        // GET: api/Caja/movimientos
        [HttpGet("movimientos")]
        public async Task<ActionResult<IEnumerable<MovimientoCaja>>> GetMovimientos()
        {
            return Ok(await _context.MovimientosCaja.OrderByDescending(m => m.Fecha).ToListAsync());
        }

        // POST: api/Caja/movimientos
        [HttpPost("movimientos")]
        public async Task<ActionResult<MovimientoCaja>> Post([FromBody] MovimientoCaja movimiento)
        {
            // Si el frontend no manda fecha, se le asigna la hora exacta de Nicaragua, no UTC ni EE.UU.
            if (movimiento.Fecha == default) 
            {
                movimiento.Fecha = GetNicaraguaTime();
            }
            else 
            {
                // Si el frontend envía la fecha manual (p.ej. "2026-07-07"), aseguramos que se tome como tal
                movimiento.Fecha = movimiento.Fecha.Date; 
            }

            _context.MovimientosCaja.Add(movimiento);
            await _context.SaveChangesAsync();
            return Ok(movimiento);
        }

        // GET: api/Caja/reporte-utilidades
        [HttpGet("reporte-utilidades")]
        public async Task<IActionResult> GetReporteUtilidades()
        {
            // 1. Definir los puntos cronológicos basados estrictamente en Nicaragua
            var hoyNicaragua = GetNicaraguaTime().Date;
            var inicioMesNicaragua = new DateTime(hoyNicaragua.Year, hoyNicaragua.Month, 1);
            var mañanaNicaragua = hoyNicaragua.AddDays(1);

            // 2. FILTRAR DIRECTO EN BASE DE DATOS (Eficiencia de rendimiento crítico)
            var movimientosMes = await _context.MovimientosCaja
                .Where(m => m.Fecha >= inicioMesNicaragua && m.Fecha < mañanaNicaragua)
                .ToListAsync();

            var ventasMes = await _context.Ventas
                .Include(v => v.Detalles)
                .ThenInclude(d => d.Producto)
                .Where(v => v.FechaVenta >= inicioMesNicaragua && v.FechaVenta < mañanaNicaragua)
                .ToListAsync();

            // 3. CÁLCULOS DEL DÍA (Filtrado en memoria sobre el subset optimizado)
            var movsHoy = movimientosMes.Where(m => m.Fecha.Date == hoyNicaragua).ToList();
            decimal ingresosDia = movsHoy.Where(m => m.Tipo == "Ingreso").Sum(m => m.Monto);
            decimal egresosDia = movsHoy.Where(m => m.Tipo == "Egreso").Sum(m => m.Monto);
            decimal comprasDia = movsHoy.Where(m => m.Concepto == "Compra Proveedor").Sum(m => m.Monto);
            decimal gastosDia = movsHoy.Where(m => m.Concepto == "Gasto Ordinario").Sum(m => m.Monto);

            decimal utilidadDiaria = ventasMes.Where(v => v.FechaVenta.Date == hoyNicaragua)
                .SelectMany(v => v.Detalles)
                .Sum(d => d.Producto != null ? (d.PrecioUnitario - d.Producto.PrecioCosto) * d.Cantidad : d.SubTotal) - gastosDia;

            // 4. CÁLCULOS DEL MES
            decimal ingresosMes = movimientosMes.Where(m => m.Tipo == "Ingreso").Sum(m => m.Monto);
            decimal egresosMes = movimientosMes.Where(m => m.Tipo == "Egreso").Sum(m => m.Monto);
            decimal comprasMes = movimientosMes.Where(m => m.Concepto == "Compra Proveedor").Sum(m => m.Monto);
            decimal gastosMes = movimientosMes.Where(m => m.Concepto == "Gasto Ordinario").Sum(m => m.Monto);

            decimal utilidadMensual = ventasMes
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