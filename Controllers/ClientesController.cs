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

        public ClientesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Clientes
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Cliente>>> GetClientes()
        {
            return await _context.Clientes.OrderBy(c => c.Nombre).ToListAsync();
        }

        // GET: api/Clientes/{id}/historial (Módulo CRM de búsqueda e historia completa)
        [HttpGet("{id}/historial")]
        public async Task<IActionResult> GetHistorialCliente(int id)
        {
            // 1. Validar existencia del cliente
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null) return NotFound("Cliente no encontrado.");

            // 2. Extraer Historial de Compras detallado (Físicas, Digitales y Liquidaciones de Taller)
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
                        // Si es un servicio o recarga, aquí vienen IDs de Free Fire, números, o el dispositivo del taller
                        d.MetadataDigital 
                    })
                })
                .ToListAsync();

            // 3. Calcular Total Gastado histórico en caliente
            decimal totalGastado = compras.Sum(c => c.Total);

            // 4. Consultar Cuentas por Cobrar (Detección de deudas y morosidad)
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
                    EsVencida = cxc.FechaVencimiento < DateTime.UtcNow && cxc.SaldoPendiente > 0
                })
                .ToListAsync();

            // Lógica de inyección de etiquetas dinámicas (Moroso) sin alterar físicamente la BD de forma estática
            var etiquetasLista = string.IsNullOrWhiteSpace(cliente.Etiquetas) 
                ? new List<string>() 
                : cliente.Etiquetas.Split(',').Select(t => t.Trim()).ToList();

            // Si tiene deudas vencidas o cuentas pendientes con fecha expirada, se le anexa "Moroso" en caliente
            if (deudas.Any(d => d.EsVencida || (d.Estado == "Pendiente" && d.FechaVencimiento < DateTime.UtcNow)))
            {
                if (!etiquetasLista.Contains("Moroso")) etiquetasLista.Add("Moroso");
            }

            // 5. Consultar Órdenes de Servicio del Taller Técnico
            var ordenesTaller = await _context.OrdenesServicio
                .Where(o => o.IdCliente == id)
                .OrderByDescending(o => o.FechaIngreso)
                .ToListAsync();

            // 6. Consultar Suscripciones recurrentes (Netflix, Licencias, etc.)
            var todasSuscripciones = await _context.Suscripciones
                .Where(s => s.IdCliente == id)
                .OrderByDescending(s => s.FechaVencimiento)
                .ToListAsync();

            // 7. Segmentación de Servicios Activos
            // Un servicio está ACTIVO si es un equipo en taller sin entregar O una suscripción vigente
            var serviciosActivos = new
            {
                TallerEquiposEnRevision = ordenesTaller
                    .Where(o => o.Estado == "Recibido" || o.Estado == "En Revisión" || o.Estado == "Listo")
                    .Select(o => new { o.Id, o.Dispositivo, o.Diagnostico, o.Estado, o.FechaIngreso }),

                SuscripcionesVigentes = todasSuscripciones
                    .Where(s => s.Estado == "Activa" && s.FechaVencimiento >= DateTime.UtcNow)
                    .Select(s => new { s.Id, s.NombreServicio, s.TipoSuscripcion, s.FechaVencimiento, s.DetallesCredenciales })
            };

            // 8. Segmentación de Servicios Vencidos / Históricos
            // Un servicio está VENCIDO/HISTÓRICO si el equipo ya se entregó O la suscripción expiró/se canceló
            var serviciosVencidos = new
            {
                TallerEquiposEntregados = ordenesTaller
                    .Where(o => o.Estado == "Entregado")
                    .Select(o => new { o.Id, o.Dispositivo, o.FechaEntrega, o.Notas }),

                SuscripcionesExpiradas = todasSuscripciones
                    .Where(s => s.Estado == "Cancelada" || s.Estado == "Vencida" || s.FechaVencimiento < DateTime.UtcNow)
                    .Select(s => new { s.Id, s.NombreServicio, s.TipoSuscripcion, s.FechaVencimiento, s.Estado })
            };

            // 9. Retorno unificado de la estructura completa del CRM
            return Ok(new
            {
                Cliente = new
                {
                    cliente.Id,
                    cliente.Nombre,
                    cliente.Telefono, // WhatsApp
                    cliente.Email,
                    cliente.FechaRegistro,
                    cliente.Observaciones,
                    Etiquetas = string.Join(", ", etiquetasLista), // Retorna la lista con "Moroso" si aplica
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

            cliente.FechaRegistro = DateTime.Now;
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
            // 1. Validar existencia del cliente
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null) return NotFound("Cliente no encontrado.");

            // 2. Verificar si tiene ventas registradas y traer los datos básicos de estas
            var ventasAsociadas = await _context.Ventas
                .Where(v => v.IdCliente == id)
                .OrderByDescending(v => v.FechaVenta)
                .Select(v => new 
                {
                    v.Id,
                    Fecha = v.FechaVenta,
                    v.Total,
                    v.MetodoPago
                })
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

            // 3. Si no tiene ventas, proceder con el borrado físico de forma segura
            _context.Clientes.Remove(cliente);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}