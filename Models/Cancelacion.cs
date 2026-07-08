using System.ComponentModel.DataAnnotations;

namespace NicaplusApi.Models
{
    public class Cancelacion
    {
        public int Id { get; set; }


        public int IdSuscripcion { get; set; }

        public int IdCliente { get; set; }


        [Required]
        public string Motivo { get; set; } = "";


        public DateTime FechaCancelacion { get; set; } = DateTime.UtcNow;



        public Suscripcion? Suscripcion { get; set; } = null!;

        public Cliente? Cliente { get; set; } = null!;
    }
}