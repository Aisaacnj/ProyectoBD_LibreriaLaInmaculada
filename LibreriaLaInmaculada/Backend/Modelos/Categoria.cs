namespace LibreriaLaInmaculada.Backend.Modelos;

/// <summary>Una fila de dbo.CAT_Categoria.</summary>
public class Categoria
{
    public int IdCategoria { get; set; }
    public string Nombre { get; set; } = "";
    public bool Activo { get; set; }
    public DateTime FechaRegistro { get; set; }
    public DateTime FechaActualizacion { get; set; }
    public int TotalArticulos { get; set; }    // artículos activos en esta categoría
}
