using Microsoft.IdentityModel.Tokens;
using NicaplusApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace NicaplusApi.Services
{
    public class JwtService
    {
        private readonly IConfiguration _configuration;

        public JwtService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerarTokenCliente(Cliente cliente)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, cliente.Id.ToString()),
                new Claim(ClaimTypes.Name, cliente.Nombre),
                new Claim(ClaimTypes.Email, cliente.Email),

                new Claim("TipoUsuario", "Cliente")
            };

            return GenerarToken(claims);
        }

        public string GenerarTokenUsuario(Usuario usuario)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Name, usuario.Username),

                new Claim(ClaimTypes.Role, usuario.Rol?.NombreRol ?? "Ventas"),

                new Claim("TipoUsuario", "Administrador")
            };

            return GenerarToken(claims);
        }

        private string GenerarToken(IEnumerable<Claim> claims)
        {
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!)
            );

            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256
            );

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(
                    Convert.ToDouble(_configuration["Jwt:ExpireHours"])
                ),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}