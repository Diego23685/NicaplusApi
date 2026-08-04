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
    [Authorize] // Exclusivo para gestión de administración / soporte
    public class PerfilesCuentasController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PerfilesCuentasController> _logger;

        public PerfilesCuentasController(
            ApplicationDbContext context,
            ILogger<PerfilesCuentasController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // 1. GET: api/PerfilesCuentas/producto/{idProducto}
        [HttpGet("producto/{idProducto}")]
        public async Task<IActionResult> GetPerfilesPorProducto(int idProducto)
        {
            try
            {
                var perfiles = await _context.PerfilesCuentas
                    .AsNoTracking()
                    .Where(p => p.IdProducto == idProducto)
                    .OrderBy(p => p.AccountGroupKey)
                    .ThenBy(p => p.NombrePerfil)
                    .Select(p => new PerfilCuentaResponseDto
                    {
                        Id = p.Id,
                        IdProducto = p.IdProducto,
                        NombrePerfil = p.NombrePerfil,
                        PIN = p.PIN,
                        CorreoCuenta = p.CorreoCuenta,
                        PasswordCuenta = p.PasswordCuenta,
                        Ocupado = p.Ocupado,
                        IdClienteAsignado = p.IdClienteAsignado,
                        NombreCliente = p.IdClienteAsignado.HasValue
                            ? _context.Clientes
                                .Where(c => c.Id == p.IdClienteAsignado.Value)
                                .Select(c => c.Nombre)
                                .FirstOrDefault() ?? "Cliente Desconocido"
                            : "Disponible",
                        EstadoPerfil = p.EstadoPerfil,
                        AccountGroupKey = p.AccountGroupKey,
                        FechaAsignacion = p.FechaAsignacion
                    })
                    .ToListAsync();

                return Ok(perfiles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar los perfiles del producto ID {IdProducto}", idProducto);
                return StatusCode(500, new { mensaje = "Error interno al consultar el inventario de perfiles." });
            }
        }

        // 2. POST: api/PerfilesCuentas
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CrearPerfilCuentaDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { mensaje = "Datos del perfil incompletos o inválidos.", detalles = ModelState });
            }

            try
            {
                var productoExiste = await _context.Productos.AnyAsync(p => p.Id == dto.IdProducto);
                if (!productoExiste)
                {
                    return BadRequest(new { mensaje = "El producto especificado no existe." });
                }

                string groupKey = string.IsNullOrWhiteSpace(dto.AccountGroupKey)
                    ? Guid.NewGuid().ToString("N")[..8]
                    : dto.AccountGroupKey.Trim();

                var perfil = new PerfilCuenta
                {
                    IdProducto = dto.IdProducto,
                    NombrePerfil = dto.NombrePerfil.Trim(),
                    PIN = dto.PIN?.Trim() ?? string.Empty,
                    CorreoCuenta = dto.CorreoCuenta.Trim(),
                    PasswordCuenta = dto.PasswordCuenta.Trim(),
                    Ocupado = false,
                    EstadoPerfil = "Disponible",
                    AccountGroupKey = groupKey
                };

                _context.PerfilesCuentas.Add(perfil);
                await _context.SaveChangesAsync();

                var response = new PerfilCuentaResponseDto
                {
                    Id = perfil.Id,
                    IdProducto = perfil.IdProducto,
                    NombrePerfil = perfil.NombrePerfil,
                    PIN = perfil.PIN,
                    CorreoCuenta = perfil.CorreoCuenta,
                    PasswordCuenta = perfil.PasswordCuenta,
                    Ocupado = perfil.Ocupado,
                    NombreCliente = "Disponible",
                    EstadoPerfil = perfil.EstadoPerfil,
                    AccountGroupKey = perfil.AccountGroupKey
                };

                return Ok(new
                {
                    mensaje = "Perfil de cuenta registrado individualmente con éxito.",
                    perfil = response
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear perfil de cuenta individual.");
                return StatusCode(500, new { mensaje = "Error interno al registrar el perfil." });
            }
        }

        // 3. POST: api/PerfilesCuentas/cuenta-completa
        [HttpPost("cuenta-completa")]
        public async Task<IActionResult> PostCuentaCompleta([FromBody] RequestCuentaCompletaDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { mensaje = "Datos de la cuenta completa inválidos.", detalles = ModelState });
            }

            try
            {
                var productoExiste = await _context.Productos.AnyAsync(p => p.Id == request.IdProducto);
                if (!productoExiste)
                {
                    return BadRequest(new { mensaje = "El producto especificado no existe." });
                }

                string groupKey = Guid.NewGuid().ToString("N")[..8];
                var nuevosPerfiles = new List<PerfilCuenta>();

                for (int i = 1; i <= request.CantidadPerfiles; i++)
                {
                    nuevosPerfiles.Add(new PerfilCuenta
                    {
                        IdProducto = request.IdProducto,
                        NombrePerfil = $"Perfil {i}",
                        CorreoCuenta = request.CorreoCuenta.Trim(),
                        PasswordCuenta = request.PasswordCuenta.Trim(),
                        PIN = $"100{i}",
                        Ocupado = false,
                        IdClienteAsignado = null,
                        EstadoPerfil = "Disponible",
                        AccountGroupKey = groupKey
                    });
                }

                _context.PerfilesCuentas.AddRange(nuevosPerfiles);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    mensaje = $"Cuenta {request.CorreoCuenta} registrada exitosamente.",
                    groupKey = groupKey,
                    perfilesGenerados = request.CantidadPerfiles
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar lote de cuenta completa para {Correo}", request.CorreoCuenta);
                return StatusCode(500, new { mensaje = "Error interno al procesar el lote de perfiles." });
            }
        }

        // 4. PUT: api/PerfilesCuentas/5
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] ActualizarPerfilCuentaDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { mensaje = "Datos de actualización inválidos.", detalles = ModelState });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var perfilExistente = await _context.PerfilesCuentas.FindAsync(id);
                if (perfilExistente == null)
                {
                    return NotFound(new { mensaje = "El perfil solicitado no existe." });
                }

                string? grupoOriginal = perfilExistente.AccountGroupKey;
                bool credencialesCambiaron = perfilExistente.CorreoCuenta != dto.CorreoCuenta.Trim() ||
                                             perfilExistente.PasswordCuenta != dto.PasswordCuenta.Trim();

                // Si las credenciales globales de la cuenta cambiaron, sincronizamos todo el grupo técnico
                if (credencialesCambiaron && !string.IsNullOrEmpty(grupoOriginal))
                {
                    var perfilesDelMismoGrupo = await _context.PerfilesCuentas
                        .Where(p => p.AccountGroupKey == grupoOriginal && p.IdProducto == perfilExistente.IdProducto)
                        .ToListAsync();

                    foreach (var pLote in perfilesDelMismoGrupo)
                    {
                        pLote.CorreoCuenta = dto.CorreoCuenta.Trim();
                        pLote.PasswordCuenta = dto.PasswordCuenta.Trim();

                        // Si el perfil está asignado, actualizamos el detalle de la suscripción activa
                        if (pLote.Ocupado && pLote.IdClienteAsignado.HasValue)
                        {
                            var subActiva = await _context.Suscripciones
                                .FirstOrDefaultAsync(s => s.IdCliente == pLote.IdClienteAsignado.Value &&
                                                          s.IdPerfilCuenta == pLote.Id &&
                                                          s.Estado == "Activa");
                            if (subActiva != null)
                            {
                                subActiva.DetallesCredenciales = $"Perfil: {pLote.NombrePerfil} | Correo: {pLote.CorreoCuenta} | Clave: {pLote.PasswordCuenta} | PIN: {pLote.PIN}";
                            }
                        }
                    }
                }

                perfilExistente.NombrePerfil = dto.NombrePerfil.Trim();
                perfilExistente.PIN = dto.PIN?.Trim() ?? string.Empty;

                if (!credencialesCambiaron)
                {
                    perfilExistente.CorreoCuenta = dto.CorreoCuenta.Trim();
                    perfilExistente.PasswordCuenta = dto.PasswordCuenta.Trim();
                }

                // Sincronización individual si solo cambió PIN o Nombre en perfil activo
                if (perfilExistente.Ocupado && perfilExistente.IdClienteAsignado.HasValue)
                {
                    var suscripcionActiva = await _context.Suscripciones
                        .FirstOrDefaultAsync(s => s.IdCliente == perfilExistente.IdClienteAsignado.Value &&
                                                  s.IdPerfilCuenta == perfilExistente.Id &&
                                                  s.Estado == "Activa");

                    if (suscripcionActiva != null)
                    {
                        suscripcionActiva.DetallesCredenciales = $"Perfil: {perfilExistente.NombrePerfil} | Correo: {perfilExistente.CorreoCuenta} | Clave: {perfilExistente.PasswordCuenta} | PIN: {perfilExistente.PIN}";
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { mensaje = "Perfil y credenciales asociadas actualizados correctamente." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al actualizar credenciales del perfil ID {Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al actualizar las credenciales del perfil." });
            }
        }

        // 5. PUT: api/PerfilesCuentas/5/liberar
        [HttpPut("{id}/liberar")]
        public async Task<IActionResult> LiberarPerfil(int id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var perfil = await _context.PerfilesCuentas.FindAsync(id);
                if (perfil == null)
                {
                    return NotFound(new { mensaje = "Perfil no encontrado." });
                }

                if (perfil.IdClienteAsignado.HasValue)
                {
                    int idCliente = perfil.IdClienteAsignado.Value;
                    int idProducto = perfil.IdProducto;

                    var suscripcionActiva = await _context.Suscripciones
                        .FirstOrDefaultAsync(s => s.IdCliente == idCliente &&
                                                  s.IdProducto == idProducto &&
                                                  s.IdPerfilCuenta == perfil.Id &&
                                                  s.Estado == "Activa");

                    if (suscripcionActiva != null)
                    {
                        suscripcionActiva.Estado = "Cancelada";
                    }
                }

                perfil.Ocupado = false;
                perfil.IdClienteAsignado = null;
                perfil.EstadoPerfil = "Disponible";
                perfil.FechaLiberacion = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, GetNicaraguaTimeZone());

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { mensaje = "El perfil ha sido liberado exitosamente y su suscripción asociada ha sido marcada como Cancelada." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al liberar el perfil ID {Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al intentar liberar el perfil." });
            }
        }

        // 6. DELETE: api/PerfilesCuentas/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var perfil = await _context.PerfilesCuentas.FindAsync(id);
                if (perfil == null)
                {
                    return NotFound(new { mensaje = "El perfil especificado no existe." });
                }

                if (perfil.Ocupado)
                {
                    return BadRequest(new { mensaje = "No se puede eliminar un perfil asignado a un cliente activo." });
                }

                _context.PerfilesCuentas.Remove(perfil);
                await _context.SaveChangesAsync();

                return Ok(new { mensaje = "Perfil eliminado correctamente del pool." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar el perfil ID {Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al eliminar el perfil." });
            }
        }

        // 7. DELETE: api/PerfilesCuentas/grupo/{groupKey}
        [HttpDelete("grupo/{groupKey}")]
        public async Task<IActionResult> DeleteGrupo(string groupKey)
        {
            if (string.IsNullOrWhiteSpace(groupKey))
            {
                return BadRequest(new { mensaje = "El identificador de grupo (AccountGroupKey) es requerido." });
            }

            try
            {
                var perfilesGrupo = await _context.PerfilesCuentas
                    .Where(p => p.AccountGroupKey == groupKey)
                    .ToListAsync();

                if (perfilesGrupo.Count == 0)
                {
                    return NotFound(new { mensaje = "No se encontraron perfiles asociados al grupo indicado." });
                }

                if (perfilesGrupo.Any(p => p.Ocupado))
                {
                    return BadRequest(new { mensaje = "No se puede eliminar la cuenta completa porque uno o más perfiles están asignados actualmente a clientes." });
                }

                _context.PerfilesCuentas.RemoveRange(perfilesGrupo);
                await _context.SaveChangesAsync();

                return Ok(new { mensaje = $"Se eliminó el grupo completo ({perfilesGrupo.Count} perfiles)." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar el grupo de perfiles con Key {GroupKey}", groupKey);
                return StatusCode(500, new { mensaje = "Error interno al eliminar el lote de perfiles." });
            }
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
    }
}