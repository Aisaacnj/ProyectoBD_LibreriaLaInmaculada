using System.ComponentModel.DataAnnotations;
using LibreriaLaInmaculada.Backend.Modelos;

namespace LibreriaLaInmaculada.Frontend.ModelosDeVista;

/// <summary>Pantalla Ventas: lista, filtros y totales.</summary>
public class VentasIndexViewModel
{
    public List<Venta> Ventas { get; set; } = new();
    public List<Usuario> Vendedores { get; set; } = new();   // solo para el filtro del administrador
    public bool EsAdmin { get; set; }

    // Filtros aplicados
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
    public int? IdVendedor { get; set; }
    public string Estado { get; set; } = "Activa";
    public int Limite { get; set; }

    // Totales de lo filtrado
    public int TotalEncontradas { get; set; }
    public decimal TotalVendido { get; set; }
    public decimal TotalGanancia { get; set; }

    // Tarjetas
    public int VentasHoy { get; set; }
    public decimal VendidoHoy { get; set; }
    public decimal VendidoMes { get; set; }
    public decimal GananciaMes { get; set; }
}

/// <summary>Pantalla Vender: el carrito y con cuánto paga el cliente.</summary>
public class VentaNuevaViewModel
{
    public List<LineaVentaViewModel> Lineas { get; set; } = new();

    [Range(typeof(decimal), "0", "100000", ErrorMessage = "El pago debe estar entre C$ 0 y C$ 100,000.")]
    [Display(Name = "Paga con (C$)")]
    public decimal? PagaCon { get; set; }

    // Para el buscador
    public List<Articulo> Articulos { get; set; } = new();
}

/// <summary>Una fila del carrito: qué artículo y cuántas unidades. El precio NO viaja desde el navegador.</summary>
public class LineaVentaViewModel
{
    public int? IdArticulo { get; set; }
    public int? Cantidad { get; set; }
}

/// <summary>Detalle de una venta (comprobante).</summary>
public class VentaDetalleViewModel
{
    public Venta Venta { get; set; } = new();
    public bool EsAdmin { get; set; }
    public decimal? PagaCon { get; set; }        // solo justo después de cobrar
    public decimal? Vuelto => PagaCon is null ? null : PagaCon - Venta.Total;
}
