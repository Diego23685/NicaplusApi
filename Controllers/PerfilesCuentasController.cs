using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class RequestCuentaCompleta
{
    public int IdProducto { get; set; }
    public string CorreoCuenta { get; set; } = string.Empty;
    public string PasswordCuenta { get; set; } = string.Empty;
    public int CantidadPerfiles { get; set; } = 5; // Por defecto 5 si no se envía
}

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PerfilesCuentasController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PerfilesCuentasController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. GET: api/perfilescuentas/producto/{idProducto}
        [HttpGet("producto/{idProducto}")]
        public async Task<IActionResult> GetPerfilesPorProducto(int idProducto)
        {
            var perfiles = await _context.PerfilesCuentas
                .Where(p => p.IdProducto == idProducto)
                .OrderBy(p => p.AccountGroupKey)
                .ThenBy(p => p.NombrePerfil)
                .Select(p => new
                {
                    p.Id,
                    p.IdProducto,
                    p.NombrePerfil,
                    p.PIN,
                    p.CorreoCuenta,
                    p.PasswordCuenta,
                    p.Ocupado,
                    p.IdClienteAsignado,
                    p.AccountGroupKey,
                    NombreCliente = p.IdClienteAsignado.HasValue 
                        ? _context.Clientes.Where(c => c.Id == p.IdClienteAsignado.Value).Select(c => c.Nombre).FirstOrDefault() 
                        : "Disponible"
                })
                .ToListAsync();

            return Ok(perfiles);
        }

        // 2. POST: api/perfilescuentas
        [HttpPost]
        public async Task<ActionResult<PerfilCuenta>> Post([FromBody] PerfilCuenta perfil)
        {
            if (perfil.IdProducto == 0) return BadRequest("El ID del producto base es obligatorio.");

            if (string.IsNullOrEmpty(perfil.AccountGroupKey))
            {
                perfil.AccountGroupKey = Guid.NewGuid().ToString().Substring(0, 8);
            }

            _context.PerfilesCuentas.Add(perfil);
            await _context.SaveChangesAsync();

            return Ok(perfil);
        }

        // 4. POST: api/perfilescuentas/cuenta-completa
        [HttpPost("cuenta-completa")]
        public async Task<IActionResult> PostCuentaCompleta([FromBody] RequestCuentaCompleta request)
        {
            if (request.IdProducto == 0) return BadRequest("El ID del producto base es obligatorio.");
            if (string.IsNullOrEmpty(request.CorreoCuenta) || string.IsNullOrEmpty(request.PasswordCuenta))
                return BadRequest("El correo y la contraseña son obligatorios.");

            string groupKey = Guid.NewGuid().ToString().Substring(0, 8); 

            var nuevosPerfiles = new List<PerfilCuenta>();

            for (int i = 1; i <= request.CantidadPerfiles; i++)
            {
                nuevosPerfiles.Add(new PerfilCuenta
                {
                    IdProducto = request.IdProducto,
                    NombrePerfil = $"Perfil {i}",
                    CorreoCuenta = request.CorreoCuenta,
                    PasswordCuenta = request.PasswordCuenta,
                    PIN = $"100{i}",
                    Ocupado = false,
                    IdClienteAsignado = null,
                    EstadoPerfil = "Disponible",
                    AccountGroupKey = groupKey
                });
            }

            _context.PerfilesCuentas.AddRange(nuevosPerfiles);
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = $"Cuenta {request.CorreoCuenta} registrada. Se generaron {request.CantidadPerfiles} perfiles en el pool técnico." });
        }

        // 3. DELETE: api/perfilescuentas/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var perfil = await _context.PerfilesCuentas.FindAsync(id);
            if (perfil == null)
            {
                return NotFound("El perfil no existe.");
            }

            if (perfil.Ocupado)
            {
                return BadRequest("No se puede eliminar un perfil que se encuentra actualmente asignado a un cliente.");
            }

            _context.PerfilesCuentas.Remove(perfil);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // 🌟 NUEVO ENDPOINT AGREGADO 🌟
        // 7. DELETE: api/perfilescuentas/grupo/{groupKey}
        // Elimina por lote todas las pantallas vinculadas a una cuenta si ninguna está ocupada
        [HttpDelete("grupo/{groupKey}")]
        public async Task<IActionResult> DeleteGrupo(string groupKey)
        {
            if (string.IsNullOrEmpty(groupKey)) return BadRequest("El identificador de grupo es requerido.");

            var perfilesGrupo = await _context.PerfilesCuentas
                .Where(p => p.AccountGroupKey == groupKey)
                .ToListAsync();

            if (perfilesGrupo.Count == 0)
            {
                return NotFound("No se encontraron perfiles asociados a este grupo de cuentas.");
            }

            // Validar que ninguna pantalla de la cuenta esté en uso por integridad
            if (perfilesGrupo.Any(p => p.Ocupado))
            {
                return BadRequest("No se puede eliminar la cuenta completa porque uno o más perfiles están asignados a un cliente.");
            }

            _context.PerfilesCuentas.RemoveRange(perfilesGrupo);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // 5. PUT: api/perfilescuentas/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] PerfilCuenta perfilActualizado)
        {
            if (id != perfilActualizado.Id) return BadRequest("Los IDs no coinciden.");

            var perfilExistente = await _context.PerfilesCuentas.FindAsync(id);
            if (perfilExistente == null) return NotFound("Perfil no encontrado.");

            string? grupoOriginal = perfilExistente.AccountGroupKey;
            bool credencialesCambiaron = perfilExistente.CorreoCuenta != perfilActualizado.CorreoCuenta || 
                                         perfilExistente.PasswordCuenta != perfilActualizado.PasswordCuenta;

            if (credencialesCambiaron && !string.IsNullOrEmpty(grupoOriginal))
            {
                var perfilesDelMismoGrupo = await _context.PerfilesCuentas
                    .Where(p => p.AccountGroupKey == grupoOriginal && p.IdProducto == perfilExistente.IdProducto)
                    .ToListAsync();

                foreach (var pLote in perfilesDelMismoGrupo)
                {
                    pLote.CorreoCuenta = perfilActualizado.CorreoCuenta;
                    pLote.PasswordCuenta = perfilActualizado.PasswordCuenta;
                    _context.Entry(pLote).State = EntityState.Modified;

                    if (pLote.Ocupado && pLote.IdClienteAsignado.HasValue)
                    {
                        var subActiva = await _context.Suscripciones
                            .FirstOrDefaultAsync(s => s.IdCliente == pLote.IdClienteAsignado.Value && 
                                                      s.IdPerfilCuenta == pLote.Id && 
                                                      s.Estado == "Activa");
                        if (subActiva != null)
                        {
                            subActiva.DetallesCredenciales = $"Perfil: {pLote.NombrePerfil} | Correo: {pLote.CorreoCuenta} | Clave: {pLote.PasswordCuenta} | PIN: {pLote.PIN}";
                            _context.Entry(subActiva).State = EntityState.Modified;
                        }
                    }
                }
            }

            perfilExistente.NombrePerfil = perfilActualizado.NombrePerfil;
            perfilExistente.PIN = perfilActualizado.PIN;
            
            if (!credencialesCambiaron)
            {
                perfilExistente.CorreoCuenta = perfilActualizado.CorreoCuenta;
                perfilExistente.PasswordCuenta = perfilActualizado.PasswordCuenta;
            }

            _context.Entry(perfilExistente).State = EntityState.Modified;

            if (perfilExistente.Ocupado && perfilExistente.IdClienteAsignado.HasValue)
            {
                var suscripcionActiva = await _context.Suscripciones
                    .FirstOrDefaultAsync(s => s.IdCliente == perfilExistente.IdClienteAsignado.Value && 
                                              s.IdPerfilCuenta == perfilExistente.Id && 
                                              s.Estado == "Activa");

                if (suscripcionActiva != null)
                {
                    suscripcionActiva.DetallesCredenciales = $"Perfil: {perfilExistente.NombrePerfil} | Correo: {perfilExistente.CorreoCuenta} | Clave: {perfilExistente.PasswordCuenta} | PIN: {perfilExistente.PIN}";
                    _context.Entry(suscripcionActiva).State = EntityState.Modified;
                }
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // 6. PUT: api/perfilescuentas/{id}/liberar
        [HttpPut("{id}/liberar")]
        public async Task<IActionResult> LiberarPerfil(int id)
        {
            var perfil = await _context.PerfilesCuentas.FindAsync(id);
            if (perfil == null) return NotFound("Perfil no encontrado.");

            if (perfil.IdClienteAsignado.HasValue)
            {
                int idCliente = perfil.IdClienteAsignado.Value;
                int idProducto = perfil.IdProducto;

                var suscripcionActiva = await _context.Suscripciones
                    .Where(s => s.IdCliente == idCliente && 
                                s.IdProducto == idProducto && 
                                s.Estado == "Activa")
                    .FirstOrDefaultAsync();

                if (suscripcionActiva != null)
                {
                    suscripcionActiva.Estado = "Cancelada"; 
                    _context.Entry(suscripcionActiva).State = EntityState.Modified;
                }
            }

            perfil.Ocupado = false;
            perfil.IdClienteAsignado = null;

            _context.Entry(perfil).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "El perfil ha sido liberado y su suscripción activa ha sido marcada como Cancelada." });
        }
    }
}