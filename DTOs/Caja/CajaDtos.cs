using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.DTOs.Caja
{
    public class CrearMovimientoCajaDto
    {
        [Required(ErrorMessage = "El tipo de movimiento es requerido.")]
        public string Tipo { get; set; } = null!;

        [Required(ErrorMessage = "El concepto es requerido.")]
        public string Concepto { get; set; } = null!;

        [Range(0.01, double.MaxValue, ErrorMessage = "El monto debe ser mayor a 0.")]
        public decimal Monto { get; set; }

        public string? Detalle { get; set; }

        public DateTime? Fecha { get; set; }
    }

    public class MovimientoCajaResponseDto
    {
        public int Id { get; set; }
        public string Tipo { get; set; } = null!;
        public string Concepto { get; set; } = null!;
        public decimal Monto { get; set; }
        public string? Detalle { get; set; }
        public DateTime Fecha { get; set; }
        public bool EsAutomatico { get; set; } // 👈 Informa al frontend si proviene de Venta/Compra
    }
}