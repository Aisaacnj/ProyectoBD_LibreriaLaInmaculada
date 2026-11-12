namespace LibreriaLaInmaculada.Backend.Modelos;

/// <summary>Una fila de dbo.CAT_Proveedor.</summary>
public class Proveedor
{
    public int IdProveedor { get; set; }
    public string Nombre { get; set; } = "";
    public string? ContactoNombre { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Direccion { get; set; }
    public bool Activo { get; set; }
    public DateTime FechaRegistro { get; set; }
    public DateTime FechaActualizacion { get; set; }
    public int TotalCompras { get; set; }      // compras activas registradas a este proveedor
}
