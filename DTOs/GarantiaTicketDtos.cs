using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.DTOs
{
    public class CrearGarantiaTicketDto
    {
        [Required(ErrorMessage = "El cliente es obligatorio.")]
        public int IdCliente { get; set; }

        [Required(ErrorMessage = "El usuario responsable es obligatorio.")]
        public int IdUsuarioResponsable { get; set; }

        public int? IdProducto { get; set; }

        [Required(ErrorMessage = "El motivo de la garantía es obligatorio.")]
        public string Motivo { get; set; } = string.Empty;

        [Required(ErrorMessage = "Debe especificar la cuenta o credencial anterior.")]
        [StringLength(200)]
        public string CuentaAnterior { get; set; } = string.Empty;

        [Required(ErrorMessage = "Debe especificar la nueva cuenta o credencial asignada.")]
        [StringLength(200)]
        public string CuentaNueva { get; set; } = string.Empty;

        [Range(0, double.MaxValue, ErrorMessage = "El costo de reposición no puede ser negativo.")]
        public decimal CostoReposicion { get; set; }

        public DateTime? FechaRepo { get; set; }

        public string Estado { get; set; } = "Pendiente";
    }

    public class GarantiaTicketResponseDto
    {
        public int Id { get; set; }
        public int IdCliente { get; set; }
        public string ClienteNombre { get; set; } = null!;
        public string ClienteTelefono { get; set; } = string.Empty;
        public int IdUsuarioResponsable { get; set; }
        public string ResponsableNombre { get; set; } = null!;
        public int? IdProducto { get; set; }
        public string Motivo { get; set; } = null!;
        public string CuentaAnterior { get; set; } = null!;
        public string CuentaNueva { get; set; } = null!;
        public decimal CostoReposicion { get; set; }
        public DateTime FechaRepo { get; set; }
        public string Estado { get; set; } = null!;
    }
}