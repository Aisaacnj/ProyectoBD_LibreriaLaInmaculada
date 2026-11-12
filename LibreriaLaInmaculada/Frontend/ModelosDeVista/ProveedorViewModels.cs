using System.ComponentModel.DataAnnotations;
using LibreriaLaInmaculada.Backend.Modelos;

namespace LibreriaLaInmaculada.Frontend.ModelosDeVista;

public class ProveedoresIndexViewModel
{
    public List<Proveedor> Proveedores { get; set; } = new();
    public string? Buscar { get; set; }
    public bool MostrarInactivos { get; set; }
}

/// <summary>Formulario para crear o editar un proveedor. IdProveedor = 0 significa "nuevo".</summary>
public class ProveedorFormViewModel
{
    public int IdProveedor { get; set; }

    [Required(ErrorMessage = "Escriba el nombre del proveedor.")]
    [StringLength(100, ErrorMessage = "El nombre puede tener hasta 100 caracteres.")]
    [Display(Name = "Nombre o sucursal")]
    public string? Nombre { get; set; }

    [StringLength(100, ErrorMessage = "Puede tener hasta 100 caracteres.")]
    [Display(Name = "Persona de contacto")]
    public string? ContactoNombre { get; set; }

    [StringLength(20, ErrorMessage = "Puede tener hasta 20 caracteres.")]
    [Display(Name = "Teléfono")]
    public string? Telefono { get; set; }

    [StringLength(100, ErrorMessage = "Puede tener hasta 100 caracteres.")]
    [EmailAddress(ErrorMessage = "Escriba un correo válido, por ejemplo: ventas@proveedor.com")]
    [Display(Name = "Correo")]
    public string? Email { get; set; }

    [StringLength(255, ErrorMessage = "Puede tener hasta 255 caracteres.")]
    [Display(Name = "Dirección")]
    public string? Direccion { get; set; }

    public bool Activo { get; set; } = true;
    public bool EsNuevo => IdProveedor == 0;
}
