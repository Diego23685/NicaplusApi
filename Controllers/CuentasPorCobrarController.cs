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
    [Authorize] // Protegemos el módulo financiero de créditos
    public class CuentasPorCobrarController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CuentasPorCobrarController> _logger;

        public CuentasPorCobrarController(
            ApplicationDbContext context,
            ILogger<CuentasPorCobrarController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private static TimeZoneInfo GetNicaraguaTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Central America Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/Managua");
            }
        }

        private DateTime GetNicaraguaTime()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, GetNicaraguaTimeZone());
        }

        // GET: api/CuentasPorCobrar
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string estado = "Todos")
        {
            try
            {
                var ahoraNicaragua = GetNicaraguaTime();

                var query = _context.CuentasPorCobrar
                    .AsNoTracking()
                    .Include(c => c.Cliente)
                    .AsQueryable();

                if (!string.Equals(estado, "Todos", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(c => c.Estado == estado);
                }

                var resultados = await query
                    .OrderByDescending(c => c.FechaVencimiento)
                    .Select(c => new CuentaPorCobrarResponseDto
                    {
                        Id = c.Id,
                        IdCliente = c.IdCliente,
                        NombreCliente = c.Cliente != null ? c.Cliente.Nombre : "Cliente Desconocido",
                        TelefonoCliente = c.Cliente != null ? c.Cliente.Telefono : string.Empty,
                        IdVenta = c.IdVenta,
                        MontoTotal = c.MontoTotal,
                        SaldoPendiente = c.SaldoPendiente,
                        FechaEmision = c.FechaEmision,
                        FechaVencimiento = c.FechaVencimiento,
                        Estado = c.Estado,
                        EsVencida = c.FechaVencimiento < ahoraNicaragua && c.SaldoPendiente > 0
                    })
                    .ToListAsync();

                return Ok(resultados);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar las cuentas por cobrar.");
                return StatusCode(500, new { mensaje = "Error interno al obtener el reporte de cuentas por cobrar." });
            }
        }

        // POST: api/CuentasPorCobrar
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CrearCuentaPorCobrarDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { mensaje = "Datos de cuenta por cobrar inválidos.", detalles = ModelState });
            }

            try
            {
                var cliente = await _context.Clientes.FindAsync(dto.IdCliente);
                if (cliente == null)
                {
                    return BadRequest(new { mensaje = "El cliente especificado no existe." });
                }

                DateTime fechaEmisionFinal = dto.FechaEmision.HasValue && dto.FechaEmision.Value != default
                    ? dto.FechaEmision.Value
                    : GetNicaraguaTime();

                var cxC = new CuentaPorCobrar
                {
                    IdCliente = dto.IdCliente,
                    IdVenta = dto.IdVenta,
                    MontoTotal = dto.MontoTotal,
                    SaldoPendiente = dto.MontoTotal,
                    FechaEmision = fechaEmisionFinal,
                    FechaVencimiento = dto.FechaVencimiento,
                    Estado = "Pendiente"
                };

                _context.CuentasPorCobrar.Add(cxC);
                await _context.SaveChangesAsync();

                var response = new CuentaPorCobrarResponseDto
                {
                    Id = cxC.Id,
                    IdCliente = cxC.IdCliente,
                    NombreCliente = cliente.Nombre,
                    TelefonoCliente = cliente.Telefono,
                    IdVenta = cxC.IdVenta,
                    MontoTotal = cxC.MontoTotal,
                    SaldoPendiente = cxC.SaldoPendiente,
                    FechaEmision = cxC.FechaEmision,
                    FechaVencimiento = cxC.FechaVencimiento,
                    Estado = cxC.Estado,
                    EsVencida = cxC.FechaVencimiento < GetNicaraguaTime() && cxC.SaldoPendiente > 0
                };

                return Ok(new { mensaje = "Cuenta por cobrar registrada con éxito.", cuenta = response });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear registro de cuenta por cobrar manual.");
                return StatusCode(500, new { mensaje = "Error interno al guardar la cuenta por cobrar." });
            }
        }

        // PUT: api/CuentasPorCobrar/5/abonar
        [HttpPut("{id}/abonar")]
        public async Task<IActionResult> Abonar(int id, [FromBody] RegistrarAbonoDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { mensaje = "Datos del abono inválidos.", detalles = ModelState });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var cuenta = await _context.CuentasPorCobrar
                    .Include(c => c.Cliente)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (cuenta == null)
                {
                    return NotFound(new { mensaje = "Cuenta por cobrar no encontrada." });
                }

                if (cuenta.Estado == "Pagado" || cuenta.SaldoPendiente <= 0)
                {
                    return BadRequest(new { mensaje = "Esta cuenta ya se encuentra totalmente liquidada." });
                }

                if (dto.MontoAbono > cuenta.SaldoPendiente)
                {
                    return BadRequest(new { mensaje = $"El abono (C${dto.MontoAbono:F2}) no puede ser mayor al saldo pendiente (C${cuenta.SaldoPendiente:F2})." });
                }

                var ahoraNicaragua = GetNicaraguaTime();

                // 1. Actualización de saldos de la deuda
                cuenta.SaldoPendiente -= dto.MontoAbono;
                if (cuenta.SaldoPendiente <= 0)
                {
                    cuenta.SaldoPendiente = 0;
                    cuenta.Estado = "Pagado";
                }

                // 2. Registro del abono en el módulo de Caja
                var nombreCliente = cuenta.Cliente?.Nombre ?? "Cliente General";
                var movimientoCaja = new MovimientoCaja
                {
                    Fecha = ahoraNicaragua,
                    Tipo = "Ingreso",
                    Monto = dto.MontoAbono,
                    Concepto = "Abono CxC",
                    Detalle = $"Abono a CxC ID: {cuenta.Id} | Cliente: {nombreCliente} | Método: {dto.MetodoPago}"
                };

                _context.MovimientosCaja.Add(movimientoCaja);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var response = new CuentaPorCobrarResponseDto
                {
                    Id = cuenta.Id,
                    IdCliente = cuenta.IdCliente,
                    NombreCliente = nombreCliente,
                    TelefonoCliente = cuenta.Cliente?.Telefono ?? string.Empty,
                    IdVenta = cuenta.IdVenta,
                    MontoTotal = cuenta.MontoTotal,
                    SaldoPendiente = cuenta.SaldoPendiente,
                    FechaEmision = cuenta.FechaEmision,
                    FechaVencimiento = cuenta.FechaVencimiento,
                    Estado = cuenta.Estado,
                    EsVencida = cuenta.FechaVencimiento < ahoraNicaragua && cuenta.SaldoPendiente > 0
                };

                return Ok(new
                {
                    mensaje = "Abono registrado correctamente.",
                    cuenta = response
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error crítico al procesar el abono a la cuenta por cobrar ID {Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al procesar el abono financiero." });
            }
        }
    }
}