using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.Models;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdenesServicioController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public OrdenesServicioController(ApplicationDbContext context)
        {
            _context = context;
        }

        // 1. Obtener todas las órdenes de servicio activas
        [HttpGet]
        public async Task<ActionResult<IEnumerable<OrdenServicio>>> GetOrdenes()
        {
            return await _context.OrdenesServicio
                .Include(o => o.Cliente)
                .Include(o => o.Tecnico)
                .OrderByDescending(o => o.FechaIngreso)
                .ToListAsync();
        }

        // 2. Crear una nueva orden de soporte técnico
        [HttpPost]
        public async Task<ActionResult<OrdenServicio>> CrearOrden([FromBody] OrdenServicio orden)
        {
            orden.FechaIngreso = DateTime.UtcNow;
            orden.Estado = "Recibido";

            _context.OrdenesServicio.Add(orden);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetOrdenes), new { id = orden.Id }, orden);
        }

        // 3. Actualizar el estado o diagnóstico de una orden
        [HttpPut("{id}/estado")]
        public async Task<IActionResult> ActualizarEstado(int id, [FromQuery] string nuevoEstado, [FromBody] string? notas)
        {
            var orden = await _context.OrdenesServicio.FindAsync(id);
            if (orden == null)
            {
                return NotFound("La orden de servicio no existe.");
            }

            var estadosValidos = new[] { "Recibido", "En Revisión", "Listo", "Entregado" };
            if (!estadosValidos.Contains(nuevoEstado))
            {
                return BadRequest("Estado de servicio no válido.");
            }

            orden.Estado = nuevoEstado;
            
            // CORREGIDO: Cambiado de 'notes' a 'notas' para que coincida con el parámetro
            if (notas != null)
            {
                orden.Notas = notas;
            }

            if (nuevoEstado == "Entregado")
            {
                orden.FechaEntrega = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        // 4. Liquidar y procesar cobro automático en Ventas
        [HttpPut("{id}/entregar")]
        public async Task<IActionResult> EntregarEquipo(int id, [FromBody] EntregaOrdenDto dto)
        {
            var orden = await _context.OrdenesServicio.Include(o => o.Cliente).FirstOrDefaultAsync(o => o.Id == id);
            if (orden == null) return NotFound();

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                orden.Estado = "Entregado";
                orden.Notas = $"[ENTREGA] Herramientas: {dto.HerramientasUsed}. Diagnóstico: {dto.DiagnosticoFinal}. {orden.Notas}";

                // CORREGIDO: Mapeado estrictamente a 'DetalleVenta' respetando tu modelo
                var ventaServicio = new Venta
                {
                    IdUsuario = 1, 
                    IdCliente = orden.IdCliente,
                    MetodoPago = "Efectivo",
                    FechaVenta = DateTime.Now, 
                    Total = dto.CostoReparacion,
                    Detalles = new List<DetalleVenta>
                    {
                        new DetalleVenta
                        {
                            IdProducto = 1, // Semilla en tu DB para servicios generales de taller
                            Cantidad = 1,
                            PrecioUnitario = dto.CostoReparacion,
                            SubTotal = dto.CostoReparacion,
                            MetadataDigital = $"Equipo: {orden.Dispositivo}"
                        }
                    }
                };

                _context.Ventas.Add(ventaServicio);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { ventaId = ventaServicio.Id });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, "Error en cascada al liquidar orden e inyectar cobro.");
            }
        }
        
        public class EntregaOrdenDto
        {
            public required string DiagnosticoFinal { get; set; }
            public required string HerramientasUsed { get; set; } // CORREGIDO: Añadido required para quitar advertencia CS8618
            public decimal CostoReparacion { get; set; }
        }
    }
}