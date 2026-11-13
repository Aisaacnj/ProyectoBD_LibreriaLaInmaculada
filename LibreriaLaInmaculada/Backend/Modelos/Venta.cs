namespace LibreriaLaInmaculada.Backend.Modelos;

/// <summary>Una venta (tabla dbo.Venta). Las ventas no llevan IVA.</summary>
public class Venta
{
    public int IdVenta { get; set; }
    public int IdUsuario { get; set; }
    public string Vendedor { get; set; } = "";            // usuario para iniciar sesión
    public string? VendedorNombre { get; set; }           // nombre completo
    public DateTime FechaVenta { get; set; }              // fecha y hora
    public decimal Total { get; set; }
    public string Estado { get; set; } = "Activa";        // "Activa" o "Anulada"

    // Solo para la lista
    public int TotalLineas { get; set; }
    public int TotalUnidades { get; set; }
    public decimal Ganancia { get; set; }

    // Solo para el detalle
    public List<VentaDetalle> Lineas { get; set; } = new();

    public bool Anulada => Estado == "Anulada";
}

/// <summary>Una línea de la venta (tabla dbo.Venta_Detalle).</summary>
public class VentaDetalle
{
    public int IdArticulo { get; set; }
    public string Articulo { get; set; } = "";
    public string Categoria { get; set; } = "";
    public int Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }   // precio cobrado en ese momento
    public decimal CostoUnitario { get; set; }    // costo del artículo en ese momento (para la ganancia)
    public decimal Total { get; set; }
    public decimal Ganancia { get; set; }
}

/// <summary>Lo que se manda a registrar: qué artículo y cuántas unidades. El precio lo pone la base.</summary>
public class LineaVenta
{
    public int IdArticulo { get; set; }
    public int Cantidad { get; set; }
}

/// <summary>Resultado de registrar una venta: el número nuevo, o los motivos por los que no se pudo.</summary>
public class ResultadoVenta
{
    public int IdVenta { get; set; }
    public decimal Total { get; set; }
    public List<string> Errores { get; } = new();
    public bool Exito => Errores.Count == 0;
}

/// <summary>Filtros de la lista de ventas.</summary>
public class FiltroVentas
{
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
    public int? IdUsuario { get; set; }               // el vendedor solo ve las suyas
    public string Estado { get; set; } = "Activa";    // "Activa", "Anulada" o "Todas"
    public int Limite { get; set; } = 200;            // cuántas se muestran como máximo
}
