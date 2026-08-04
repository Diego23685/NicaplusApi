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
    [Authorize] // Protegemos el módulo de egresos y deudas a proveedores
    public class CuentasPorPagarController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CuentasPorPagarController> _logger;

        public CuentasPorPagarController(
            ApplicationDbContext context,
            ILogger<CuentasPorPagarController> logger)
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

        // GET: api/CuentasPorPagar
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] string estado = "Todos")
        {
            try
            {
                var ahoraNicaragua = GetNicaraguaTime();

                var query = _context.CuentasPorPagar
                    .AsNoTracking()
                    .Include(p => p.Proveedor)
                    .AsQueryable();

                if (!string.Equals(estado, "Todos", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(c => c.Estado == estado);
                }

                var resultados = await query
                    .OrderByDescending(c => c.FechaVencimiento)
                    .Select(c => new CuentaPorPagarResponseDto
                    {
                        Id = c.Id,
                        IdProveedor = c.IdProveedor,
                        RazonSocialProveedor = c.Proveedor != null ? c.Proveedor.RazonSocial : "Proveedor Desconocido",
                        RucProveedor = c.Proveedor != null ? c.Proveedor.Ruc : string.Empty,
                        NumeroFactura = c.NumeroFactura,
                        MontoTotal = c.MontoTotal,
                        SaldoPendiente = c.SaldoPendiente,
                        FechaRegistro = c.FechaRegistro,
                        FechaVencimiento = c.FechaVencimiento,
                        Estado = c.Estado,
                        EsVencida = c.FechaVencimiento < ahoraNicaragua && c.SaldoPendiente > 0
                    })
                    .ToListAsync();

                return Ok(resultados);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al consultar las cuentas por pagar.");
                return StatusCode(500, new { mensaje = "Error interno al obtener el reporte de cuentas por pagar." });
            }
        }

        // POST: api/CuentasPorPagar
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CrearCuentaPorPagarDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { mensaje = "Datos de cuenta por pagar inválidos.", detalles = ModelState });
            }

            try
            {
                var proveedor = await _context.Proveedores.FindAsync(dto.IdProveedor);
                if (proveedor == null)
                {
                    return BadRequest(new { mensaje = "El proveedor especificado no existe." });
                }

                DateTime fechaRegistroFinal = dto.FechaRegistro.HasValue && dto.FechaRegistro.Value != default
                    ? dto.FechaRegistro.Value
                    : GetNicaraguaTime();

                var cxP = new CuentaPorPagar
                {
                    IdProveedor = dto.IdProveedor,
                    NumeroFactura = dto.NumeroFactura.Trim(),
                    MontoTotal = dto.MontoTotal,
                    SaldoPendiente = dto.MontoTotal,
                    FechaRegistro = fechaRegistroFinal,
                    FechaVencimiento = dto.FechaVencimiento,
                    Estado = "Pendiente"
                };

                _context.CuentasPorPagar.Add(cxP);
                await _context.SaveChangesAsync();

                var response = new CuentaPorPagarResponseDto
                {
                    Id = cxP.Id,
                    IdProveedor = cxP.IdProveedor,
                    RazonSocialProveedor = proveedor.RazonSocial,
                    RucProveedor = proveedor.Ruc,
                    NumeroFactura = cxP.NumeroFactura,
                    MontoTotal = cxP.MontoTotal,
                    SaldoPendiente = cxP.SaldoPendiente,
                    FechaRegistro = cxP.FechaRegistro,
                    FechaVencimiento = cxP.FechaVencimiento,
                    Estado = cxP.Estado,
                    EsVencida = cxP.FechaVencimiento < GetNicaraguaTime() && cxP.SaldoPendiente > 0
                };

                return Ok(new { mensaje = "Cuenta por pagar registrada con éxito.", cuenta = response });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear registro manual de cuenta por pagar.");
                return StatusCode(500, new { mensaje = "Error interno al registrar la deuda con el proveedor." });
            }
        }

        // PUT: api/CuentasPorPagar/5/abonar
        [HttpPut("{id}/abonar")]
        public async Task<IActionResult> Abonar(int id, [FromBody] RegistrarAbonoProveedorDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { mensaje = "Datos del abono inválidos.", detalles = ModelState });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var cuenta = await _context.CuentasPorPagar
                    .Include(p => p.Proveedor)
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (cuenta == null)
                {
                    return NotFound(new { mensaje = "Cuenta por pagar no encontrada." });
                }

                if (cuenta.Estado == "Pagado" || cuenta.SaldoPendiente <= 0)
                {
                    return BadRequest(new { mensaje = "Esta cuenta con el proveedor ya se encuentra totalmente liquidada." });
                }

                if (dto.MontoAbono > cuenta.SaldoPendiente)
                {
                    return BadRequest(new { mensaje = $"El abono (C${dto.MontoAbono:F2}) no puede exceder el saldo pendiente (C${cuenta.SaldoPendiente:F2})." });
                }

                var ahoraNicaragua = GetNicaraguaTime();

                // 1. Descontar saldo de la obligación
                cuenta.SaldoPendiente -= dto.MontoAbono;
                if (cuenta.SaldoPendiente <= 0)
                {
                    cuenta.SaldoPendiente = 0;
                    cuenta.Estado = "Pagado";
                }

                // 2. Registrar la salida real de efectivo/banco en Caja (Egreso)
                var proveedorNombre = cuenta.Proveedor?.RazonSocial ?? "Proveedor Genérico";
                var movimientoCaja = new MovimientoCaja
                {
                    Fecha = ahoraNicaragua,
                    Tipo = "Egreso",
                    Monto = dto.MontoAbono,
                    Concepto = "Gasto Ordinario",
                    Detalle = $"Abono a proveedor: {proveedorNombre} | Factura: {cuenta.NumeroFactura} | CxP ID: {cuenta.Id} | Método: {dto.MetodoPago}"
                };

                _context.MovimientosCaja.Add(movimientoCaja);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var response = new CuentaPorPagarResponseDto
                {
                    Id = cuenta.Id,
                    IdProveedor = cuenta.IdProveedor,
                    RazonSocialProveedor = proveedorNombre,
                    RucProveedor = cuenta.Proveedor?.Ruc ?? string.Empty,
                    NumeroFactura = cuenta.NumeroFactura,
                    MontoTotal = cuenta.MontoTotal,
                    SaldoPendiente = cuenta.SaldoPendiente,
                    FechaRegistro = cuenta.FechaRegistro,
                    FechaVencimiento = cuenta.FechaVencimiento,
                    Estado = cuenta.Estado,
                    EsVencida = cuenta.FechaVencimiento < ahoraNicaragua && cuenta.SaldoPendiente > 0
                };

                return Ok(new
                {
                    mensaje = "Abono a proveedor registrado correctamente.",
                    cuenta = response
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error al procesar el abono a la cuenta por pagar ID {Id}", id);
                return StatusCode(500, new { mensaje = "Error interno al procesar el egreso financiero." });
            }
        }
    }
}