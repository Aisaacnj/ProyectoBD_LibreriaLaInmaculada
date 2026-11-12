using System.ComponentModel.DataAnnotations;

namespace LibreriaLaInmaculada.Frontend.ModelosDeVista;

public class LoginViewModel
{
    [Required(ErrorMessage = "Escriba su usuario.")]
    [StringLength(50)]
    public string Usuario { get; set; } = "";

    [Required(ErrorMessage = "Escriba su contraseña.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    /// <summary>Página a la que se quería entrar antes de pedir el login.</summary>
    public string? ReturnUrl { get; set; }
}
