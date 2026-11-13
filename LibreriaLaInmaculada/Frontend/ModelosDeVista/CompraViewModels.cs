using System.ComponentModel.DataAnnotations;
using LibreriaLaInmaculada.Backend.Modelos;

namespace LibreriaLaInmaculada.Frontend.ModelosDeVista;

/// <summary>Pantalla Compras: lista, filtros y totales del mes.</summary>
public class ComprasIndexViewModel
{
    public List<Compra> Compras { get; set; } = new();
    public List<Proveedor> Proveedores { get; set; } = new();

    // Filtros aplicados
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
    public int? IdProveedor { get; set; }
    public string Estado { get; set; } = "Activa";

    // Tarjetas: compras activas del mes actual
    public int ComprasMes { get; set; }
    public decimal TotalMes { get; set; }
    public decimal IvaMes { get; set; }
}

/// <summary>Formulario para registrar una compra con varias líneas.</summary>
public class CompraNuevaViewModel
{
    [Required(ErrorMessage = "Elija el proveedor.")]
    [Display(Name = "Proveedor")]
    public int? IdProveedor { get; set; }

    [Required(ErrorMessage = "Escriba el número de la factura del proveedor.")]
    [StringLength(30, MinimumLength = 3, ErrorMessage = "El número de factura debe tener entre 3 y 30 caracteres.")]
    [RegularExpression(Validaciones.NumFactura, ErrorMessage = "Use solo letras, números y guiones. Por ejemplo: GP-0029541.")]
    [Display(Name = "N.º de factura del proveedor")]
    public string? NumFactura { get; set; }

    [Required(ErrorMessage = "Escriba la fecha de la factura.")]
    [Display(Name = "Fecha de la factura")]
    public DateTime? FechaCompra { get; set; } = DateTime.Today;

    public List<LineaCompraViewModel> Lineas { get; set; } = new();

    // Para llenar las listas desplegables
    public List<Proveedor> Proveedores { get; set; } = new();
    public List<Articulo> Articulos { get; set; } = new();
    public List<Categoria> Categorias { get; set; } = new();   // para la ventanita "Artículo nuevo"
}

/// <summary>Una fila del formulario: qué artículo, cuántas unidades, a qué costo y con qué IVA.</summary>
public class LineaCompraViewModel
{
    public int? IdArticulo { get; set; }
    public int? Cantidad { get; set; }
    public decimal? PrecioUnitario { get; set; }    // costo por unidad SIN IVA
    public decimal? PorcentajeIVA { get; set; } = 15;

    /// <summary>¿La fila quedó completamente vacía? (se ignora al guardar)</summary>
    public bool EstaVacia => IdArticulo is null && Cantidad is null && PrecioUnitario is null;
}
