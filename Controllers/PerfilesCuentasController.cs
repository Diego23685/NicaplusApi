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
        // Devuelve las pantallas/perfiles asociados a un servicio con el nombre del cliente incluido
        [HttpGet("producto/{idProducto}")]
        public async Task<IActionResult> GetPerfilesPorProducto(int idProducto)
        {
            var perfiles = await _context.PerfilesCuentas
                .Where(p => p.IdProducto == idProducto)
                .OrderBy(p => p.NombrePerfil)
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
                    // Buscamos el nombre del cliente en caliente si está asignado
                    NombreCliente = p.IdClienteAsignado.HasValue 
                        ? _context.Clientes.Where(c => c.Id == p.IdClienteAsignado.Value).Select(c => c.Nombre).FirstOrDefault() 
                        : "Disponible"
                })
                .ToListAsync();

            return Ok(perfiles);
        }

        // 2. POST: api/perfilescuentas
        // Inserta un nuevo perfil/pantalla libre al pool de una cuenta mayor
        [HttpPost]
        public async Task<ActionResult<PerfilCuenta>> Post([FromBody] PerfilCuenta perfil)
        {
            if (perfil.IdProducto == 0)
            {
                return BadRequest("El ID del producto base es obligatorio.");
            }

            _context.PerfilesCuentas.Add(perfil);
            await _context.SaveChangesAsync();

            return Ok(perfil);
        }

        // 4. POST: api/perfilescuentas/cuenta-completa
        // Inserta una cuenta completa generando automáticamente X perfiles en el pool
        [HttpPost("cuenta-completa")]
        public async Task<IActionResult> PostCuentaCompleta([FromBody] RequestCuentaCompleta request)
        {
            if (request.IdProducto == 0) return BadRequest("El ID del producto base es obligatorio.");
            if (string.IsNullOrEmpty(request.CorreoCuenta) || string.IsNullOrEmpty(request.PasswordCuenta))
                return BadRequest("El correo y la contraseña son obligatorios.");

            var nuevosPerfiles = new List<PerfilCuenta>();

            for (int i = 1; i <= request.CantidadPerfiles; i++)
            {
                nuevosPerfiles.Add(new PerfilCuenta
                {
                    IdProducto = request.IdProducto,
                    NombrePerfil = $"Perfil {i}",
                    CorreoCuenta = request.CorreoCuenta,
                    PasswordCuenta = request.PasswordCuenta,
                    PIN = $"100{i}", // Genera automáticamente PINs: 1001, 1002, 1003...
                    Ocupado = false,
                    IdClienteAsignado = null
                });
            }

            _context.PerfilesCuentas.AddRange(nuevosPerfiles);
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = $"Se han generado exitosamente {request.CantidadPerfiles} perfiles con PINs automáticos." });
        }

        // 3. DELETE: api/perfilescuentas/{id}
        // Elimina un perfil si no está ocupado o vendido por motivos de integridad relacional
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

        // 5. PUT: api/perfilescuentas/{id}
        // Permite modificar los datos de un perfil (Nombre, PIN, Correo, Password)
        // PUT: api/perfilescuentas/{id}
        // Modifica los datos del perfil y sincroniza las credenciales en la suscripción del cliente si está ocupado
        [HttpPut("{id}")]
        public async Task<IActionResult> Put(int id, [FromBody] PerfilCuenta perfilActualizado)
        {
            if (id != perfilActualizado.Id) return BadRequest("Los IDs no coinciden.");

            var perfilExistente = await _context.PerfilesCuentas.FindAsync(id);
            if (perfilExistente == null) return NotFound("Perfil no encontrado.");

            // Actualizamos los campos en el pool de perfiles
            perfilExistente.NombrePerfil = perfilActualizado.NombrePerfil;
            perfilExistente.PIN = perfilActualizado.PIN;
            perfilExistente.CorreoCuenta = perfilActualizado.CorreoCuenta;
            perfilExistente.PasswordCuenta = perfilActualizado.PasswordCuenta;

            _context.Entry(perfilExistente).State = EntityState.Modified;

            // SI EL PERFIL ESTÁ OCUPADO: Sincronizamos las credenciales en su suscripción activa
            if (perfilExistente.Ocupado && perfilExistente.IdClienteAsignado.HasValue)
            {
                var suscripcionActiva = await _context.Suscripciones
                    .Where(s => s.IdCliente == perfilExistente.IdClienteAsignado.Value && 
                                s.IdProducto == perfilExistente.IdProducto && 
                                s.Estado == "Activa")
                    .FirstOrDefaultAsync();

                if (suscripcionActiva != null)
                {
                    // Formateamos las nuevas credenciales de manera limpia para la tabla de renovaciones
                    suscripcionActiva.DetallesCredenciales = $"Perfil: {perfilExistente.NombrePerfil} | Correo: {perfilExistente.CorreoCuenta} | Clave: {perfilExistente.PasswordCuenta} | PIN: {perfilExistente.PIN}";
                    _context.Entry(suscripcionActiva).State = EntityState.Modified;
                }
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // 6. PUT: api/perfilescuentas/{id}/liberar
        // PUT: api/perfilescuentas/{id}/liberar
        // Desvincula al cliente actual, libera el perfil y cancela su suscripción activa
        [HttpPut("{id}/liberar")]
        public async Task<IActionResult> LiberarPerfil(int id)
        {
            var perfil = await _context.PerfilesCuentas.FindAsync(id);
            if (perfil == null) return NotFound("Perfil no encontrado.");

            if (perfil.IdClienteAsignado.HasValue)
            {
                int idCliente = perfil.IdClienteAsignado.Value;
                int idProducto = perfil.IdProducto;

                // 1. Buscamos la suscripción activa de este cliente para este producto específico
                var suscripcionActiva = await _context.Suscripciones
                    .Where(s => s.IdCliente == idCliente && 
                                s.IdProducto == idProducto && 
                                s.Estado == "Activa")
                    .FirstOrDefaultAsync();

                if (suscripcionActiva != null)
                {
                    // Cambiamos el estado para que deje de figurar en la lista de renovaciones pendientes
                    suscripcionActiva.Estado = "Cancelada"; 
                    _context.Entry(suscripcionActiva).State = EntityState.Modified;
                }
            }

            // 2. Liberamos el perfil
            perfil.Ocupado = false;
            perfil.IdClienteAsignado = null;

            _context.Entry(perfil).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "El perfil ha sido liberado y su suscripción activa ha sido marcada como Cancelada." });
        }
    }
}