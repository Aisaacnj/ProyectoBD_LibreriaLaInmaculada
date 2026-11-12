namespace LibreriaLaInmaculada.Backend.Modelos;

/// <summary>Una fila de la tabla dbo.Usuarios.</summary>
public class Usuario
{
    public int IdUsuario { get; set; }
    public string NombreUsuario { get; set; } = "";
    public string? NombreCompleto { get; set; }
    public string Password { get; set; } = "";   // hash PBKDF2, nunca la contraseña
    public string Rol { get; set; } = "";
    public bool Activo { get; set; }
    public DateTime FechaRegistro { get; set; }
}
