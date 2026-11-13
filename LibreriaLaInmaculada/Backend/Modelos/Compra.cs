namespace LibreriaLaInmaculada.Backend.Modelos;

/// <summary>Una compra a un proveedor (tabla dbo.Compra, leída desde la vista V_Compra).</summary>
public class Compra
{
    public int IdCompra { get; set; }
    public string NumFactura { get; set; } = "";      // número impreso en la factura del proveedor
    public int IdProveedor { get; set; }
    public string Proveedor { get; set; } = "";
    public DateTime FechaCompra { get; set; }
    public decimal SubTotal { get; set; }             // sin IVA
    public decimal IVA { get; set; }
    public decimal Total { get; set; }                // SubTotal + IVA
    public string Estado { get; set; } = "Activa";    // "Activa" o "Anulada"
    public string RegistradoPor { get; set; } = "";
    public DateTime FechaRegistro { get; set; }

    // Solo para la lista
    public int TotalLineas { get; set; }
    public int TotalUnidades { get; set; }

    // Solo para el detalle y al registrar
    public List<CompraDetalle> Lineas { get; set; } = new();

    public bool Anulada => Estado == "Anulada";
}

/// <summary>Una línea de la compra (tabla dbo.Compra_Detalle).</summary>
public class CompraDetalle
{
    public int IdArticulo { get; set; }
    public string Articulo { get; set; } = "";
    public string Categoria { get; set; } = "";
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }       // costo por unidad, sin IVA
    public decimal PorcentajeIVA { get; set; }        // 15 normal, 0 si viene exento
    public decimal SubTotal { get; set; }
    public decimal IVA { get; set; }
    public decimal Total { get; set; }

    /// <summary>
    /// Calcula los importes igual que las columnas calculadas de la base:
    /// IVA redondeado a 2 decimales, y Total = SubTotal + IVA.
    /// </summary>
    public void CalcularImportes()
    {
        SubTotal = Math.Round(Cantidad * PrecioUnitario, 2, MidpointRounding.AwayFromZero);
        IVA = Math.Round(Cantidad * PrecioUnitario * PorcentajeIVA / 100m, 2, MidpointRounding.AwayFromZero);
        Total = SubTotal + IVA;
    }
}

/// <summary>Filtros de la lista de compras.</summary>
public class FiltroCompras
{
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
    public int? IdProveedor { get; set; }
    public string Estado { get; set; } = "Activa";    // "Activa", "Anulada" o "Todas"
}
