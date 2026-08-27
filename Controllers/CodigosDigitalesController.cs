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
    [Authorize]
    public class CodigosDigitalesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CodigosDigitalesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/CodigosDigitales/producto/5
        [HttpGet("producto/{idProducto}")]
        public async Task<IActionResult> GetCodigosPorProducto(int idProducto, [FromQuery] bool soloDisponibles = true)
        {
            var query = _context.CodigosDigitales
                .AsNoTracking()
                .Where(c => c.IdProducto == idProducto);

            if (soloDisponibles)
                query = query.Where(c => !c.Vendido && c.Estado == "Disponible");

            var lista = await query
                .OrderBy(c => c.Id)
                .Select(c => new CodigoDigitalResponseDto
                {
                    Id = c.Id,
                    IdProducto = c.IdProducto,
                    IdVariacion = c.IdVariacion,
                    Clave = c.Clave,
                    Vendido = c.Vendido,
                    Estado = c.Estado,
                    FechaVenta = c.FechaVenta,
                    IdVenta = c.IdVenta
                })
                .ToListAsync();

            return Ok(lista);
        }

        // POST: api/CodigosDigitales/masivo
        [HttpPost("masivo")]
        public async Task<IActionResult> CargaMasivaCodigos([FromBody] RegistrarCodigosDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var producto = await _context.Productos.FindAsync(dto.IdProducto);
            if (producto == null) return NotFound(new { mensaje = "El producto no existe." });

            var nuevosCodigos = dto.Codigos
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim())
                .Distinct()
                .Select(clave => new CodigoDigital
                {
                    IdProducto = dto.IdProducto,
                    IdVariacion = dto.IdVariacion,
                    Clave = clave,
                    Vendido = false,
                    Estado = "Disponible"
                })
                .ToList();

            if (!nuevosCodigos.Any())
                return BadRequest(new { mensaje = "No se recibieron códigos válidos." });

            _context.CodigosDigitales.AddRange(nuevosCodigos);
            await _context.SaveChangesAsync();

            return Ok(new { mensaje = $"Se registraron {nuevosCodigos.Count} códigos exitosamente." });
        }
    }
}