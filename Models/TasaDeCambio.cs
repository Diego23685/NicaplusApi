using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.Models
{
    public class TasaDeCambio
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Range(0.01, 10000.00, ErrorMessage = "El valor de la tasa debe ser mayor a cero.")]
        public decimal Valor { get; set; }
    }
}