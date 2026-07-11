namespace NicaplusApi.DTOs.Clientes
{
    public class ClientePerfilDto
    {
        public int Id { get; set; }

        public string Nombre { get; set; } = string.Empty;

        public string Telefono { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public int PuntosAcumulados { get; set; }

        public DateTime FechaRegistro { get; set; }
    }
}