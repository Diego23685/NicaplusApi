using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.DTOs;
using NicaplusApi.Models;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AuthController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegistroDto dto)
        {
            // Validar si el nombre de usuario ya existe
            if (await _context.Usuarios.AnyAsync(u => u.Username.ToLower() == dto.Username.ToLower()))
            {
                return BadRequest("El nombre de usuario ya se encuentra registrado.");
            }

            // Validar que el rol exista
            var rolExiste = await _context.Roles.AnyAsync(r => r.Id == dto.IdRol);
            if (!rolExiste)
            {
                return BadRequest("El rol especificado no existe.");
            }

            // Generar hash seguro de la contraseña
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            var nuevoUsuario = new Usuario
            {
                Nombre = dto.Nombre,
                Username = dto.Username,
                PasswordHash = passwordHash,
                IdRol = dto.IdRol
            };

            _context.Usuarios.Add(nuevoUsuario);
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Usuario registrado exitosamente." });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            if (dto == null || string.IsNullOrEmpty(dto.Username) || string.IsNullOrEmpty(dto.Password))
            {
                return BadRequest("El usuario y la contraseña son requeridos.");
            }

            // Buscamos ignorando mayúsculas/minúsculas
            var usuario = await _context.Usuarios
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.Username.ToLower() == dto.Username.ToLower());

            if (usuario == null)
            {
                return Unauthorized("Credenciales de acceso incorrectas.");
            }

            // Validación del Hash de BCrypt
            bool passwordValido = BCrypt.Net.BCrypt.Verify(dto.Password, usuario.PasswordHash);
            
            if (!passwordValido)
            {
                return Unauthorized("Credenciales de acceso incorrectas.");
            }

            // Nota: Aquí se generará el JWT. Por ahora devolvemos el objeto de sesión para pruebas iniciales.
            return Ok(new
            {
                id = usuario.Id,
                nombre = usuario.Nombre,
                username = usuario.Username,
                rol = usuario.Rol?.NombreRol
            });
        }
    }
}