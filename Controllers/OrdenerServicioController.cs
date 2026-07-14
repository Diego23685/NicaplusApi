using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NicaplusApi.Data;
using NicaplusApi.Models;
using NicaplusApi.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NicaplusApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdenesServicioController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWhatsAppService _whatsappService;

        // Zona horaria estándar para Nicaragua
        private static readonly TimeZoneInfo NicaraguaZone = TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");

        public OrdenesServicioController(ApplicationDbContext context, IWhatsAppService whatsappService)
        {
            _context = context;
            _whatsappService = whatsappService;
        }

        private DateTime GetNicaraguaTime()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, NicaraguaZone);
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
            // Corregido: Hora local de Nicaragua para el ingreso al taller
            orden.FechaIngreso = GetNicaraguaTime();
            orden.Estado = "Recibido";

            _context.OrdenesServicio.Add(orden);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetOrdenes), new { id = orden.Id }, orden);
        }

        [HttpPut("{id}/estado")]
        public async Task<IActionResult> ActualizarEstado(int id, [FromQuery] string nuevoEstado, [FromBody] string? notas)
        {
            var orden = await _context.OrdenesServicio.Include(o => o.Cliente).FirstOrDefaultAsync(o => o.Id == id);
            if (orden == null) return NotFound("La orden de servicio no existe.");

            var estadosValidos = new[] { "Recibido", "En Revisión", "Listo", "Entregado" };
            if (!estadosValidos.Contains(nuevoEstado)) return BadRequest("Estado de servicio no válido.");

            string estadoAnterior = orden.Estado;
            orden.Estado = nuevoEstado;
            
            // CORREGIDO: Uso de variable correcta 'notas' y prevención de asignación nula
            if (!string.IsNullOrWhiteSpace(notas)) 
            {
                orden.Notas = notas;
            }

            if (nuevoEstado == "Entregado") 
            {
                orden.FechaEntrega = GetNicaraguaTime();
            }

            await _context.SaveChangesAsync();

            if (nuevoEstado == "Listo" && estadoAnterior != "Listo" && orden.Cliente != null)
            {
                var variables = new Dictionary<string, string>
                {
                    { "cliente", orden.Cliente.Nombre },
                    { "dispositivo", orden.Dispositivo },
                    { "id", orden.Id.ToString() }
                };
                
                await _whatsappService.EnviarDesdePlantillaAsync("EnvioComprobante", orden.Cliente.Telefono, variables);
            }

            return NoContent();
        }

        [HttpPut("{id}/entregar")]
        public async Task<IActionResult> EntregarEquipo(int id, [FromBody] EntregaOrdenDto dto)
        {
            var orden = await _context.OrdenesServicio.Include(o => o.Cliente).FirstOrDefaultAsync(o => o.Id == id);
            if (orden == null) return NotFound("La orden de servicio no existe.");

            // 1. Extraer el ID del usuario logueado desde los Claims del Token JWT
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            
            // Fallback: Si por alguna razón no viene en el token, usamos un ID administrador válido por defecto (ej. 3) o arrojamos Unauthorized
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized("No se pudo identificar al usuario que procesa la entrega.");
            }
            
            int idUsuarioLogueado = int.Parse(userIdClaim);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var ahoraNicaragua = GetNicaraguaTime();

                orden.Estado = "Entregado";
                orden.FechaEntrega = ahoraNicaragua;
                orden.Notas = $"[ENTREGA] Herramientas: {dto.HerramientasUsed}. Diagnóstico: {dto.DiagnosticoFinal}. {orden.Notas}";

                var productoServicio = await _context.Productos
                    .FirstOrDefaultAsync(p => p.RequiereServicio || p.Id == dto.IdProductoServicio);

                if (productoServicio == null)
                {
                    return BadRequest("No se encontró un producto de tipo 'Servicio' en el catálogo para el ingreso contable.");
                }

                var ventaServicio = new Venta
                {
                    IdUsuario = idUsuarioLogueado, // ◄ CORREGIDO: Registra la venta al usuario real que está operando
                    IdCliente = orden.IdCliente,
                    MetodoPago = dto.MetodoPago,
                    FechaVenta = ahoraNicaragua, 
                    Total = dto.CostoReparacion,
                    Detalles = new List<DetalleVenta>
                    {
                        new DetalleVenta
                        {
                            IdProducto = productoServicio.Id, 
                            Cantidad = 1,
                            PrecioUnitario = dto.CostoReparacion,
                            SubTotal = dto.CostoReparacion,
                            MetadataDigital = $"Taller - Equipo: {orden.Dispositivo}"
                        }
                    }
                };

                _context.Ventas.Add(ventaServicio);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                if (orden.Cliente != null)
                {
                    var variables = new Dictionary<string, string>
                    {
                        { "cliente", orden.Cliente.Nombre },
                        { "factura", $"#000{ventaServicio.Id}" },
                        { "total", $"C$ {dto.CostoReparacion}" },
                        { "dispositivo", orden.Dispositivo }
                    };

                    await _whatsappService.EnviarDesdePlantillaAsync("TallerListo", orden.Cliente.Telefono, variables);
                }

                return Ok(new { ventaId = ventaServicio.Id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Error en cascada al liquidar orden: {ex.Message}");
            }
        }
        
        public class EntregaOrdenDto
        {
            public required string DiagnosticoFinal { get; set; }
            public required string HerramientasUsed { get; set; } 
            public decimal CostoReparacion { get; set; }
            public string MetodoPago { get; set; } = "Efectivo"; // ◄ NUEVO
            public int IdProductoServicio { get; set; } = 1;      // ◄ NUEVO: Dinámico desde el Front
        }
    }
}