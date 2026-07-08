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
    public class ClientesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        
        // Zona horaria estándar para Nicaragua
        private static readonly TimeZoneInfo NicaraguaZone = TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");

        public ClientesController(ApplicationDbContext context)
        {
            _context = context;
        }

        private DateTime GetNicaraguaTime()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, NicaraguaZone);
        }

        // GET: api/Clientes
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Cliente>>> GetClientes()
        {
            return await _context.Clientes.OrderBy(c => c.Nombre).ToListAsync();
        }

        // GET: api/Clientes/{id}/historial
        [HttpGet("{id}/historial")]
        public async Task<IActionResult> GetHistorialCliente(int id)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null) return NotFound("Cliente no encontrado.");

            var ahoraNicaragua = GetNicaraguaTime();

            // 1. Historial de Compras detallado
            var compras = await _context.Ventas
                .Where(v => v.IdCliente == id)
                .OrderByDescending(v => v.FechaVenta)
                .Select(v => new 
                { 
                    v.Id, 
                    Fecha = v.FechaVenta, 
                    v.Total,
                    v.MetodoPago,
                    Detalles = v.Detalles.Select(d => new 
                    { 
                        d.Cantidad, 
                        d.PrecioUnitario, 
                        d.SubTotal,
                        d.MetadataDigital 
                    })
                })
                .ToListAsync();

            decimal totalGastado = compras.Sum(c => c.Total);

            // 2. Cuentas por Cobrar (Corregido con la hora de Nicaragua)
            var deudas = await _context.CuentasPorCobrar
                .Where(cxc => cxc.IdCliente == id)
                .OrderByDescending(cxc => cxc.FechaVencimiento)
                .Select(cxc => new 
                {
                    cxc.Id,
                    cxc.IdVenta,
                    cxc.MontoTotal,
                    cxc.SaldoPendiente,
                    cxc.FechaEmision,
                    cxc.FechaVencimiento,
                    cxc.Estado,
                    EsVencida = cxc.FechaVencimiento < ahoraNicaragua && cxc.SaldoPendiente > 0
                })
                .ToListAsync();

            var etiquetasLista = string.IsNullOrWhiteSpace(cliente.Etiquetas) 
                ? new List<string>() 
                : cliente.Etiquetas.Split(',').Select(t => t.Trim()).ToList();

            if (deudas.Any(d => d.EsVencida || (d.Estado == "Pendiente" && d.FechaVencimiento < ahoraNicaragua)))
            {
                if (!etiquetasLista.Contains("Moroso")) etiquetasLista.Add("Moroso");
            }

            // 3. Órdenes de Servicio del Taller (Filtrado optimizado en BD)
            var ordenesTaller = await _context.OrdenesServicio
                .Where(o => o.IdCliente == id)
                .OrderByDescending(o => o.FechaIngreso)
                .ToListAsync();

            // 4. Suscripciones recurrentes (Filtrado optimizado en BD)
            var todasSuscripciones = await _context.Suscripciones
                .Include(s => s.PerfilCuenta)
                .Where(s => s.IdCliente == id)
                .OrderByDescending(s => s.FechaVencimiento)
                .ToListAsync();

            // 5. Segmentación en memoria de listas ya optimizadas
            var serviciosActivos = new
            {
                TallerEquiposEnRevision = ordenesTaller
                    .Where(o => o.Estado == "Recibido" || o.Estado == "En Revisión" || o.Estado == "Listo")
                    .Select(o => new { o.Id, o.Dispositivo, o.Diagnostico, o.Estado, o.FechaIngreso }),

                SuscripcionesVigentes = todasSuscripciones
                    .Where(s => s.Estado == "Activa" && s.FechaVencimiento >= ahoraNicaragua)
                    .Select(s => new
                    {
                        s.Id,
                        s.NombreServicio,
                        s.TipoSuscripcion,
                        s.FechaVencimiento,
                        DetallesCredenciales = s.PerfilCuenta != null
                            ? $"PERFIL: {s.PerfilCuenta.NombrePerfil} | PIN: {s.PerfilCuenta.PIN} | Acceso: {s.PerfilCuenta.CorreoCuenta} / {s.PerfilCuenta.PasswordCuenta}"
                            : s.DetallesCredenciales
                    })
            };

            var serviciosVencidos = new
            {
                TallerEquiposEntregados = ordenesTaller
                    .Where(o => o.Estado == "Entregado")
                    .Select(o => new { o.Id, o.Dispositivo, o.FechaEntrega, o.Notas }),

                SuscripcionesExpiradas = todasSuscripciones
                    .Where(s => s.Estado == "Cancelada" || s.Estado == "Vencida" || s.FechaVencimiento < ahoraNicaragua)
                    .Select(s => new { s.Id, s.NombreServicio, s.TipoSuscripcion, s.FechaVencimiento, s.Estado })
            };

            return Ok(new
            {
                Cliente = new
                {
                    cliente.Id,
                    cliente.Nombre,
                    cliente.Telefono,
                    cliente.Email,
                    cliente.FechaRegistro,
                    cliente.Observaciones,
                    Etiquetas = string.Join(", ", etiquetasLista),
                    cliente.PuntosAcumulados
                },
                TotalGastado = totalGastado,
                HistorialCompras = compras,
                HistorialDeudas = deudas,
                ServiciosActivos = serviciosActivos,
                ServiciosVencidos = serviciosVencidos
            });
        }

        // POST: api/Clientes
        [HttpPost]
        public async Task<ActionResult<Cliente>> CrearCliente([FromBody] Cliente cliente)
        {
            if (string.IsNullOrWhiteSpace(cliente.Nombre) || string.IsNullOrWhiteSpace(cliente.Telefono))
            {
                return BadRequest("El nombre y el teléfono son campos obligatorios.");
            }

            var clienteExistente = await _context.Clientes.FirstOrDefaultAsync(c => c.Telefono == cliente.Telefono);
            if (clienteExistente != null) return Ok(clienteExistente);

            // Corregido: Usa la hora oficial del negocio (Nicaragua), no la del servidor de EE.UU.
            cliente.FechaRegistro = GetNicaraguaTime();
            cliente.PuntosAcumulados = 0;

            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetClientes), new { id = cliente.Id }, cliente);
        }

        // PUT: api/Clientes/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> ActualizarCliente(int id, [FromBody] Cliente cliente)
        {
            if (id != cliente.Id) return BadRequest("El ID del cliente no coincide.");
            if (string.IsNullOrWhiteSpace(cliente.Nombre) || string.IsNullOrWhiteSpace(cliente.Telefono)) return BadRequest("Campos obligatorios vacíos.");

            _context.Entry(cliente).State = EntityState.Modified;
            _context.Entry(cliente).Property(x => x.FechaRegistro).IsModified = false; 

            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Clientes.AnyAsync(c => c.Id == id)) return NotFound();
                throw;
            }
            return NoContent();
        }

        // DELETE: api/Clientes/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> EliminarCliente(int id)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null) return NotFound("Cliente no encontrado.");

            var ventasAsociadas = await _context.Ventas
                .Where(v => v.IdCliente == id)
                .OrderByDescending(v => v.FechaVenta)
                .Select(v => new { v.Id, Fecha = v.FechaVenta, v.Total, v.MetodoPago })
                .ToListAsync();

            if (ventasAsociadas.Any())
            {
                return BadRequest(new
                {
                    Mensaje = "No se puede eliminar el cliente porque tiene ventas registradas en el sistema.",
                    CantidadVentas = ventasAsociadas.Count,
                    Ventas = ventasAsociadas
                });
            }

            _context.Clientes.Remove(cliente);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}