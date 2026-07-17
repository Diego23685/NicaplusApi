using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.DTOs;
using NicaplusApi.Models;
using NicaplusApi.Services;
using NicaplusApi.DTOs.Clientes;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Net; // Requerido para WebUtility.HtmlEncode

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly JwtService _jwtService;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;

        public AuthController(
            ApplicationDbContext context,
            JwtService jwtService,
            IEmailService emailService,
            IConfiguration configuration)
        {
            _context = context;
            _jwtService = jwtService;
            _emailService = emailService;
            _configuration = configuration;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegistroDto dto)
        {
            if (await _context.Usuarios.AnyAsync(u => u.Username.ToLower() == dto.Username.ToLower()))
            {
                return BadRequest("El nombre de usuario ya se encuentra registrado.");
            }

            var rolExiste = await _context.Roles.AnyAsync(r => r.Id == dto.IdRol);
            if (!rolExiste)
            {
                return BadRequest("El rol especificado no existe.");
            }

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

            var usuario = await _context.Usuarios
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.Username.ToLower() == dto.Username.ToLower());

            if (usuario == null)
            {
                return Unauthorized("Credenciales de acceso incorrectas.");
            }

            bool passwordValido = BCrypt.Net.BCrypt.Verify(dto.Password, usuario.PasswordHash);

            if (!passwordValido)
            {
                return Unauthorized("Credenciales de acceso incorrectas.");
            }

            var token = _jwtService.GenerarTokenUsuario(usuario);

            return Ok(new { token });
        }

        [HttpPost("registro-cliente")]
        public async Task<IActionResult> RegistroCliente([FromBody] ClienteRegistroDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (await _context.Clientes.AnyAsync(c => c.Email.ToLower() == dto.Email.ToLower()))
            {
                return BadRequest("Ya existe una cuenta con ese correo.");
            }

            if (await _context.Clientes.AnyAsync(c => c.Telefono == dto.Telefono))
            {
                return BadRequest("Ese teléfono ya está registrado.");
            }

            var tokenConfirmacion = Guid.NewGuid().ToString("N");
            var expiracion = DateTime.UtcNow.AddMinutes(15); // UTC estricto

            var cliente = new Cliente
            {
                Nombre = dto.Nombre,
                Telefono = dto.Telefono,
                Email = dto.Email.Trim().ToLower(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                FechaRegistro = DateTime.UtcNow, // Guardado en UTC
                PuntosAcumulados = 0,
                EmailConfirmado = false,
                TokenConfirmacion = tokenConfirmacion,  
                ExpiracionTokenConfirmacion = expiracion,
                Activo = true
            };

            _context.Clientes.Add(cliente);
            await _context.SaveChangesAsync();

            var frontend = _configuration["Frontend:Url"];
            var enlace = $"{frontend}/confirmar-email?token={tokenConfirmacion}";
            
            // Sanitización del nombre para prevenir XSS en el cliente de correo
            var nombreSeguro = WebUtility.HtmlEncode(cliente.Nombre);

            var html = $@"
            <!DOCTYPE html>
            <html lang='es'>
            <head>
                <meta charset='UTF-8'>
                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                <title>Activa tu cuenta en Nicaplus Gaming</title>
            </head>
            <body style='margin: 0; padding: 0; background-color: #0f172a; font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif; color: #ffffff;'>
                <table cellpadding='0' cellspacing='0' width='100%' style='background-color: #0f172a; min-height: 100vh; padding: 40px 20px;'>
                    <tr>
                        <td align='center' valign='top'>
                            <!-- Contenedor Principal -->
                            <table cellpadding='0' cellspacing='0' width='100%' style='max-width: 600px; background-color: #1e293b; border-radius: 16px; border: 1px solid #334155; overflow: hidden; box-shadow: 0 10px 15px -3px rgba(0, 0, 0, 0.3);'>
                                
                                <!-- Banner de Encabezado -->
                                <tr>
                                    <td align='center' style='padding: 30px 40px; border-bottom: 3px solid #fb923c; background: linear-gradient(135deg, #1e293b 0%, #0f172a 100%);'>
                                        <h1 style='margin: 0; font-size: 28px; font-weight: 800; letter-spacing: 1px; color: #ffffff; text-transform: uppercase;'>
                                            NICAPLUS<span style='color: #581c7e;'> GAMING</span>
                                        </h1>
                                        <p style='margin: 5px 0 0 0; font-size: 13px; color: #581c7e; font-weight: 600; letter-spacing: 2px; text-transform: uppercase;'>Soporte y Ventas Oficial</p>
                                    </td>
                                </tr>

                                <!-- Cuerpo del Correo -->
                                <tr>
                                    <td style='padding: 40px; text-align: left;'>
                                        <h2 style='margin-top: 0; margin-bottom: 20px; font-size: 22px; font-weight: 700; color: #581c7e;'>
                                            ¡Hola, {nombreSeguro}! 👋
                                        </h2>
                                        
                                        <p style='margin: 0 0 20px 0; font-size: 15px; line-height: 1.6; color: #cbd5e1;'>
                                            Te damos la más cordial bienvenida a nuestra plataforma. Estamos muy emocionados de tenerte con nosotros en la comunidad de <strong>NICAPLUS</strong>.
                                        </p>
                                        
                                        <p style='margin: 0 0 30px 0; font-size: 15px; line-height: 1.6; color: #cbd5e1;'>
                                            Para completar tu registro y asegurar tu cuenta de forma exitosa, por favor haz clic en el siguiente botón de activación (este enlace expirará en <strong>15 minutos</strong> por tu seguridad):
                                        </p>

                                        <!-- Botón de Acción -->
                                        <table cellpadding='0' cellspacing='0' width='100%' style='margin-bottom: 30px;'>
                                            <tr>
                                                <td align='center'>
                                                    <a href='{enlace}' style='display: inline-block; padding: 14px 32px; background-color: #581c7e; color: #000000; font-size: 15px; font-weight: bold; text-decoration: none; border-radius: 8px; text-transform: uppercase; letter-spacing: 0.5px; transition: background-color 0.2s;'>
                                                        Confirmar mi cuenta
                                                    </a>
                                                </td>
                                            </tr>
                                        </table>

                                        <p style='margin: 0; font-size: 14px; line-height: 1.5; color: #64748b; font-style: italic; border-left: 3px solid #334155; padding-left: 15px;'>
                                            Si no solicitaste la creación de esta cuenta o crees que se trata de un error, puedes ignorar este correo con total tranquilidad.
                                        </p>
                                    </td>
                                </tr>

                                <!-- Pie de Página -->
                                <tr>
                                    <td style='padding: 30px 40px; background-color: #0f172a; border-top: 1px solid #334155; text-align: center;'>
                                        <p style='margin: 0 0 10px 0; font-size: 12px; color: #94a3b8;'>
                                            © {DateTime.Now.Year} NICAPLUS GAMING. Todos los derechos reservados.
                                        </p>
                                        <p style='margin: 0; font-size: 11px; color: #64748b;'>
                                            León, Nicaragua. Este es un correo automático, por favor no respondas directamente a esta dirección.
                                        </p>
                                    </td>
                                </tr>

                            </table>
                        </td>
                    </tr>
                </table>
            </body>
            </html>";

            await _emailService.EnviarCorreoAsync(
                cliente.Email,
                "Confirma tu cuenta de Nicaplus",
                html);

            // CORRECCIÓN: Quitamos la generación automática de Token para forzar la verificación de correo
            return Ok(new 
            { 
                mensaje = "Registro exitoso. Por favor, revisa tu correo electrónico para activar tu cuenta." 
            });
        }

        [HttpPost("login-cliente")]
        public async Task<IActionResult> LoginCliente([FromBody] ClienteLoginDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Buscamos al cliente por correo
            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.Email.ToLower() == dto.Email.ToLower());

            // 1. Validación de existencia
            if (cliente == null)
                return Unauthorized("Correo o contraseña incorrectos.");

            // 2. NUEVA VALIDACIÓN: Verificar si la cuenta está activa
            if (!cliente.Activo)
            {
                return Unauthorized("Tu cuenta se encuentra temporalmente suspendida o inactiva. Por favor, contacta al soporte.");
            }

            // 3. Validación de correo confirmado
            if (!cliente.EmailConfirmado)
            {
                return Unauthorized("Debes confirmar tu correo electrónico antes de iniciar sesión.");
            }

            // 4. Validación de contraseña
            bool passwordCorrecto = BCrypt.Net.BCrypt.Verify(dto.Password, cliente.PasswordHash);

            if (!passwordCorrecto)
                return Unauthorized("Correo o contraseña incorrectos.");

            // Registro de auditoría del último acceso (en UTC)
            cliente.UltimoAcceso = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Generación del Token JWT
            var token = _jwtService.GenerarTokenCliente(cliente);

            return Ok(new ClienteAuthResponseDto
            {
                Token = token,
                Cliente = new ClientePerfilDto
                {
                    Id = cliente.Id,
                    Nombre = cliente.Nombre,
                    Telefono = cliente.Telefono,
                    Email = cliente.Email,
                    FechaRegistro = cliente.FechaRegistro,
                    PuntosAcumulados = cliente.PuntosAcumulados
                }
            });
        }

        [Authorize]
        [HttpGet("perfil-cliente")]
        public async Task<IActionResult> PerfilCliente()
        {
            var tipoUsuario = User.FindFirst("TipoUsuario")?.Value;

            if (tipoUsuario != "Cliente")
            {
                return Unauthorized("Este token no pertenece a un cliente.");
            }

            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(idClaim))
            {
                return Unauthorized();
            }

            int idCliente = int.Parse(idClaim);
            var cliente = await _context.Clientes.FindAsync(idCliente);

            if (cliente == null)
            {
                return NotFound();
            }

            return Ok(new ClientePerfilDto
            {
                Id = cliente.Id,
                Nombre = cliente.Nombre,
                Telefono = cliente.Telefono,
                Email = cliente.Email,
                FechaRegistro = cliente.FechaRegistro,
                PuntosAcumulados = cliente.PuntosAcumulados
            });
        }

        [Authorize]
        [HttpPut("perfil-cliente")]
        public async Task<IActionResult> ActualizarPerfil([FromBody] ClienteActualizarPerfilDto dto)
        {
            var tipo = User.FindFirst("TipoUsuario")?.Value;

            if (tipo != "Cliente")
                return Unauthorized();

            int idCliente = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var cliente = await _context.Clientes.FindAsync(idCliente);

            if (cliente == null)
                return NotFound();

            if (await _context.Clientes.AnyAsync(c => c.Email == dto.Email && c.Id != idCliente))
            {
                return BadRequest("Ese correo ya está registrado.");
            }

            if (await _context.Clientes.AnyAsync(c => c.Telefono == dto.Telefono && c.Id != idCliente))
            {
                return BadRequest("Ese teléfono ya está registrado.");
            }

            cliente.Nombre = dto.Nombre;
            cliente.Telefono = dto.Telefono;
            cliente.Email = dto.Email.ToLower().Trim();

            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Perfil actualizado correctamente." });
        }

        [Authorize]
        [HttpPut("cambiar-password")]
        public async Task<IActionResult> CambiarPassword(CambiarPasswordDto dto)
        {
            var tipo = User.FindFirst("TipoUsuario")?.Value;

            if (tipo != "Cliente")
                return Unauthorized();

            int idCliente = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var cliente = await _context.Clientes.FindAsync(idCliente);

            if (cliente == null)
                return NotFound();

            bool correcta = BCrypt.Net.BCrypt.Verify(dto.PasswordActual, cliente.PasswordHash);

            if (!correcta)
                return BadRequest("La contraseña actual es incorrecta.");

            cliente.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.PasswordNueva);
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "Contraseña actualizada correctamente." });
        }

        [Authorize]
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            return Ok(new { mensaje = "Sesión finalizada." });
        }

        [HttpGet("confirmar-email")]
        public async Task<IActionResult> ConfirmarEmail([FromQuery] string token) // ◄ 1. Aseguramos que lea explícitamente desde la Query URL
        {
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest("El token de confirmación es requerido.");

            // ◄ 2. LIMPIEZA TOTAL: Eliminamos espacios al inicio/final y lo convertimos a minúsculas
            var tokenLimpio = token.Trim().ToLower();

            // ◄ 3. BUSQUEDA TOLERANTE: Comparamos en minúsculas para evitar problemas de enrutamiento/encoding
            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.TokenConfirmacion!.ToLower() == tokenLimpio);

            if (cliente == null)
            {
                // Esto se imprimirá en la terminal negra de tu backend para que verifiques qué está llegando
                Console.WriteLine($"[NICAPLUS DEBUG] No se encontró cliente para el token procesado: '{tokenLimpio}'");
                return BadRequest("El enlace no es válido o ya fue utilizado.");
            }

            // 4. Evaluamos la expiración estrictamente en C# usando UTC
            var fechaExpiracionUtc = DateTime.SpecifyKind(cliente.ExpiracionTokenConfirmacion!.Value, DateTimeKind.Utc);
            var ahoraUtc = DateTime.UtcNow;

            if (fechaExpiracionUtc < ahoraUtc)
            {
                return BadRequest("El enlace ya expiró. Por favor, solicita uno nuevo desde la pantalla de ingreso.");
            }

            // 5. Si todo es correcto, liberamos la cuenta
            cliente.EmailConfirmado = true;
            cliente.TokenConfirmacion = null;
            cliente.ExpiracionTokenConfirmacion = null;

            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "¡Tu cuenta ha sido activada con éxito! Ya puedes iniciar sesión." });
        }

        [HttpPost("recuperar-password")]
        public async Task<IActionResult> RecuperarPassword([FromBody] SolicitarRecuperacionDto dto)
        {
            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.Email.ToLower() == dto.Email.ToLower());

            if (cliente == null)
            {
                return Ok(new { mensaje = "Si el correo existe, recibirás instrucciones para recuperar tu contraseña." });
            }

            // CORRECCIÓN: Comparación utilizando estrictamente DateTime.UtcNow
            if (cliente.TokenRecuperacion != null && cliente.ExpiracionTokenRecuperacion > DateTime.UtcNow)
            {
                return Ok(new { mensaje = "Ya existe una solicitud de recuperación activa. Revisa tu correo o espera 15 minutos antes de solicitar otra." });
            }

            cliente.TokenRecuperacion = Guid.NewGuid().ToString("N");
            cliente.ExpiracionTokenRecuperacion = DateTime.UtcNow.AddMinutes(15); // Guardado en UTC

            await _context.SaveChangesAsync();

            var frontend = _configuration["Frontend:Url"];
            var enlace = $"{frontend}/restablecer-password?token={cliente.TokenRecuperacion}";

            var html = $@"
                <h2>Recuperación de contraseña</h2>
                <p>Haz clic en el siguiente botón para crear una nueva contraseña.</p>
                <p><a href='{enlace}'>Restablecer contraseña</a></p>
                <p>Si no solicitaste este cambio puedes ignorar este correo.</p>";

            await _emailService.EnviarCorreoAsync(
                cliente.Email,
                "Recuperación de contraseña",
                html);

            return Ok(new { mensaje = "Si el correo existe, recibirás instrucciones para recuperar tu contraseña." });
        }

        [HttpPost("restablecer-password")]
        public async Task<IActionResult> RestablecerPassword([FromBody] RestablecerPasswordDto dto)
        {
            // CORRECCIÓN: Comparación utilizando estrictamente DateTime.UtcNow
            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.TokenRecuperacion == dto.Token && c.ExpiracionTokenRecuperacion > DateTime.UtcNow);

            if (cliente == null)
            {
                return BadRequest("El enlace ya no es válido.");
            }

            cliente.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NuevaPassword);
            cliente.TokenRecuperacion = null;
            cliente.ExpiracionTokenRecuperacion = null;

            await _context.SaveChangesAsync();

            return Ok(new { mensaje = "La contraseña fue actualizada correctamente." });
        }

        [HttpPost("reenviar-confirmacion")]
        public async Task<IActionResult> ReenviarConfirmacion([FromBody] ReenviarConfirmacionDto dto)
        {
            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.Email.ToLower() == dto.Email.ToLower());

            if (cliente == null)
            {
                return Ok(new { mensaje = "Si la cuenta existe, se enviará un nuevo correo de confirmación." });
            }

            if (cliente.EmailConfirmado)
            {
                return Ok(new { mensaje = "La cuenta ya se encuentra confirmada." });
            }

            // CORRECCIÓN: Comparación utilizando estrictamente DateTime.UtcNow
            if (cliente.TokenConfirmacion != null && cliente.ExpiracionTokenConfirmacion > DateTime.UtcNow)
            {
                return Ok(new { mensaje = "Ya existe un correo de confirmación vigente. Revisa tu bandeja de entrada." });
            }

            cliente.TokenConfirmacion = Guid.NewGuid().ToString("N");
            cliente.ExpiracionTokenConfirmacion = DateTime.UtcNow.AddMinutes(15); // Guardado en UTC

            await _context.SaveChangesAsync();

            var frontend = _configuration["Frontend:Url"];
            var enlace = $"{frontend}/confirmar-email?token={cliente.TokenConfirmacion}";

            var html = $@"
                <h2>Confirma tu cuenta</h2>
                <p>Haz clic en el siguiente enlace para activar tu cuenta.</p>
                <p><a href='{enlace}'>Confirmar correo</a></p>
                <p>Este enlace expirará en 15 minutos.</p>";

            await _emailService.EnviarCorreoAsync(
                cliente.Email,
                "Confirma tu cuenta de Nicaplus",
                html);

            return Ok(new { mensaje = "Si la cuenta existe, se ha enviado un nuevo correo de confirmación." });
        }

        [HttpGet("validar-token-confirmacion")]
        public async Task<IActionResult> ValidarTokenConfirmacion(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return BadRequest(new { valido = false, mensaje = "Token inválido." });
            }

            // CORRECCIÓN: Comparación utilizando estrictamente DateTime.UtcNow
            var valido = await _context.Clientes.AnyAsync(c =>
                c.TokenConfirmacion == token &&
                c.ExpiracionTokenConfirmacion > DateTime.UtcNow &&
                !c.EmailConfirmado);

            return Ok(new { valido });
        }
    }
}