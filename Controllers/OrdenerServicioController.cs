using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.DTOs;
using NicaplusApi.Models;
using NicaplusApi.Services;
using System.Security.Claims;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Protegido mediante JWT
    public class OrdenesServicioController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWhatsAppService _whatsappService;
        private readonly ILogger<OrdenesServicioController> _logger;

        public OrdenesServicioController(
            ApplicationDbContext context,
            IWhatsAppService whatsappService,
            ILogger<OrdenesServicioController> logger)
        {
            _context = context;
            _whatsappService = whatsappService;
            _logger = logger;
        }

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

        private bool TryGetUserId(out int userId)
        {
            userId = 0;
            var claimValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return !string.IsNullOrEmpty(claimValue) && int.TryParse(claimValue, out userId);
        }

        // GET: api/OrdenesServicio
        [HttpGet]
        public async Task<IActionResult> GetOrdenes()
        {
            try
            {
                var ordenes = await _context.OrdenesServicio
                    .AsNoTracking()
                    .Include(o => o.Cliente)
                    .Include(o => o.Tecnico)
                    .OrderByDescending(o => o.FechaIngreso)
                    .Select(o => new OrdenServicioResponseDto
                    {
                        Id = o.Id,
                        IdCliente = o.IdCliente,
                        ClienteNombre = o.Cliente != null ? o.Cliente.Nombre : "Genérico",
                        ClienteTelefono = o.Cliente != null ? o.Cliente.Telefono : string.Empty,
                        IdUsuario = o.IdUsuario,
                        TecnicoNombre = o.Tecnico != null ? o.Tecnico.Nombre : "Sin asignar",
                        Dispositivo = o.Dispositivo,
                        Diagnostico = o.Diagnostico,
                        Estado = o.Estado,
                        FechaIngreso = o.FechaIngreso,
                        FechaEntrega = o.FechaEntrega,
                        Notas = o.Notas
                    })
                    .ToListAsync();

                return Ok(ordenes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar las órdenes de servicio del taller.");
                return StatusCode(500, new { mensaje = "Error interno al obtener las órdenes de servicio." });
            }
        }

        // GET: api/OrdenesServicio/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var orden = await _context.OrdenesServicio
                    .AsNoTracking()
                    .Include(o => o.Cliente)
                    .Include(o => o.Tecnico)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (orden == null)
                {
                    return NotFound(new { mensaje = "La orden de servicio especificada no existe." });
                }

                var response = new OrdenServicioResponseDto
                {
                    Id = orden.Id,
                    IdCliente = orden.IdCliente,
                    ClienteNombre = orden.Cliente != null ? orden.Cliente.Nombre : "Genérico",
                    ClienteTelefono = orden.Cliente != null ? orden.Cliente.Telefono : string.Empty,
                    IdUsuario = orden.IdUsuario,
                    TecnicoNombre = orden.Tecnico != null ? orden.Tecnico.Nombre : "Sin asignar",
                    Dispositivo = orden.Dispositivo,
                    Diagnostico = orden.Diagnostico,
                    Estado = orden.Estado,
                    FechaIngreso = orden.FechaIngreso,
                    FechaEntrega = orden.FechaEntrega,
                    Notas = orden.Notas
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar la orden de servicio ID {Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al consultar la orden de servicio." });
            }
        }

        // POST: api/OrdenesServicio
        [HttpPost]
        public async Task<IActionResult> CrearOrden([FromBody] CrearOrdenServicioDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { mensaje = "Datos de la orden de servicio inválidos.", detalles = ModelState });
            }

            try
            {
                var cliente = await _context.Clientes.FindAsync(dto.IdCliente);
                if (cliente == null)
                {
                    return BadRequest(new { mensaje = "El cliente especificado no existe." });
                }

                if (dto.IdUsuario.HasValue)
                {
                    var tecnico = await _context.Usuarios.FindAsync(dto.IdUsuario.Value);
                    if (tecnico == null)
                    {
                        return BadRequest(new { mensaje = "El técnico asignado especificado no existe." });
                    }
                }

                var orden = new OrdenServicio
                {
                    IdCliente = dto.IdCliente,
                    IdUsuario = dto.IdUsuario,
                    Dispositivo = dto.Dispositivo.Trim(),
                    Diagnostico = dto.Diagnostico.Trim(),
                    Estado = "Recibido",
                    FechaIngreso = GetNicaraguaTime(),
                    Notas = dto.Notas?.Trim() ?? string.Empty
                };

                _context.OrdenesServicio.Add(orden);
                await _context.SaveChangesAsync();

                var response = new OrdenServicioResponseDto
                {
                    Id = orden.Id,
                    IdCliente = orden.IdCliente,
                    ClienteNombre = cliente.Nombre,
                    ClienteTelefono = cliente.Telefono,
                    IdUsuario = orden.IdUsuario,
                    TecnicoNombre = dto.IdUsuario.HasValue ? (await _context.Usuarios.FindAsync(dto.IdUsuario.Value))?.Nombre ?? "Sin asignar" : "Sin asignar",
                    Dispositivo = orden.Dispositivo,
                    Diagnostico = orden.Diagnostico,
                    Estado = orden.Estado,
                    FechaIngreso = orden.FechaIngreso,
                    FechaEntrega = orden.FechaEntrega,
                    Notas = orden.Notas
                };

                return CreatedAtAction(nameof(GetById), new { id = orden.Id }, new
                {
                    mensaje = "Orden de servicio registrada correctamente.",
                    orden = response
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar una nueva orden de servicio.");
                return StatusCode(500, new { mensaje = "Error interno al guardar la orden de servicio." });
            }
        }

        // PUT: api/OrdenesServicio/5/estado
        [HttpPut("{id}/estado")]
        public async Task<IActionResult> ActualizarEstado(int id, [FromBody] ActualizarEstadoOrdenDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { mensaje = "Datos incompletos para actualizar el estado.", detalles = ModelState });
            }

            var estadosValidos = new[] { "Recibido", "En Revisión", "Listo", "Entregado" };
            if (!estadosValidos.Contains(dto.NuevoEstado, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new { mensaje = $"Estado no válido. Los estados permitidos son: {string.Join(", ", estadosValidos)}" });
            }

            try
            {
                var orden = await _context.OrdenesServicio
                    .Include(o => o.Cliente)
                    .FirstOrDefaultAsync(o => o.Id == id);

                if (orden == null)
                {
                    return NotFound(new { mensaje = "La orden de servicio especificada no existe." });
                }

                string estadoAnterior = orden.Estado;
                orden.Estado = dto.NuevoEstado;

                if (!string.IsNullOrWhiteSpace(dto.Notas))
                {
                    orden.Notas = string.IsNullOrWhiteSpace(orden.Notas)
                        ? dto.Notas.Trim()
                        : $"{orden.Notas} | {dto.Notas.Trim()}";
                }

                if (dto.NuevoEstado.Equals("Entregado", StringComparison.OrdinalIgnoreCase))
                {
                    orden.FechaEntrega = GetNicaraguaTime();
                }

                await _context.SaveChangesAsync();

                // Notificación vía WhatsApp cuando el equipo está "Listo" para retiro
                if (dto.NuevoEstado.Equals("Listo", StringComparison.OrdinalIgnoreCase) &&
                    !estadoAnterior.Equals("Listo", StringComparison.OrdinalIgnoreCase) &&
                    orden.Cliente != null && !string.IsNullOrWhiteSpace(orden.Cliente.Telefono))
                {
                    var variables = new Dictionary<string, string>
                    {
                        { "cliente", orden.Cliente.Nombre },
                        { "dispositivo", orden.Dispositivo },
                        { "id", orden.Id.ToString() }
                    };

                    try
                    {
                        await _whatsappService.EnviarDesdePlantillaAsync("EnvioComprobante", orden.Cliente.Telefono, variables);
                    }
                    catch (Exception exWs)
                    {
                        _logger.LogWarning(exWs, "No se pudo enviar la notificación de WhatsApp para la orden ID {Id}", orden.Id);
                    }
                }

                return Ok(new
                {
                    mensaje = $"Estado de la orden #{orden.Id} actualizado a '{orden.Estado}'.",
                    estadoActual = orden.Estado,
                    fechaEntrega = orden.FechaEntrega
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar el estado de la orden de servicio ID {Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al actualizar el estado de la orden." });
            }
        }

        // PUT: api/OrdenesServicio/5/entregar
        [HttpPut("{id}/entregar")]
        public async Task<IActionResult> EntregarEquipo(int id, [FromBody] EntregaOrdenDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { mensaje = "Datos de entrega inválidos.", detalles = ModelState });
            }

            if (!TryGetUserId(out int idUsuarioLogueado))
            {
                return Unauthorized(new { mensaje = "No se pudo identificar al usuario autenticado para registrar la venta de servicio." });
            }

            var orden = await _context.OrdenesServicio
                .Include(o => o.Cliente)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (orden == null)
            {
                return NotFound(new { mensaje = "La orden de servicio especificada no existe." });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var ahoraNicaragua = GetNicaraguaTime();

                orden.Estado = "Entregado";
                orden.FechaEntrega = ahoraNicaragua;
                orden.Notas = $"[ENTREGA] Herramientas: {dto.HerramientasUsed}. Diagnóstico: {dto.DiagnosticoFinal}. {orden.Notas}".Trim();

                // Busca un producto configurado como servicio técnico
                var productoServicio = await _context.Productos
                    .FirstOrDefaultAsync(p => p.Id == dto.IdProductoServicio || p.RequiereServicio);

                if (productoServicio == null)
                {
                    return BadRequest(new { mensaje = "No se encontró un concepto de 'Servicio Técnico' válido en el catálogo para generar el ingreso contable." });
                }

                var ventaServicio = new Venta
                {
                    IdUsuario = idUsuarioLogueado,
                    IdCliente = orden.IdCliente,
                    MetodoPago = dto.MetodoPago,
                    FechaVenta = ahoraNicaragua,
                    Total = dto.CostoReparacion,
                    Detalles = new List<DetalleVenta>
                    {
                        new DetalleVenta
                        {
                            IdProducto = productoServicio.Id,
                            Cantidad = 1,
                            PrecioUnitario = dto.CostoReparacion,
                            SubTotal = dto.CostoReparacion,
                            MetadataDigital = $"Taller - Equipo: {orden.Dispositivo} (Orden #{orden.Id})"
                        }
                    }
                };

                _context.Ventas.Add(ventaServicio);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Notificación final vía WhatsApp al entregar
                if (orden.Cliente != null && !string.IsNullOrWhiteSpace(orden.Cliente.Telefono))
                {
                    var variables = new Dictionary<string, string>
                    {
                        { "cliente", orden.Cliente.Nombre },
                        { "factura", $"#000{ventaServicio.Id}" },
                        { "total", $"C$ {dto.CostoReparacion:N2}" },
                        { "dispositivo", orden.Dispositivo }
                    };

                    try
                    {
                        await _whatsappService.EnviarDesdePlantillaAsync("TallerListo", orden.Cliente.Telefono, variables);
                    }
                    catch (Exception exWs)
                    {
                        _logger.LogWarning(exWs, "Error al enviar la plantilla 'TallerListo' para la orden #{Id}", orden.Id);
                    }
                }

                return Ok(new
                {
                    mensaje = "Orden de servicio liquidada y entregada con éxito.",
                    ventaId = ventaServicio.Id,
                    totalCobrado = ventaServicio.Total
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error crítico al liquidar y entregar la orden de servicio ID {Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al liquidar la orden de servicio en caja." });
            }
        }
    }
}