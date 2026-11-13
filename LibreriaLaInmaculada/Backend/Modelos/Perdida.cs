namespace LibreriaLaInmaculada.Backend.Modelos;

/// <summary>
/// Una salida de inventario que no es venta (tabla dbo.Perdidas_Devoluciones):
/// daño, producto seco, faltante, defecto de fábrica, uso interno...
/// </summary>
public class Perdida
{
    public int IdPerdida { get; set; }
    public int IdArticulo { get; set; }
    public string Articulo { get; set; } = "";
    public int IdMotivo { get; set; }
    public string Motivo { get; set; } = "";
    public int Cantidad { get; set; }
    public decimal CostoAproximado { get; set; }    // Cantidad × costo actual del artículo
    public string? Observacion { get; set; }
    public string RegistradoPor { get; set; } = "";
    public DateTime FechaRegistro { get; set; }

    /// <summary>Solo se puede corregir (eliminar) el mismo día en que se registró.</summary>
    public bool SePuedeCorregir => FechaRegistro.Date == DateTime.Today;
}

/// <summary>Un motivo de pérdida (tabla dbo.CAT_Motivo_Perdida).</summary>
public class MotivoPerdida
{
    public int IdMotivo { get; set; }
    public string Descripcion { get; set; } = "";
    public int TotalRegistros { get; set; }
}

/// <summary>Totales por motivo (para el resumen y los reportes).</summary>
public class PerdidaPorMotivo
{
    public string Motivo { get; set; } = "";
    public int Registros { get; set; }
    public int Unidades { get; set; }
    public decimal Costo { get; set; }
}

/// <summary>Filtros de la lista de pérdidas.</summary>
public class FiltroPerdidas
{
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
    public int? IdMotivo { get; set; }
    public string? Buscar { get; set; }      // parte del nombre del artículo
}
