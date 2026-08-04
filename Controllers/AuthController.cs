using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.DTOs;
using NicaplusApi.Models;
using NicaplusApi.Services;
using NicaplusApi.DTOs.Clientes;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Net;

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
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            ApplicationDbContext context,
            JwtService jwtService,
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<AuthController> logger)
        {
            _context = context;
            _jwtService = jwtService;
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
        }

        #region Endpoints Administrativos / Usuarios

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegistroDto dto)
        {
            try
            {
                if (await _context.Usuarios.AnyAsync(u => u.Username.ToLower() == dto.Username.ToLower()))
                {
                    return BadRequest(new { mensaje = "El nombre de usuario ya se encuentra registrado." });
                }

                var rolExiste = await _context.Roles.AnyAsync(r => r.Id == dto.IdRol);
                if (!rolExiste)
                {
                    return BadRequest(new { mensaje = "El rol especificado no existe." });
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar usuario administrativo.");
                return StatusCode(500, new { mensaje = "Error interno al registrar el usuario." });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            try
            {
                if (dto == null || string.IsNullOrEmpty(dto.Username) || string.IsNullOrEmpty(dto.Password))
                {
                    return BadRequest(new { mensaje = "El usuario y la contraseña son requeridos." });
                }

                var usuario = await _context.Usuarios
                    .Include(u => u.Rol)
                    .FirstOrDefaultAsync(u => u.Username.ToLower() == dto.Username.ToLower());

                if (usuario == null || !BCrypt.Net.BCrypt.Verify(dto.Password, usuario.PasswordHash))
                {
                    return Unauthorized(new { mensaje = "Credenciales de acceso incorrectas." });
                }

                var token = _jwtService.GenerarTokenUsuario(usuario);
                return Ok(new { token });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante el proceso de login de usuario administrativo.");
                return StatusCode(500, new { mensaje = "Error interno al procesar el inicio de sesión." });
            }
        }

        #endregion

        #region Registro y Confirmación de Clientes

        [HttpPost("registro-cliente")]
        public async Task<IActionResult> RegistroCliente([FromBody] ClienteRegistroDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { mensaje = "Datos de formulario inválidos.", detalles = ModelState });

            try
            {
                var emailLimpio = dto.Email.Trim().ToLower();

                if (await _context.Clientes.AnyAsync(c => c.Email.ToLower() == emailLimpio))
                {
                    return BadRequest(new { mensaje = "Ya existe una cuenta registrada con ese correo electrónico." });
                }

                if (await _context.Clientes.AnyAsync(c => c.Telefono == dto.Telefono))
                {
                    return BadRequest(new { mensaje = "El número de teléfono ya se encuentra registrado." });
                }

                var tokenConfirmacion = Guid.NewGuid().ToString("N");
                var expiracion = DateTime.UtcNow.AddMinutes(15);

                var cliente = new Cliente
                {
                    Nombre = dto.Nombre,
                    Telefono = dto.Telefono,
                    Email = emailLimpio,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                    FechaRegistro = DateTime.UtcNow,
                    PuntosAcumulados = 0,
                    EmailConfirmado = false,
                    TokenConfirmacion = tokenConfirmacion,  
                    ExpiracionTokenConfirmacion = expiracion,
                    Activo = true
                };

                _context.Clientes.Add(cliente);
                await _context.SaveChangesAsync();

                // Intento aislado de envío de correo para no revertir/romper la cuenta si falla el servidor SMTP
                var frontend = _configuration["Frontend:Url"];
                var enlace = $"{frontend}/confirmar-email?token={tokenConfirmacion}";
                var nombreSeguro = WebUtility.HtmlEncode(cliente.Nombre);

                var html = ObtenerPlantillaCorreoConfirmacion(nombreSeguro, enlace);

                try
                {
                    await _emailService.EnviarCorreoAsync(cliente.Email, "Confirma tu cuenta de Nicaplus", html);
                }
                catch (Exception mailEx)
                {
                    _logger.LogError(mailEx, "No se pudo enviar el correo de confirmación al cliente {Email}", cliente.Email);
                    return Ok(new { 
                        mensaje = "Registro exitoso, pero ocurrió un inconveniente al enviar el correo. Por favor, solicita el reenvío de confirmación." 
                    });
                }

                return Ok(new { mensaje = "Registro exitoso. Por favor, revisa tu correo electrónico para activar tu cuenta." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al registrar nuevo cliente.");
                return StatusCode(500, new { mensaje = "Error interno del servidor al procesar el registro." });
            }
        }

        [HttpPost("login-cliente")]
        public async Task<IActionResult> LoginCliente([FromBody] ClienteLoginDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { mensaje = "Datos incompletos o en formato incorrecto." });

            try
            {
                var emailLimpio = dto.Email.Trim().ToLower();
                var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.Email.ToLower() == emailLimpio);

                if (cliente == null || !BCrypt.Net.BCrypt.Verify(dto.Password, cliente.PasswordHash))
                {
                    return Unauthorized(new { mensaje = "Correo o contraseña incorrectos." });
                }

                if (!cliente.Activo)
                {
                    return Unauthorized(new { mensaje = "Tu cuenta se encuentra temporalmente suspendida o inactiva. Contacta a soporte." });
                }

                if (!cliente.EmailConfirmado)
                {
                    return Unauthorized(new { mensaje = "Debes confirmar tu correo electrónico antes de iniciar sesión." });
                }

                cliente.UltimoAcceso = DateTime.UtcNow;
                await _context.SaveChangesAsync();

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al procesar el inicio de sesión del cliente.");
                return StatusCode(500, new { mensaje = "Error al iniciar sesión en el servidor." });
            }
        }

        [HttpGet("confirmar-email")]
        public async Task<IActionResult> ConfirmarEmail([FromQuery] string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest(new { mensaje = "El token de confirmación es requerido." });

            try
            {
                var tokenLimpio = token.Trim().ToLower();

                var cliente = await _context.Clientes
                    .FirstOrDefaultAsync(c => c.TokenConfirmacion!.ToLower() == tokenLimpio);

                if (cliente == null)
                {
                    _logger.LogWarning("Intento de confirmación con token no existente o ya usado: '{Token}'", tokenLimpio);
                    return BadRequest(new { mensaje = "El enlace no es válido o ya fue utilizado." });
                }

                var fechaExpiracionUtc = DateTime.SpecifyKind(cliente.ExpiracionTokenConfirmacion!.Value, DateTimeKind.Utc);

                if (fechaExpiracionUtc < DateTime.UtcNow)
                {
                    return BadRequest(new { mensaje = "El enlace ya expiró. Por favor, solicita uno nuevo desde la pantalla de ingreso." });
                }

                cliente.EmailConfirmado = true;
                cliente.TokenConfirmacion = null;
                cliente.ExpiracionTokenConfirmacion = null;

                await _context.SaveChangesAsync();

                return Ok(new { mensaje = "¡Tu cuenta ha sido activada con éxito! Ya puedes iniciar sesión." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al activar la cuenta por token.");
                return StatusCode(500, new { mensaje = "Ocurrió un error interno al confirmar la cuenta." });
            }
        }

        #endregion

        #region Perfil de Cliente

        [Authorize]
        [HttpGet("perfil-cliente")]
        public async Task<IActionResult> PerfilCliente()
        {
            try
            {
                if (!EsClienteValido(out int idCliente, out string? error))
                    return Unauthorized(new { mensaje = error });

                var cliente = await _context.Clientes.FindAsync(idCliente);
                if (cliente == null) return NotFound(new { mensaje = "Cliente no encontrado." });

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar el perfil del cliente.");
                return StatusCode(500, new { mensaje = "Error al obtener la información del perfil." });
            }
        }

        [Authorize]
        [HttpPut("perfil-cliente")]
        public async Task<IActionResult> ActualizarPerfil([FromBody] ClienteActualizarPerfilDto dto)
        {
            try
            {
                if (!EsClienteValido(out int idCliente, out string? error))
                    return Unauthorized(new { mensaje = error });

                var cliente = await _context.Clientes.FindAsync(idCliente);
                if (cliente == null) return NotFound(new { mensaje = "Cliente no encontrado." });

                var emailLimpio = dto.Email.ToLower().Trim();

                if (await _context.Clientes.AnyAsync(c => c.Email == emailLimpio && c.Id != idCliente))
                {
                    return BadRequest(new { mensaje = "Ese correo ya está registrado por otra cuenta." });
                }

                if (await _context.Clientes.AnyAsync(c => c.Telefono == dto.Telefono && c.Id != idCliente))
                {
                    return BadRequest(new { mensaje = "Ese número de teléfono ya está registrado por otra cuenta." });
                }

                cliente.Nombre = dto.Nombre;
                cliente.Telefono = dto.Telefono;
                cliente.Email = emailLimpio;

                await _context.SaveChangesAsync();

                return Ok(new { mensaje = "Perfil actualizado correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar perfil de cliente.");
                return StatusCode(500, new { mensaje = "Error interno al actualizar los datos." });
            }
        }

        [Authorize]
        [HttpPut("cambiar-password")]
        public async Task<IActionResult> CambiarPassword([FromBody] CambiarPasswordDto dto)
        {
            try
            {
                if (!EsClienteValido(out int idCliente, out string? error))
                    return Unauthorized(new { mensaje = error });

                var cliente = await _context.Clientes.FindAsync(idCliente);
                if (cliente == null) return NotFound(new { mensaje = "Cliente no encontrado." });

                if (!BCrypt.Net.BCrypt.Verify(dto.PasswordActual, cliente.PasswordHash))
                {
                    return BadRequest(new { mensaje = "La contraseña actual es incorrecta." });
                }

                cliente.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.PasswordNueva);
                await _context.SaveChangesAsync();

                return Ok(new { mensaje = "Contraseña actualizada correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cambiar contraseña del cliente.");
                return StatusCode(500, new { mensaje = "Error interno al actualizar la contraseña." });
            }
        }

        [Authorize]
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            return Ok(new { mensaje = "Sesión finalizada." });
        }

        #endregion

        #region Recuperación y Reenvío de Tokens

        [HttpPost("recuperar-password")]
        public async Task<IActionResult> RecuperarPassword([FromBody] SolicitarRecuperacionDto dto)
        {
            try
            {
                var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.Email.ToLower() == dto.Email.ToLower().Trim());

                if (cliente == null)
                {
                    return Ok(new { mensaje = "Si el correo existe, recibirás instrucciones para recuperar tu contraseña." });
                }

                if (cliente.TokenRecuperacion != null && cliente.ExpiracionTokenRecuperacion > DateTime.UtcNow)
                {
                    return Ok(new { mensaje = "Ya existe una solicitud de recuperación activa. Revisa tu correo o espera 15 minutos." });
                }

                cliente.TokenRecuperacion = Guid.NewGuid().ToString("N");
                cliente.ExpiracionTokenRecuperacion = DateTime.UtcNow.AddMinutes(15);

                await _context.SaveChangesAsync();

                var frontend = _configuration["Frontend:Url"];
                var enlace = $"{frontend}/restablecer-password?token={cliente.TokenRecuperacion}";

                var html = $@"
                    <h2>Recuperación de contraseña - NICAPLUS</h2>
                    <p>Haz clic en el siguiente enlace para crear una nueva contraseña.</p>
                    <p><a href='{enlace}'>Restablecer contraseña</a></p>
                    <p>Este enlace es válido por 15 minutos. Si no solicitaste este cambio, ignora este correo.</p>";

                try
                {
                    await _emailService.EnviarCorreoAsync(cliente.Email, "Recuperación de contraseña - Nicaplus", html);
                }
                catch (Exception mailEx)
                {
                    _logger.LogError(mailEx, "Error al enviar el correo de recuperación de contraseña.");
                }

                return Ok(new { mensaje = "Si el correo existe, recibirás instrucciones para recuperar tu contraseña." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en solicitud de recuperación de contraseña.");
                return StatusCode(500, new { mensaje = "Error al procesar la solicitud de recuperación." });
            }
        }

        [HttpPost("restablecer-password")]
        public async Task<IActionResult> RestablecerPassword([FromBody] RestablecerPasswordDto dto)
        {
            try
            {
                var cliente = await _context.Clientes
                    .FirstOrDefaultAsync(c => c.TokenRecuperacion == dto.Token && c.ExpiracionTokenRecuperacion > DateTime.UtcNow);

                if (cliente == null)
                {
                    return BadRequest(new { mensaje = "El enlace no es válido o ha expirado." });
                }

                cliente.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NuevaPassword);
                cliente.TokenRecuperacion = null;
                cliente.ExpiracionTokenRecuperacion = null;

                await _context.SaveChangesAsync();

                return Ok(new { mensaje = "La contraseña fue actualizada correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al restablecer la contraseña.");
                return StatusCode(500, new { mensaje = "Error interno al guardar la nueva contraseña." });
            }
        }

        [HttpPost("reenviar-confirmacion")]
        public async Task<IActionResult> ReenviarConfirmacion([FromBody] ReenviarConfirmacionDto dto)
        {
            try
            {
                var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.Email.ToLower() == dto.Email.ToLower().Trim());

                if (cliente == null)
                {
                    return Ok(new { mensaje = "Si la cuenta existe, se enviará un nuevo correo de confirmación." });
                }

                if (cliente.EmailConfirmado)
                {
                    return Ok(new { mensaje = "La cuenta ya se encuentra confirmada." });
                }

                if (cliente.TokenConfirmacion != null && cliente.ExpiracionTokenConfirmacion > DateTime.UtcNow)
                {
                    return Ok(new { mensaje = "Ya existe un correo de confirmación vigente. Revisa tu bandeja de entrada o de spam." });
                }

                cliente.TokenConfirmacion = Guid.NewGuid().ToString("N");
                cliente.ExpiracionTokenConfirmacion = DateTime.UtcNow.AddMinutes(15);

                await _context.SaveChangesAsync();

                var frontend = _configuration["Frontend:Url"];
                var enlace = $"{frontend}/confirmar-email?token={cliente.TokenConfirmacion}";

                var html = $@"
                    <h2>Confirma tu cuenta en Nicaplus</h2>
                    <p>Haz clic en el siguiente enlace para activar tu cuenta.</p>
                    <p><a href='{enlace}'>Confirmar correo</a></p>
                    <p>Este enlace expirará en 15 minutos.</p>";

                try
                {
                    await _emailService.EnviarCorreoAsync(cliente.Email, "Confirma tu cuenta de Nicaplus", html);
                }
                catch (Exception mailEx)
                {
                    _logger.LogError(mailEx, "Error al enviar el correo de confirmación reenviado.");
                }

                return Ok(new { mensaje = "Si la cuenta existe, se ha enviado un nuevo correo de confirmación." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al reenviar la confirmación.");
                return StatusCode(500, new { mensaje = "Error al procesar el reenvío de confirmación." });
            }
        }

        [HttpGet("validar-token-confirmacion")]
        public async Task<IActionResult> ValidarTokenConfirmacion([FromQuery] string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return BadRequest(new { valido = false, mensaje = "Token inválido o vacío." });
            }

            try
            {
                var tokenLimpio = token.Trim().ToLower();
                var valido = await _context.Clientes.AnyAsync(c =>
                    c.TokenConfirmacion!.ToLower() == tokenLimpio &&
                    c.ExpiracionTokenConfirmacion > DateTime.UtcNow &&
                    !c.EmailConfirmado);

                return Ok(new { valido });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al validar existencia de token.");
                return StatusCode(500, new { valido = false, mensaje = "Error en el servidor al comprobar token." });
            }
        }

        #endregion

        #region Helpers Privados

        private bool EsClienteValido(out int idCliente, out string? error)
        {
            idCliente = 0;
            error = null;

            var tipoUsuario = User.FindFirst("TipoUsuario")?.Value;
            if (tipoUsuario != "Cliente")
            {
                error = "Este token no pertenece a un cliente.";
                return false;
            }

            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(idClaim) || !int.TryParse(idClaim, out idCliente))
            {
                error = "Identificador de usuario inválido en el token.";
                return false;
            }

            return true;
        }

        private string ObtenerPlantillaCorreoConfirmacion(string nombreSeguro, string enlace)
        {
            return $@"
            <!DOCTYPE html>
            <html lang='es'>
            <head>
                <meta charset='UTF-8'>
                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                <title>Activa tu cuenta en Nicaplus</title>
            </head>
            <body style='margin: 0; padding: 0; background-color: #0b0f19; font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif; color: #ffffff;'>
                <table cellpadding='0' cellspacing='0' width='100%' style='background-color: #0b0f19; min-height: 100vh; padding: 40px 20px;'>
                    <tr>
                        <td align='center' valign='top'>
                            <table cellpadding='0' cellspacing='0' width='100%' style='max-width: 600px; background-color: #131b2e; border-radius: 16px; border: 1px solid #7c3aed; overflow: hidden; box-shadow: 0 0 20px rgba(124, 58, 237, 0.15);'>
                                <tr>
                                    <td align='center' style='padding: 35px 40px; background: linear-gradient(180deg, #2e1065 0%, #131b2e 100%); border-bottom: 1px solid #4c1d95;'>
                                        <h1 style='margin: 0; font-size: 28px; font-weight: 900; letter-spacing: 1.5px; color: #ffffff; text-transform: uppercase;'>
                                            NICAPLUS<span style='color: #a78bfa; text-shadow: 0 0 10px rgba(167, 139, 250, 0.6);'> GAMING & TECH</span>
                                        </h1>
                                        <p style='margin: 6px 0 0 0; font-size: 11px; color: #cbd5e1; font-weight: 600; letter-spacing: 3px; text-transform: uppercase;'>Soporte y Ventas Oficial</p>
                                    </td>
                                </tr>
                                <tr>
                                    <td style='padding: 40px; text-align: left;'>
                                        <h2 style='margin-top: 0; margin-bottom: 20px; font-size: 24px; font-weight: 700; color: #c084fc;'>
                                            ¡Hola, {nombreSeguro}! 👋
                                        </h2>
                                        <p style='margin: 0 0 20px 0; font-size: 15px; line-height: 1.6; color: #94a3b8;'>
                                            Te damos la bienvenida a nuestra plataforma. Para completar tu registro de forma segura, activa tu cuenta haciendo clic en el siguiente botón (expirará en <strong>15 minutos</strong>):
                                        </p>
                                        <table cellpadding='0' cellspacing='0' width='100%' style='margin-bottom: 35px;'>
                                            <tr>
                                                <td align='center'>
                                                    <a href='{enlace}' style='display: inline-block; padding: 15px 36px; background-color: #7c3aed; color: #ffffff; font-size: 15px; font-weight: bold; text-decoration: none; border-radius: 8px; text-transform: uppercase; letter-spacing: 1px; box-shadow: 0 4px 12px rgba(124, 58, 237, 0.3); border: 1px solid #a78bfa;'>
                                                        Confirmar mi cuenta
                                                    </a>
                                                </td>
                                            </tr>
                                        </table>
                                        <table cellpadding='0' cellspacing='0' width='100%' style='background-color: #1e1b4b; border-left: 4px solid #a78bfa; border-radius: 4px;'>
                                            <tr>
                                                <td style='padding: 15px; color: #94a3b8; font-size: 13px; line-height: 1.5; font-style: italic;'>
                                                    Si no solicitaste la creación de esta cuenta, puedes ignorar este mensaje.
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                                <tr>
                                    <td style='padding: 30px 40px; background-color: #0b0f19; border-top: 1px solid #1e293b; text-align: center;'>
                                        <p style='margin: 0 0 8px 0; font-size: 12px; color: #64748b;'>
                                            © {DateTime.UtcNow.Year} NICAPLUS. Todos los derechos reservados.
                                        </p>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>
                </table>
            </body>
            </html>";
        }

        #endregion
    }
}