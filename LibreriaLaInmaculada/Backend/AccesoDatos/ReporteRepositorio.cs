using System.Data;
using LibreriaLaInmaculada.Frontend.ModelosDeVista;
using Microsoft.Data.SqlClient;

namespace LibreriaLaInmaculada.Backend.AccesoDatos;

/// <summary>
/// Consultas de la pantalla de Reportes. Solo lectura.
/// Regla de siempre: el dinero y las cantidades cuentan SOLO ventas y compras con Estado = 'Activa'.
/// @Desde y @Hasta son fechas incluidas; si vienen NULL, no hay límite.
/// </summary>
public class ReporteRepositorio
{
    private readonly Db _db;

    public ReporteRepositorio(Db db) => _db = db;

    // Filtros de fecha de cada tabla (Fecha_Venta tiene hora; las otras son solo fecha)
    private const string FiltroVenta = "(@Desde IS NULL OR V.Fecha_Venta >= @Desde) AND (@Hasta IS NULL OR V.Fecha_Venta < DATEADD(DAY, 1, @Hasta))";
    private const string FiltroCompra = "(@Desde IS NULL OR C.Fecha_Compra >= @Desde) AND (@Hasta IS NULL OR C.Fecha_Compra <= @Hasta)";
    private const string FiltroPerdida = "(@Desde IS NULL OR P.Fecha_Registro >= @Desde) AND (@Hasta IS NULL OR P.Fecha_Registro <= @Hasta)";

    /// <summary>Arma el comando con los parámetros @Desde y @Hasta del período.</summary>
    private static SqlCommand Comando(SqlConnection cn, string sql, DateTime? desde, DateTime? hasta)
    {
        var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.Add("@Desde", SqlDbType.Date).Value = desde.HasValue ? (object)desde.Value.Date : DBNull.Value;
        cmd.Parameters.Add("@Hasta", SqlDbType.Date).Value = hasta.HasValue ? (object)hasta.Value.Date : DBNull.Value;
        return cmd;
    }

    // ---------------------------------------------------------------- Resumen
    public async Task<ResumenReporte> ObtenerResumenAsync(DateTime? desde, DateTime? hasta)
    {
        string sql = $@"
            SELECT
                (SELECT COUNT(*) FROM dbo.Venta V WHERE V.Estado = 'Activa' AND {FiltroVenta}),
                (SELECT COUNT(*) FROM dbo.Venta V WHERE V.Estado = 'Anulada' AND {FiltroVenta}),
                (SELECT ISNULL(SUM(V.Total), 0) FROM dbo.Venta V WHERE V.Estado = 'Activa' AND {FiltroVenta}),
                (SELECT ISNULL(SUM(D.Cantidad), 0)
                   FROM dbo.Venta_Detalle D INNER JOIN dbo.Venta V ON V.idVenta = D.idVenta
                  WHERE V.Estado = 'Activa' AND {FiltroVenta}),
                (SELECT ISNULL(SUM(CAST(D.Cantidad * D.Costo_Unitario AS DECIMAL(12,2))), 0)
                   FROM dbo.Venta_Detalle D INNER JOIN dbo.Venta V ON V.idVenta = D.idVenta
                  WHERE V.Estado = 'Activa' AND {FiltroVenta}),
                (SELECT COUNT(*) FROM dbo.Compra C WHERE C.Estado = 'Activa' AND {FiltroCompra}),
                (SELECT ISNULL(SUM(C.Total), 0) FROM dbo.Compra C WHERE C.Estado = 'Activa' AND {FiltroCompra}),
                (SELECT ISNULL(SUM(C.IVA), 0) FROM dbo.Compra C WHERE C.Estado = 'Activa' AND {FiltroCompra}),
                (SELECT ISNULL(SUM(P.Cantidad), 0) FROM dbo.Perdidas_Devoluciones P WHERE {FiltroPerdida}),
                (SELECT ISNULL(SUM(CAST(P.Cantidad * A.Precio_Compra AS DECIMAL(12,2))), 0)
                   FROM dbo.Perdidas_Devoluciones P INNER JOIN dbo.CAT_Articulos A ON A.idArticulo = P.idArticulo
                  WHERE {FiltroPerdida});";

        await using var cn = await _db.AbrirAsync();
        await using var cmd = Comando(cn, sql, desde, hasta);
        await using var rd = await cmd.ExecuteReaderAsync();
        await rd.ReadAsync();
        return new ResumenReporte
        {
            Ventas = rd.GetInt32(0),
            VentasAnuladas = rd.GetInt32(1),
            Vendido = rd.GetDecimal(2),
            UnidadesVendidas = rd.GetInt32(3),
            CostoVendido = rd.GetDecimal(4),
            Compras = rd.GetInt32(5),
            Comprado = rd.GetDecimal(6),
            IvaPagado = rd.GetDecimal(7),
            UnidadesPerdidas = rd.GetInt32(8),
            CostoPerdidas = rd.GetDecimal(9)
        };
    }

    // ---------------------------------------------------------------- Gráfica
    /// <summary>Ventas agrupadas por día o por mes (solo los períodos que tienen ventas).</summary>
    public async Task<List<PuntoVentas>> VentasPorPeriodoAsync(DateTime? desde, DateTime? hasta, bool porDia)
    {
        string grupo = porDia
            ? "CAST(V.Fecha_Venta AS DATE)"
            : "DATEFROMPARTS(YEAR(V.Fecha_Venta), MONTH(V.Fecha_Venta), 1)";
        string sql = $@"
            SELECT {grupo} AS Periodo, COUNT(*), SUM(V.Total)
            FROM dbo.Venta V
            WHERE V.Estado = 'Activa' AND {FiltroVenta}
            GROUP BY {grupo}
            ORDER BY Periodo;";

        var lista = new List<PuntoVentas>();
        await using var cn = await _db.AbrirAsync();
        await using var cmd = Comando(cn, sql, desde, hasta);
        await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
            lista.Add(new PuntoVentas { Periodo = rd.GetDateTime(0), Ventas = rd.GetInt32(1), Total = rd.GetDecimal(2) });
        return lista;
    }

    // ---------------------------------------------------------------- Artículos
    public async Task<List<FilaArticuloVendido>> MasVendidosAsync(DateTime? desde, DateTime? hasta)
    {
        string sql = $@"
            SELECT TOP (50) A.Descripcion, CA.Nombre, SUM(D.Cantidad), SUM(D.Total),
                   SUM(CAST(D.Cantidad * (D.Precio_Venta_Unitario - D.Costo_Unitario) AS DECIMAL(12,2)))
            FROM dbo.Venta_Detalle D
            INNER JOIN dbo.Venta V          ON V.idVenta      = D.idVenta
            INNER JOIN dbo.CAT_Articulos A  ON A.idArticulo   = D.idArticulo
            INNER JOIN dbo.CAT_Categoria CA ON CA.idCategoria = A.idCategoria
            WHERE V.Estado = 'Activa' AND {FiltroVenta}
            GROUP BY A.idArticulo, A.Descripcion, CA.Nombre
            ORDER BY SUM(D.Cantidad) DESC, SUM(D.Total) DESC;";

        var lista = new List<FilaArticuloVendido>();
        await using var cn = await _db.AbrirAsync();
        await using var cmd = Comando(cn, sql, desde, hasta);
        await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
            lista.Add(new FilaArticuloVendido
            {
                Articulo = rd.GetString(0),
                Categoria = rd.GetString(1),
                Unidades = rd.GetInt32(2),
                Total = rd.GetDecimal(3),
                Ganancia = rd.GetDecimal(4)
            });
        return lista;
    }

    public async Task<List<FilaGrupo>> PorCategoriaAsync(DateTime? desde, DateTime? hasta)
    {
        string sql = $@"
            SELECT CA.Nombre, COUNT(DISTINCT V.idVenta), SUM(D.Cantidad), SUM(D.Total),
                   SUM(CAST(D.Cantidad * (D.Precio_Venta_Unitario - D.Costo_Unitario) AS DECIMAL(12,2)))
            FROM dbo.Venta_Detalle D
            INNER JOIN dbo.Venta V          ON V.idVenta      = D.idVenta
            INNER JOIN dbo.CAT_Articulos A  ON A.idArticulo   = D.idArticulo
            INNER JOIN dbo.CAT_Categoria CA ON CA.idCategoria = A.idCategoria
            WHERE V.Estado = 'Activa' AND {FiltroVenta}
            GROUP BY CA.Nombre
            ORDER BY SUM(D.Total) DESC;";
        return await LeerGruposAsync(sql, desde, hasta);
    }

    public async Task<List<FilaGrupo>> PorVendedorAsync(DateTime? desde, DateTime? hasta)
    {
        string sql = $@"
            SELECT ISNULL(U.Nombre_Completo, U.Usuario), COUNT(DISTINCT V.idVenta), SUM(D.Cantidad), SUM(D.Total),
                   SUM(CAST(D.Cantidad * (D.Precio_Venta_Unitario - D.Costo_Unitario) AS DECIMAL(12,2)))
            FROM dbo.Venta_Detalle D
            INNER JOIN dbo.Venta V    ON V.idVenta   = D.idVenta
            INNER JOIN dbo.Usuarios U ON U.idUsuario = V.idUsuario
            WHERE V.Estado = 'Activa' AND {FiltroVenta}
            GROUP BY U.idUsuario, ISNULL(U.Nombre_Completo, U.Usuario)
            ORDER BY SUM(D.Total) DESC;";
        return await LeerGruposAsync(sql, desde, hasta);
    }

    public async Task<List<FilaGrupo>> ComprasPorProveedorAsync(DateTime? desde, DateTime? hasta)
    {
        string sql = $@"
            SELECT PR.Nombre, COUNT(*),
                   ISNULL((SELECT SUM(CD.Cantidad) FROM dbo.Compra_Detalle CD
                           INNER JOIN dbo.Compra C2 ON C2.idCompra = CD.idCompra
                           WHERE C2.idProveedor = PR.idProveedor AND C2.Estado = 'Activa'
                             AND (@Desde IS NULL OR C2.Fecha_Compra >= @Desde) AND (@Hasta IS NULL OR C2.Fecha_Compra <= @Hasta)), 0),
                   SUM(C.Total), SUM(C.IVA)
            FROM dbo.Compra C
            INNER JOIN dbo.CAT_Proveedor PR ON PR.idProveedor = C.idProveedor
            WHERE C.Estado = 'Activa' AND {FiltroCompra}
            GROUP BY PR.idProveedor, PR.Nombre
            ORDER BY SUM(C.Total) DESC;";
        return await LeerGruposAsync(sql, desde, hasta);
    }

    private async Task<List<FilaGrupo>> LeerGruposAsync(string sql, DateTime? desde, DateTime? hasta)
    {
        var lista = new List<FilaGrupo>();
        await using var cn = await _db.AbrirAsync();
        await using var cmd = Comando(cn, sql, desde, hasta);
        await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
            lista.Add(new FilaGrupo
            {
                Nombre = rd.GetString(0),
                Cantidad = rd.GetInt32(1),
                Unidades = rd.GetInt32(2),
                Total = rd.GetDecimal(3),
                Extra = rd.GetDecimal(4)
            });
        return lista;
    }

    // ---------------------------------------------------------------- Inventario
    /// <summary>Artículos activos que llegaron a su stock mínimo (no depende del período).</summary>
    public async Task<List<FilaReabastecer>> PorReabastecerAsync()
    {
        const string sql = @"
            SELECT Descripcion, Categoria, Stock_Disponible, Stock_Minimo, Precio_Compra
            FROM dbo.V_Articulos
            WHERE Activo = 1 AND Bajo_Stock = 1
            ORDER BY Stock_Disponible, Descripcion;";

        var lista = new List<FilaReabastecer>();
        await using var cn = await _db.AbrirAsync();
        await using var cmd = Comando(cn, sql, null, null);
        await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
            lista.Add(new FilaReabastecer
            {
                Articulo = rd.GetString(0),
                Categoria = rd.GetString(1),
                Stock = rd.GetInt32(2),
                Minimo = rd.GetInt32(3),
                CostoUnitario = rd.GetDecimal(4)
            });
        return lista;
    }

    /// <summary>Artículos activos con stock que NO se vendieron en el período (dinero parado).</summary>
    public async Task<List<FilaSinMovimiento>> SinMovimientoAsync(DateTime? desde, DateTime? hasta)
    {
        string sql = $@"
            SELECT TOP (50) A.Descripcion, CA.Nombre, A.Cantidad, CAST(A.Cantidad * A.Precio_Compra AS DECIMAL(12,2)) AS Valor
            FROM dbo.CAT_Articulos A
            INNER JOIN dbo.CAT_Categoria CA ON CA.idCategoria = A.idCategoria
            WHERE A.Activo = 1 AND A.Cantidad > 0
              AND NOT EXISTS (SELECT 1
                              FROM dbo.Venta_Detalle D INNER JOIN dbo.Venta V ON V.idVenta = D.idVenta
                              WHERE D.idArticulo = A.idArticulo AND V.Estado = 'Activa' AND {FiltroVenta})
            ORDER BY Valor DESC;";

        var lista = new List<FilaSinMovimiento>();
        await using var cn = await _db.AbrirAsync();
        await using var cmd = Comando(cn, sql, desde, hasta);
        await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
            lista.Add(new FilaSinMovimiento
            {
                Articulo = rd.GetString(0),
                Categoria = rd.GetString(1),
                Stock = rd.GetInt32(2),
                Valor = rd.GetDecimal(3)
            });
        return lista;
    }
}
