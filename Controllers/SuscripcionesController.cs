using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.DTOs;
using NicaplusApi.Models;
using System.Security.Claims;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class SuscripcionesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SuscripcionesController> _logger;

        public SuscripcionesController(
            ApplicationDbContext context,
            ILogger<SuscripcionesController> logger)
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

        // GET: api/Suscripciones
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var suscripciones = await _context.Suscripciones
                .AsNoTracking()
                .Include(s => s.Cliente)
                .Include(s => s.Producto)
                .OrderByDescending(s => s.FechaVencimiento)
                .Select(s => new
                {
                    s.Id,
                    s.IdCliente,
                    ClienteNombre = s.Cliente != null ? s.Cliente.Nombre : "Cliente Desconocido",
                    s.NombreServicio,
                    s.TipoSuscripcion,
                    s.CostoRenovacion,
                    s.FechaInicio,
                    s.FechaVencimiento,
                    s.Estado,
                    s.DetallesCredenciales,
                    s.IdPerfilCuenta
                })
                .ToListAsync();

            return Ok(suscripciones);
        }

        // GET: api/Suscripciones/alertas (Lectura pura sin escrituras secundarias)
        [HttpGet("alertas")]
        public async Task<IActionResult> GetAlertasRenovacion()
        {
            try
            {
                var hoyNicaragua = GetNicaraguaTime().Date;

                var suscripciones = await _context.Suscripciones
                    .AsNoTracking()
                    .Include(s => s.Cliente)
                    .Where(s => s.Estado != "Cancelada")
                    .ToListAsync();

                var listaConAlertas = suscripciones.Select(s =>
                {
                    TimeSpan diferencia = s.FechaVencimiento.Date - hoyNicaragua;
                    int diasRestantes = diferencia.Days;

                    string alertaFiltro = "Normal";
                    string estadoCalculado = s.Estado;

                    if (diasRestantes < 0)
                    {
                        alertaFiltro = "Vencido";
                        if (s.Estado == "Activa")
                        {
                            estadoCalculado = "Vencida"; // Proyectado dinámicamente sin mutar la BD en un GET
                        }
                    }
                    else if (diasRestantes == 0) alertaFiltro = "Hoy";
                    else if (diasRestantes == 1) alertaFiltro = "1 Dia";
                    else if (diasRestantes <= 3) alertaFiltro = "3 Dias";
                    else if (diasRestantes <= 7) alertaFiltro = "7 Dias";

                    return new AlertaSuscripcionDto
                    {
                        Id = s.Id,
                        NombreServicio = s.NombreServicio,
                        FechaInicio = s.FechaInicio,
                        FechaVencimiento = s.FechaVencimiento,
                        CostoRenovacion = s.CostoRenovacion,
                        Estado = estadoCalculado,
                        DetallesCredenciales = s.DetallesCredenciales,
                        DiasRestantes = diasRestantes,
                        AlertaFiltro = alertaFiltro,
                        Cliente = s.Cliente != null ? new ClienteAlertaDto
                        {
                            Nombre = s.Cliente.Nombre,
                            Telefono = s.Cliente.Telefono
                        } : null
                    };
                })
                .OrderBy(x => x.DiasRestantes)
                .ToList();

                return Ok(listaConAlertas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener las alertas de renovación.");
                return StatusCode(500, new { mensaje = "Error interno al calcular alertas de suscripciones." });
            }
        }

        // GET: api/Suscripciones/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var suscripcion = await _context.Suscripciones
                .AsNoTracking()
                .Include(s => s.Cliente)
                .Include(s => s.Producto)
                .Include(s => s.PerfilCuenta)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (suscripcion == null)
                return NotFound(new { mensaje = "Suscripción no encontrada." });

            return Ok(suscripcion);
        }

        // POST: api/Suscripciones (Alta inicial atómica)
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CrearSuscripcionDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var clienteExiste = await _context.Clientes.AnyAsync(c => c.Id == dto.IdCliente);
                if (!clienteExiste)
                    return BadRequest(new { mensaje = "El cliente especificado no existe." });

                var ahoraNicaragua = GetNicaraguaTime();

                // Extraer ID del usuario autenticado vía JWT
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                int idUsuarioOperador = int.TryParse(userIdClaim, out int idParsed) ? idParsed : 1;

                // 1. Instanciar Suscripción
                var suscripcion = new Suscripcion
                {
                    IdCliente = dto.IdCliente,
                    NombreServicio = dto.NombreServicio.Trim(),
                    TipoSuscripcion = dto.TipoSuscripcion.Trim(),
                    IdProducto = dto.IdProducto,
                    IdOrdenServicio = dto.IdOrdenServicio,
                    IdPerfilCuenta = dto.IdPerfilCuenta,
                    CostoRenovacion = dto.CostoRenovacion,
                    FechaInicio = dto.FechaInicio ?? ahoraNicaragua,
                    FechaVencimiento = dto.FechaVencimiento ?? (dto.FechaInicio ?? ahoraNicaragua).AddDays(30),
                    Estado = "Activa",
                    DetallesCredenciales = dto.DetallesCredenciales ?? string.Empty
                };

                _context.Suscripciones.Add(suscripcion);
                await _context.SaveChangesAsync(); // Se persiste para obtener suscripcion.Id

                // 2. Crear Venta vincular
                var venta = new Venta
                {
                    FechaVenta = suscripcion.FechaInicio,
                    IdCliente = suscripcion.IdCliente,
                    IdUsuario = idUsuarioOperador,
                    IdSuscripcion = suscripcion.Id,
                    Total = suscripcion.CostoRenovacion,
                    MetodoPago = "Efectivo"
                };

                _context.Ventas.Add(venta);

                // 3. Crear Detalle de Venta (si aplica producto)
                if (suscripcion.IdProducto.HasValue)
                {
                    var detalle = new DetalleVenta
                    {
                        Venta = venta, // EF Core resuelve la relación automáticamente
                        IdProducto = suscripcion.IdProducto.Value,
                        Cantidad = 1,
                        PrecioUnitario = suscripcion.CostoRenovacion,
                        SubTotal = suscripcion.CostoRenovacion
                    };

                    _context.DetallesVentas.Add(detalle);
                }

                // 4. Movimiento de caja correlacionado
                var movimiento = new MovimientoCaja
                {
                    Fecha = suscripcion.FechaInicio,
                    Tipo = "Ingreso",
                    Concepto = "Venta Suscripcion",
                    Monto = suscripcion.CostoRenovacion,
                    Detalle = $"Alta inicial {suscripcion.NombreServicio} | Cliente ID: {suscripcion.IdCliente}",
                    Venta = venta
                };

                _context.MovimientosCaja.Add(movimiento);

                // Persistencia atómica única en BD
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return CreatedAtAction(
                    nameof(GetById),
                    new { id = suscripcion.Id },
                    new
                    {
                        mensaje = "Suscripción registrada exitosamente.",
                        idSuscripcion = suscripcion.Id,
                        idVenta = venta.Id
                    });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error registrando la alta de suscripción para cliente {IdCliente}", dto.IdCliente);
                return StatusCode(500, new { mensaje = "Error interno al procesar el registro de la suscripción." });
            }
        }

        // PUT: api/Suscripciones/5
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] ActualizarSuscripcionDto dto)
        {
            if (id != dto.Id)
                return BadRequest(new { mensaje = "El ID de la URL no coincide con el cuerpo de la solicitud." });

            var suscripcion = await _context.Suscripciones.FindAsync(id);
            if (suscripcion == null)
                return NotFound(new { mensaje = "Suscripción no encontrada." });

            suscripcion.NombreServicio = dto.NombreServicio.Trim();
            suscripcion.TipoSuscripcion = dto.TipoSuscripcion.Trim();
            suscripcion.CostoRenovacion = dto.CostoRenovacion;
            suscripcion.FechaVencimiento = dto.FechaVencimiento;
            suscripcion.Estado = dto.Estado.Trim();
            suscripcion.DetallesCredenciales = dto.DetallesCredenciales ?? string.Empty;

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { mensaje = "Suscripción actualizada correctamente." });
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Conflicto de concurrencia al actualizar la suscripción #{Id}", id);
                return StatusCode(409, new { mensaje = "El registro fue modificado por otro usuario." });
            }
        }

        // DELETE: api/Suscripciones/5 (Baja Lógica)
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var suscripcion = await _context.Suscripciones.FindAsync(id);
            if (suscripcion == null)
                return NotFound(new { mensaje = "Suscripción no encontrada." });

            suscripcion.Estado = "Cancelada";
            _context.Entry(suscripcion).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = $"Suscripción #{id} cancelada correctamente." });
        }
    }
}