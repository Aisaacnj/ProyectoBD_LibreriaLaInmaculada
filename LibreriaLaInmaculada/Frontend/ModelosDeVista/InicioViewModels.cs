namespace LibreriaLaInmaculada.Frontend.ModelosDeVista;

public class ArticuloAlerta
{
    public string Descripcion { get; set; } = "";
    public int Stock { get; set; }
    public int StockMinimo { get; set; }
    public bool Agotado => Stock == 0;
}

public class VentaMes
{
    public int Mes { get; set; }          // 1 a 12
    public decimal Total { get; set; }
}

public class ArticuloTop
{
    public string Nombre { get; set; } = "";
    public int Unidades { get; set; }
}

public class VentaResumen
{
    public int IdVenta { get; set; }
    public DateTime Fecha { get; set; }
    public decimal Total { get; set; }
    public string Estado { get; set; } = "";
}

/// <summary>Lo que ve ADMIN en Inicio.</summary>
public class InicioAdminViewModel
{
    /// <summary>Primer día del mes que se resume (el de la última venta registrada).</summary>
    public DateTime MesReferencia { get; set; }
    public int VentasMes { get; set; }
    public decimal VendidoMes { get; set; }
    public decimal GananciaMes { get; set; }
    public List<ArticuloAlerta> PorReabastecer { get; set; } = new();
    public List<VentaMes> VentasPorMes { get; set; } = new();
    public List<ArticuloTop> MasVendidos { get; set; } = new();
}

/// <summary>Lo que ve el vendedor en Inicio (sin ganancias).</summary>
public class InicioVendedorViewModel
{
    public DateTime Hoy { get; set; }
    public int VentasHoy { get; set; }
    public decimal VendidoHoy { get; set; }
    public List<ArticuloAlerta> PorReabastecer { get; set; } = new();
    public List<VentaResumen> UltimasVentas { get; set; } = new();
}
