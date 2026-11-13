using LibreriaLaInmaculada.Backend.Modelos;

namespace LibreriaLaInmaculada.Frontend.ModelosDeVista;

/// <summary>Pantalla Reportes: todo lo que pasó en un período.</summary>
public class ReportesViewModel
{
    // Período elegido
    public string Periodo { get; set; } = "anio";     // "mes", "mesanterior", "anio", "todo" o "personalizado"
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
    public string NombrePeriodo { get; set; } = "";
    public bool PorDia { get; set; }                  // la gráfica va por día (períodos cortos) o por mes

    public ResumenReporte Resumen { get; set; } = new();
    public List<PuntoVentas> VentasPorPeriodo { get; set; } = new();
    public List<FilaArticuloVendido> MasVendidos { get; set; } = new();
    public List<FilaGrupo> PorCategoria { get; set; } = new();
    public List<FilaGrupo> PorVendedor { get; set; } = new();
    public List<FilaGrupo> ComprasPorProveedor { get; set; } = new();
    public List<PerdidaPorMotivo> PerdidasPorMotivo { get; set; } = new();
    public List<FilaReabastecer> PorReabastecer { get; set; } = new();
    public List<FilaSinMovimiento> SinMovimiento { get; set; } = new();
}

/// <summary>Números grandes del período. Solo ventas y compras activas.</summary>
public class ResumenReporte
{
    public int Ventas { get; set; }
    public int VentasAnuladas { get; set; }
    public decimal Vendido { get; set; }
    public int UnidadesVendidas { get; set; }
    public decimal CostoVendido { get; set; }          // lo que costó (sin IVA) lo que se vendió
    public decimal Ganancia => Vendido - CostoVendido;
    public decimal MargenPorcentaje => Vendido == 0 ? 0 : Math.Round(Ganancia / Vendido * 100, 1);
    public decimal TicketPromedio => Ventas == 0 ? 0 : Math.Round(Vendido / Ventas, 2);

    public int Compras { get; set; }
    public decimal Comprado { get; set; }              // con IVA
    public decimal IvaPagado { get; set; }

    public int UnidadesPerdidas { get; set; }
    public decimal CostoPerdidas { get; set; }

    /// <summary>Ganancia de las ventas menos lo que se perdió. Aproximado: no incluye gastos como luz o alquiler.</summary>
    public decimal ResultadoAproximado => Ganancia - CostoPerdidas;
}

/// <summary>Una barra de la gráfica: un día o un mes.</summary>
public class PuntoVentas
{
    public DateTime Periodo { get; set; }
    public int Ventas { get; set; }
    public decimal Total { get; set; }
}

public class FilaArticuloVendido
{
    public string Articulo { get; set; } = "";
    public string Categoria { get; set; } = "";
    public int Unidades { get; set; }
    public decimal Total { get; set; }
    public decimal Ganancia { get; set; }
}

/// <summary>Una fila agrupada: por categoría, por vendedor o por proveedor.</summary>
public class FilaGrupo
{
    public string Nombre { get; set; } = "";
    public int Cantidad { get; set; }      // ventas o compras
    public int Unidades { get; set; }
    public decimal Total { get; set; }
    public decimal Extra { get; set; }     // ganancia (ventas) o IVA (compras)
}

public class FilaReabastecer
{
    public string Articulo { get; set; } = "";
    public string Categoria { get; set; } = "";
    public int Stock { get; set; }
    public int Minimo { get; set; }
    public decimal CostoUnitario { get; set; }
    /// <summary>Lo que hay que comprar para llegar al doble del mínimo.</summary>
    public int Sugerido => Math.Max(Minimo * 2 - Stock, 1);
    public decimal CostoSugerido => Sugerido * CostoUnitario;
}

public class FilaSinMovimiento
{
    public string Articulo { get; set; } = "";
    public string Categoria { get; set; } = "";
    public int Stock { get; set; }
    public decimal Valor { get; set; }     // stock × costo: dinero "parado" en ese artículo
}
