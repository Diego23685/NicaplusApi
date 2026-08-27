using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.Models;

[Route("api/tasa-cambio")]
[ApiController]
public class TasaDeCambioController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public TasaDeCambioController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<TasaDeCambio>> ObtenerTasa()
    {
        var tasa = await _context.TasasDeCambio.FirstOrDefaultAsync();
        if (tasa == null)
        {
            // Tasa por defecto si la tabla está vacía
            tasa = new TasaDeCambio { Id = 1, Valor = 36.6243m }; 
            _context.TasasDeCambio.Add(tasa);
            await _context.SaveChangesAsync();
        }
        return Ok(tasa);
    }

    [HttpPut]
    public async Task<IActionResult> ActualizarTasa([FromBody] TasaDeCambio tasaDto)
    {
        var tasa = await _context.TasasDeCambio.FirstOrDefaultAsync();
        if (tasa == null)
        {
            _context.TasasDeCambio.Add(tasaDto);
        }
        else
        {
            tasa.Valor = tasaDto.Valor;
            _context.TasasDeCambio.Update(tasa);
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }
}