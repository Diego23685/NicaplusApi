using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BusquedaController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public BusquedaController(ApplicationDbContext context) => _context = context;

        [HttpGet("universal")]
        public async Task<IActionResult> Buscar([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return BadRequest("Consulta vacía.");
            query = query.ToLower().Trim();

            // CASO 1: Búsqueda de Clientes (Muestra historial completo del cliente)
            var clientes = await _context.Clientes
                .Where(c => c.Nombre.ToLower().Contains(query) || c.Telefono.Contains(query))
                .Select(c => new {
                    Tipo = "Cliente",
                    c.Nombre,
                    c.Telefono,
                    HistorialCompras = _context.Ventas.Where(v => v.IdCliente == c.Id).ToList(),
                    ServiciosActivos = _context.Suscripciones.Where(s => s.IdCliente == c.Id && s.Estado == "Activa").ToList()
                }).ToListAsync();

            // CASO 2: Búsqueda de Cuentas / Perfiles (Muestra cuentas ligadas al término, ej: "Netflix")
            var perfilesCuentas = await _context.PerfilesCuentas
                .Include(p => p.Producto)
                .Where(p => p.Producto!.Nombre.ToLower().Contains(query) || p.CorreoCuenta.ToLower().Contains(query))
                .Select(p => new {
                    Tipo = "Cuenta/Perfil",
                    Servicio = p.Producto!.Nombre,
                    p.NombrePerfil,
                    p.CorreoCuenta,
                    p.Ocupado,
                    Clave = p.PasswordCuenta,
                    p.PIN
                }).ToListAsync();

            // Unificar resultados en una sola respuesta estructurada para el cliente
            return Ok(new {
                clientes,
                cuentas = perfilesCuentas
            });
        }
    }
}