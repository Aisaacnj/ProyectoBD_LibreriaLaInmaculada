using System.ComponentModel.DataAnnotations;

namespace LibreriaLaInmaculada.Frontend.ModelosDeVista;

public class UsuarioNuevoViewModel
{
    [Required(ErrorMessage = "Escriba el nombre de usuario.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Debe tener entre 3 y 50 caracteres.")]
    [RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Use solo letras, números, punto, guion o guion bajo, sin espacios ni tildes.")]
    [Display(Name = "Usuario (para iniciar sesión)")]
    public string? Usuario { get; set; }

    [Required(ErrorMessage = "Escriba el nombre completo.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Debe tener entre 3 y 100 caracteres.")]
    [RegularExpression(Validaciones.NombrePersona, ErrorMessage = "Use solo letras y espacios.")]
    [Display(Name = "Nombre completo")]
    public string? NombreCompleto { get; set; }

    [Required(ErrorMessage = "Elija un rol.")]
    [Display(Name = "Rol")]
    public string? Rol { get; set; }

    [Required(ErrorMessage = "Escriba una contraseña.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    [RegularExpression(Validaciones.Contrasena, ErrorMessage = "La contraseña debe tener al menos una letra y un número.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string? Password { get; set; }

    [Required(ErrorMessage = "Repita la contraseña.")]
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    [DataType(DataType.Password)]
    [Display(Name = "Repetir contraseña")]
    public string? ConfirmarPassword { get; set; }
}

public class UsuarioEditarViewModel
{
    public int IdUsuario { get; set; }
    public string? Usuario { get; set; }           // solo se muestra, no se cambia

    [Required(ErrorMessage = "Escriba el nombre completo.")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Debe tener entre 3 y 100 caracteres.")]
    [RegularExpression(Validaciones.NombrePersona, ErrorMessage = "Use solo letras y espacios.")]
    [Display(Name = "Nombre completo")]
    public string? NombreCompleto { get; set; }

    [Required(ErrorMessage = "Elija un rol.")]
    [Display(Name = "Rol")]
    public string? Rol { get; set; }

    public bool EsUsuarioActual { get; set; }
}

public class UsuarioPasswordViewModel
{
    public int IdUsuario { get; set; }
    public string? Usuario { get; set; }           // solo se muestra

    [Required(ErrorMessage = "Escriba la nueva contraseña.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    [RegularExpression(Validaciones.Contrasena, ErrorMessage = "La contraseña debe tener al menos una letra y un número.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nueva contraseña")]
    public string? Password { get; set; }

    [Required(ErrorMessage = "Repita la contraseña.")]
    [Compare(nameof(Password), ErrorMessage = "Las contraseñas no coinciden.")]
    [DataType(DataType.Password)]
    [Display(Name = "Repetir contraseña")]
    public string? ConfirmarPassword { get; set; }
}
