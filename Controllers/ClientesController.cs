using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.DTOs;
using NicaplusApi.Models;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Protegemos el módulo de datos personales de clientes
    public class ClientesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ClientesController> _logger;

        public ClientesController(
            ApplicationDbContext context,
            ILogger<ClientesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene la zona horaria de Nicaragua compatible con Windows y Linux/Docker.
        /// </summary>
        private static TimeZoneInfo GetNicaraguaTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/Managua");
            }
        }

        private DateTime GetNicaraguaTime()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, GetNicaraguaTimeZone());
        }

        // GET: api/Clientes
        [HttpGet]
        public async Task<IActionResult> GetClientes()
        {
            try
            {
                var clientes = await _context.Clientes
                    .AsNoTracking()
                    .OrderBy(c => c.Nombre)
                    .Select(c => new ClienteResponseDto
                    {
                        Id = c.Id,
                        Nombre = c.Nombre,
                        Telefono = c.Telefono,
                        Email = c.Email,
                        FechaRegistro = c.FechaRegistro,
                        Observaciones = c.Observaciones,
                        Etiquetas = c.Etiquetas,
                        PuntosAcumulados = c.PuntosAcumulados,
                        Activo = c.Activo,
                        UltimoAcceso = c.UltimoAcceso
                    })
                    .ToListAsync();

                return Ok(clientes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener la lista de clientes.");
                return StatusCode(500, new { mensaje = "Error interno al consultar la lista de clientes." });
            }
        }

        // GET: api/Clientes/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var c = await _context.Clientes
                    .AsNoTracking()
                    .Where(x => x.Id == id)
                    .Select(c => new ClienteResponseDto
                    {
                        Id = c.Id,
                        Nombre = c.Nombre,
                        Telefono = c.Telefono,
                        Email = c.Email,
                        FechaRegistro = c.FechaRegistro,
                        Observaciones = c.Observaciones,
                        Etiquetas = c.Etiquetas,
                        PuntosAcumulados = c.PuntosAcumulados,
                        Activo = c.Activo,
                        UltimoAcceso = c.UltimoAcceso
                    })
                    .FirstOrDefaultAsync();

                if (c == null) return NotFound(new { mensaje = "Cliente no encontrado." });

                return Ok(c);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener detalles del cliente {Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al obtener el cliente." });
            }
        }

        // GET: api/Clientes/{id}/historial
        [HttpGet("{id}/historial")]
        public async Task<IActionResult> GetHistorialCliente(int id)
        {
            try
            {
                var cliente = await _context.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
                if (cliente == null) return NotFound(new { mensaje = "Cliente no encontrado." });

                var ahoraNicaragua = GetNicaraguaTime();

                // 1. Historial de Compras
                var compras = await _context.Ventas
                    .AsNoTracking()
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

                // 2. Cuentas por Cobrar
                var deudas = await _context.CuentasPorCobrar
                    .AsNoTracking()
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

                // 3. Órdenes de Taller
                var ordenesTaller = await _context.OrdenesServicio
                    .AsNoTracking()
                    .Where(o => o.IdCliente == id)
                    .OrderByDescending(o => o.FechaIngreso)
                    .ToListAsync();

                // 4. Suscripciones
                var todasSuscripciones = await _context.Suscripciones
                    .AsNoTracking()
                    .Include(s => s.PerfilCuenta)
                    .Where(s => s.IdCliente == id)
                    .OrderByDescending(s => s.FechaVencimiento)
                    .ToListAsync();

                // 5. Segmentación corregida
                var serviciosActivos = new
                {
                    TallerEquiposEnRevision = ordenesTaller
                        .Where(o => o.Estado == "Recibido" || o.Estado == "En Revisión" || o.Estado == "Listo")
                        .Select(o => new { o.Id, o.Dispositivo, o.Diagnostico, o.Estado, o.FechaIngreso }),

                    // 👈 Vigentes: Cualquier suscripción cuyo periodo pagado no haya vencido aún
                    SuscripcionesVigentes = todasSuscripciones
                        .Where(s => s.FechaVencimiento >= ahoraNicaragua)
                        .Select(s => new
                        {
                            s.Id,
                            s.NombreServicio,
                            s.TipoSuscripcion,
                            s.Estado, // 👈 Incluimos el Estado ("Activa", "Cancelada", etc.)
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

                    // 👈 Expiradas: Solo las suscripciones que ya sobrepasaron su fecha límite de uso
                    SuscripcionesExpiradas = todasSuscripciones
                        .Where(s => s.FechaVencimiento < ahoraNicaragua)
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar historial consolidado del cliente {Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al obtener el historial del cliente." });
            }
        }

        // POST: api/Clientes
        [HttpPost]
        public async Task<IActionResult> CrearCliente([FromBody] CrearClienteDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { mensaje = "Datos de cliente inválidos.", detalles = ModelState });
            }

            try
            {
                var telefonoLimpio = dto.Telefono.Trim();

                var clienteExistente = await _context.Clientes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Telefono == telefonoLimpio);

                if (clienteExistente != null)
                {
                    // Si ya existe por teléfono, devolvemos respuesta estructurada DTO
                    return Ok(new
                    {
                        mensaje = "El cliente ya se encontraba registrado.",
                        cliente = new ClienteResponseDto
                        {
                            Id = clienteExistente.Id,
                            Nombre = clienteExistente.Nombre,
                            Telefono = clienteExistente.Telefono,
                            Email = clienteExistente.Email,
                            FechaRegistro = clienteExistente.FechaRegistro,
                            Observaciones = clienteExistente.Observaciones,
                            Etiquetas = clienteExistente.Etiquetas,
                            PuntosAcumulados = clienteExistente.PuntosAcumulados,
                            Activo = clienteExistente.Activo
                        }
                    });
                }

                var nuevoCliente = new Cliente
                {
                    Nombre = dto.Nombre.Trim(),
                    Telefono = telefonoLimpio,
                    Email = dto.Email?.Trim() ?? string.Empty,
                    Observaciones = dto.Observaciones?.Trim() ?? string.Empty,
                    Etiquetas = dto.Etiquetas?.Trim() ?? string.Empty,
                    FechaRegistro = GetNicaraguaTime(),
                    PuntosAcumulados = 0,
                    Activo = true
                };

                _context.Clientes.Add(nuevoCliente);
                await _context.SaveChangesAsync();

                var response = new ClienteResponseDto
                {
                    Id = nuevoCliente.Id,
                    Nombre = nuevoCliente.Nombre,
                    Telefono = nuevoCliente.Telefono,
                    Email = nuevoCliente.Email,
                    FechaRegistro = nuevoCliente.FechaRegistro,
                    Observaciones = nuevoCliente.Observaciones,
                    Etiquetas = nuevoCliente.Etiquetas,
                    PuntosAcumulados = nuevoCliente.PuntosAcumulados,
                    Activo = nuevoCliente.Activo
                };

                return CreatedAtAction(nameof(GetById), new { id = nuevoCliente.Id }, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar cliente.");
                return StatusCode(500, new { mensaje = "Error interno al crear el cliente." });
            }
        }

        // PUT: api/Clientes/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> ActualizarCliente(int id, [FromBody] CrearClienteDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { mensaje = "Datos de cliente inválidos.", detalles = ModelState });
            }

            try
            {
                var cliente = await _context.Clientes.FindAsync(id);
                if (cliente == null) return NotFound(new { mensaje = "Cliente no encontrado." });

                cliente.Nombre = dto.Nombre.Trim();
                cliente.Telefono = dto.Telefono.Trim();
                cliente.Email = dto.Email?.Trim() ?? string.Empty;
                cliente.Observaciones = dto.Observaciones?.Trim() ?? string.Empty;
                cliente.Etiquetas = dto.Etiquetas?.Trim() ?? string.Empty;

                await _context.SaveChangesAsync();

                return Ok(new { mensaje = "Cliente actualizado correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar el cliente con ID {Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al actualizar los datos del cliente." });
            }
        }

        // DELETE: api/Clientes/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> EliminarCliente(int id)
        {
            try
            {
                var cliente = await _context.Clientes.FindAsync(id);
                if (cliente == null) return NotFound(new { mensaje = "Cliente no encontrado." });

                var tieneVentas = await _context.Ventas.AsNoTracking().AnyAsync(v => v.IdCliente == id);
                var tieneTaller = await _context.OrdenesServicio.AsNoTracking().AnyAsync(o => o.IdCliente == id);
                var tieneDeudas = await _context.CuentasPorCobrar.AsNoTracking().AnyAsync(c => c.IdCliente == id && c.SaldoPendiente > 0);

                if (tieneVentas || tieneTaller || tieneDeudas)
                {
                    return BadRequest(new
                    {
                        error = "Restricción de integridad contable/operativa",
                        mensaje = "No se puede eliminar el cliente porque posee historial de facturas, órdenes de taller o saldos pendientes de pago.",
                        detalles = new { tieneVentas, tieneTaller, tieneDeudas }
                    });
                }

                _context.Clientes.Remove(cliente);
                await _context.SaveChangesAsync();

                return Ok(new { mensaje = "Cliente eliminado con éxito." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al intentar eliminar el cliente {Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al eliminar el cliente." });
            }
        }
    }
}