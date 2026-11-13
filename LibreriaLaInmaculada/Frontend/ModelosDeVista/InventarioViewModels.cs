using System.ComponentModel.DataAnnotations;
using LibreriaLaInmaculada.Backend.Modelos;

namespace LibreriaLaInmaculada.Frontend.ModelosDeVista;

/// <summary>Pantalla Inventario: lista, filtros y totales.</summary>
public class InventarioIndexViewModel
{
    public List<Articulo> Articulos { get; set; } = new();
    public List<Categoria> Categorias { get; set; } = new();

    // Filtros aplicados
    public string? Buscar { get; set; }
    public int? IdCategoria { get; set; }
    public string? Estado { get; set; }

    // Totales (solo artículos activos)
    public int TotalActivos { get; set; }
    public int BajoStock { get; set; }
    public int Agotados { get; set; }
    public decimal ValorInventario { get; set; }
}

/// <summary>Formulario para crear o editar un artículo. IdArticulo = 0 significa "nuevo".</summary>
public class ArticuloFormViewModel
{
    public int IdArticulo { get; set; }

    [Required(ErrorMessage = "Escriba la descripción del artículo.")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "La descripción debe tener entre 3 y 150 caracteres.")]
    [Display(Name = "Descripción")]
    public string? Descripcion { get; set; }

    [Required(ErrorMessage = "Elija una categoría.")]
    [Display(Name = "Categoría")]
    public int? IdCategoria { get; set; }

    [Required(ErrorMessage = "Escriba el stock mínimo.")]
    [Range(0, 10000, ErrorMessage = "El stock mínimo debe estar entre 0 y 10,000.")]
    [Display(Name = "Stock mínimo")]
    public int? StockMinimo { get; set; } = 5;

    [Required(ErrorMessage = "Escriba el costo.")]
    [Range(typeof(decimal), "0.01", "100000", ErrorMessage = "El costo debe ser mayor que C$ 0 y hasta C$ 100,000.")]
    [Display(Name = "Costo por unidad (C$)")]
    public decimal? PrecioCompra { get; set; }

    [Required(ErrorMessage = "Escriba el precio de venta.")]
    [Range(typeof(decimal), "0.01", "100000", ErrorMessage = "El precio debe ser mayor que C$ 0 y hasta C$ 100,000.")]
    [Display(Name = "Precio de venta por unidad (C$)")]
    public decimal? PrecioVenta { get; set; }

    // Solo para mostrar
    public int Cantidad { get; set; }
    public bool Activo { get; set; } = true;
    public List<Categoria> Categorias { get; set; } = new();

    public bool EsNuevo => IdArticulo == 0;
}

/// <summary>Pantalla de categorías: lista y formulario para agregar.</summary>
public class CategoriasViewModel
{
    public List<Categoria> Categorias { get; set; } = new();
}

public class CategoriaFormViewModel
{
    public int IdCategoria { get; set; }

    [Required(ErrorMessage = "Escriba el nombre de la categoría.")]
    [StringLength(80, MinimumLength = 3, ErrorMessage = "El nombre debe tener entre 3 y 80 caracteres.")]
    [Display(Name = "Nombre")]
    public string? Nombre { get; set; }
}
