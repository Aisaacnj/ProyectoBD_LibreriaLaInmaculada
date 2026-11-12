namespace LibreriaLaInmaculada.Backend.Modelos;

/// <summary>Una fila de dbo.CAT_Articulos (leída desde la vista V_Articulos, que trae el nombre de la categoría).</summary>
public class Articulo
{
    public int IdArticulo { get; set; }
    public int IdCategoria { get; set; }
    public string Categoria { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public int Cantidad { get; set; }          // stock: solo cambia con compras, ventas y pérdidas
    public int StockMinimo { get; set; }
    public decimal PrecioCompra { get; set; }  // costo de la última compra
    public decimal PrecioVenta { get; set; }
    public bool Activo { get; set; }
    public DateTime FechaRegistro { get; set; }
    public DateTime FechaActualizacion { get; set; }

    public decimal Margen => PrecioVenta - PrecioCompra;
    public bool Agotado => Cantidad == 0;
    public bool BajoStock => Cantidad <= StockMinimo;
}

/// <summary>Filtros de la pantalla de Inventario.</summary>
public class FiltroInventario
{
    public string? Buscar { get; set; }
    public int? IdCategoria { get; set; }
    public string? Estado { get; set; }        // null, "bajo", "agotado" o "inactivos"
    public bool IncluirInactivos { get; set; }
}
