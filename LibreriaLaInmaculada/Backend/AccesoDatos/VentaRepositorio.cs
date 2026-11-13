using System.Data;
using LibreriaLaInmaculada.Backend.Modelos;
using Microsoft.Data.SqlClient;

namespace LibreriaLaInmaculada.Backend.AccesoDatos;

/// <summary>
/// Ventas.
/// Registrar, anular y devolver se hacen dentro de una SqlTransaction: o se guarda TODO
/// (la venta, sus líneas y el stock), o no se guarda nada.
/// El precio y el costo de cada artículo se leen de la base en el momento de vender,
/// nunca del navegador, para que nadie pueda cambiar el precio desde la página.
/// </summary>
public class VentaRepositorio
{
    private readonly Db _db;

    public VentaRepositorio(Db db) => _db = db;

    private static void Fecha(SqlCommand cmd, string nombre, DateTime? valor) =>
        cmd.Parameters.Add(nombre, SqlDbType.Date).Value = valor.HasValue ? (object)valor.Value.Date : DBNull.Value;

    private static void FechaHora(SqlCommand cmd, string nombre, DateTime valor) =>
        cmd.Parameters.Add(nombre, SqlDbType.DateTime2).Value = valor;

    private const string FiltroSql = @"
            WHERE (@Desde IS NULL OR V.Fecha_Venta >= @Desde)
              AND (@Hasta IS NULL OR V.Fecha_Venta < DATEADD(DAY, 1, @Hasta))
              AND (@IdUsuario IS NULL OR V.idUsuario = @IdUsuario)
              AND (@Estado = 'Todas' OR V.Estado = @Estado)";

    private static void ParametrosFiltro(SqlCommand cmd, FiltroVentas f)
    {
        Fecha(cmd, "@Desde", f.Desde);
        Fecha(cmd, "@Hasta", f.Hasta);
        cmd.Entero("@IdUsuario", f.IdUsuario);
        cmd.Texto("@Estado", f.Estado, 10);
    }

    // ---------------------------------------------------------------- Lista
    /// <summary>Las ventas más recientes que cumplen el filtro (como máximo f.Limite).</summary>
    public async Task<List<Venta>> ListarAsync(FiltroVentas f)
    {
        string sql = $@"
            SELECT TOP (@Limite)
                   V.idVenta, V.idUsuario, U.Usuario, U.Nombre_Completo, V.Fecha_Venta, V.Total, V.Estado,
                   (SELECT COUNT(*)                  FROM dbo.Venta_Detalle D WHERE D.idVenta = V.idVenta),
                   (SELECT ISNULL(SUM(D.Cantidad), 0) FROM dbo.Venta_Detalle D WHERE D.idVenta = V.idVenta),
                   (SELECT ISNULL(SUM(CAST(D.Cantidad * (D.Precio_Venta_Unitario - D.Costo_Unitario) AS DECIMAL(12,2))), 0)
                    FROM dbo.Venta_Detalle D WHERE D.idVenta = V.idVenta)
            FROM dbo.Venta V
            INNER JOIN dbo.Usuarios U ON U.idUsuario = V.idUsuario
            {FiltroSql}
            ORDER BY V.Fecha_Venta DESC, V.idVenta DESC;";

        var lista = new List<Venta>();
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Entero("@Limite", f.Limite);
        ParametrosFiltro(cmd, f);

        await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
        {
            var v = LeerEncabezado(rd);
            v.TotalLineas = rd.GetInt32(7);
            v.TotalUnidades = rd.GetInt32(8);
            v.Ganancia = rd.GetDecimal(9);
            lista.Add(v);
        }
        return lista;
    }

    /// <summary>Totales de TODO lo que cumple el filtro (no solo de las que se muestran). Montos solo de ventas activas.</summary>
    public async Task<(int Cantidad, decimal Total, decimal Ganancia)> TotalizarAsync(FiltroVentas f)
    {
        string sql = $@"
            SELECT COUNT(*),
                   ISNULL(SUM(CASE WHEN V.Estado = 'Activa' THEN V.Total END), 0),
                   ISNULL((SELECT SUM(CAST(D.Cantidad * (D.Precio_Venta_Unitario - D.Costo_Unitario) AS DECIMAL(12,2)))
                           FROM dbo.Venta_Detalle D INNER JOIN dbo.Venta V ON V.idVenta = D.idVenta
                           {FiltroSql} AND V.Estado = 'Activa'), 0)
            FROM dbo.Venta V
            {FiltroSql};";

        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand(sql, cn);
        ParametrosFiltro(cmd, f);
        await using var rd = await cmd.ExecuteReaderAsync();
        await rd.ReadAsync();
        return (rd.GetInt32(0), rd.GetDecimal(1), rd.GetDecimal(2));
    }

    /// <summary>Tarjetas de arriba: ventas y monto de hoy, monto y ganancia del mes. idUsuario = null para todos.</summary>
    public async Task<(int VentasHoy, decimal VendidoHoy, decimal VendidoMes, decimal GananciaMes)> ObtenerResumenAsync(int? idUsuario)
    {
        const string sql = @"
            SELECT ISNULL(SUM(CASE WHEN V.Fecha_Venta >= @Hoy THEN 1 END), 0),
                   ISNULL(SUM(CASE WHEN V.Fecha_Venta >= @Hoy THEN V.Total END), 0),
                   ISNULL(SUM(V.Total), 0),
                   ISNULL((SELECT SUM(CAST(D.Cantidad * (D.Precio_Venta_Unitario - D.Costo_Unitario) AS DECIMAL(12,2)))
                           FROM dbo.Venta_Detalle D INNER JOIN dbo.Venta V2 ON V2.idVenta = D.idVenta
                           WHERE V2.Estado = 'Activa' AND V2.Fecha_Venta >= @InicioMes AND V2.Fecha_Venta < @FinMes
                             AND (@IdUsuario IS NULL OR V2.idUsuario = @IdUsuario)), 0)
            FROM dbo.Venta V
            WHERE V.Estado = 'Activa' AND V.Fecha_Venta >= @InicioMes AND V.Fecha_Venta < @FinMes
              AND (@IdUsuario IS NULL OR V.idUsuario = @IdUsuario);";

        var hoy = DateTime.Today;
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);

        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand(sql, cn);
        FechaHora(cmd, "@Hoy", hoy);
        FechaHora(cmd, "@InicioMes", inicioMes);
        FechaHora(cmd, "@FinMes", inicioMes.AddMonths(1));
        cmd.Entero("@IdUsuario", idUsuario);
        await using var rd = await cmd.ExecuteReaderAsync();
        await rd.ReadAsync();
        return (rd.GetInt32(0), rd.GetDecimal(1), rd.GetDecimal(2), rd.GetDecimal(3));
    }

    private static Venta LeerEncabezado(SqlDataReader rd) => new()
    {
        IdVenta = rd.GetInt32(0),
        IdUsuario = rd.GetInt32(1),
        Vendedor = rd.GetString(2),
        VendedorNombre = rd.TextoONulo(3),
        FechaVenta = rd.GetDateTime(4),
        Total = rd.GetDecimal(5),
        Estado = rd.GetString(6)
    };

    // ---------------------------------------------------------------- Detalle
    public async Task<Venta?> ObtenerAsync(int id)
    {
        const string sqlEncabezado = @"
            SELECT V.idVenta, V.idUsuario, U.Usuario, U.Nombre_Completo, V.Fecha_Venta, V.Total, V.Estado
            FROM dbo.Venta V
            INNER JOIN dbo.Usuarios U ON U.idUsuario = V.idUsuario
            WHERE V.idVenta = @Id;";

        const string sqlLineas = @"
            SELECT idArticulo, Nombre_Articulo, Categoria, Cantidad, Precio_Venta_Unitario,
                   Costo_Unitario, Total, Ganancia
            FROM dbo.V_VentaDetalle WHERE idVenta = @Id
            ORDER BY idDetalleVenta;";

        await using var cn = await _db.AbrirAsync();

        Venta? venta;
        await using (var cmd = new SqlCommand(sqlEncabezado, cn))
        {
            cmd.Entero("@Id", id);
            await using var rd = await cmd.ExecuteReaderAsync();
            if (!await rd.ReadAsync()) return null;
            venta = LeerEncabezado(rd);
        }

        await using (var cmd = new SqlCommand(sqlLineas, cn))
        {
            cmd.Entero("@Id", id);
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
                venta.Lineas.Add(new VentaDetalle
                {
                    IdArticulo = rd.GetInt32(0),
                    Articulo = rd.GetString(1),
                    Categoria = rd.GetString(2),
                    Cantidad = rd.GetInt32(3),
                    PrecioUnitario = rd.GetDecimal(4),
                    CostoUnitario = rd.GetDecimal(5),
                    Total = rd.GetDecimal(6),
                    Ganancia = rd.GetDecimal(7)
                });
        }
        venta.Ganancia = venta.Lineas.Sum(l => l.Ganancia);
        return venta;
    }

    // ---------------------------------------------------------------- Registrar
    /// <summary>
    /// Registra la venta en UNA transacción:
    ///   1. lee precio, costo y stock de cada artículo con UPDLOCK (nadie más los cambia mientras tanto),
    ///   2. revisa que estén activos y que alcance el stock,
    ///   3. guarda la venta (dbo.Venta) con su total y cada línea (dbo.Venta_Detalle) con su precio y costo,
    ///   4. resta las unidades del stock.
    /// Si algo no cuadra, se deshace todo y se devuelven los motivos.
    /// </summary>
    public async Task<ResultadoVenta> RegistrarAsync(List<LineaVenta> lineas, int idUsuario)
    {
        var resultado = new ResultadoVenta();

        await using var cn = await _db.AbrirAsync();
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync();
        try
        {
            // 1 y 2. Precio, costo y stock actuales (en orden de id para evitar bloqueos cruzados)
            var detalle = new List<(LineaVenta Linea, decimal Precio, decimal Costo)>();
            foreach (var l in lineas.OrderBy(x => x.IdArticulo))
            {
                await using var cmd = new SqlCommand(@"
                    SELECT Descripcion, Cantidad, Precio_Venta, Precio_Compra, Activo
                    FROM dbo.CAT_Articulos WITH (UPDLOCK, ROWLOCK)
                    WHERE idArticulo = @Id;", cn, tx);
                cmd.Entero("@Id", l.IdArticulo);
                await using var rd = await cmd.ExecuteReaderAsync();
                if (!await rd.ReadAsync())
                {
                    resultado.Errores.Add("Uno de los artículos ya no existe.");
                    continue;
                }

                string nombre = rd.GetString(0);
                int stock = rd.GetInt32(1);
                decimal precio = rd.GetDecimal(2);
                decimal costo = rd.GetDecimal(3);
                bool activo = rd.GetBoolean(4);

                if (!activo)
                    resultado.Errores.Add($"«{nombre}» está desactivado y no se puede vender.");
                else if (stock == 0)
                    resultado.Errores.Add($"«{nombre}» está agotado.");
                else if (stock < l.Cantidad)
                    resultado.Errores.Add($"De «{nombre}» solo quedan {stock} y se quieren vender {l.Cantidad}.");

                detalle.Add((l, precio, costo));
            }

            if (!resultado.Exito)
            {
                await tx.RollbackAsync();
                return resultado;
            }

            resultado.Total = detalle.Sum(d => Math.Round(d.Linea.Cantidad * d.Precio, 2, MidpointRounding.AwayFromZero));

            // 3. Encabezado (la fecha y hora las pone la base: SYSDATETIME())
            await using (var cmd = new SqlCommand(@"
                INSERT INTO dbo.Venta (idUsuario, Total) VALUES (@IdUsuario, @Total);
                SELECT CAST(SCOPE_IDENTITY() AS int);", cn, tx))
            {
                cmd.Entero("@IdUsuario", idUsuario);
                cmd.Moneda("@Total", resultado.Total);
                resultado.IdVenta = (int)(await cmd.ExecuteScalarAsync())!;
            }

            // 3 y 4. Líneas y stock
            foreach (var d in detalle)
            {
                await InsertarLineaAsync(cn, tx, resultado.IdVenta, d.Linea.IdArticulo, d.Linea.Cantidad, d.Precio, d.Costo);
                await CambiarStockAsync(cn, tx, d.Linea.IdArticulo, -d.Linea.Cantidad);
            }

            await tx.CommitAsync();
            return resultado;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private static async Task InsertarLineaAsync(SqlConnection cn, SqlTransaction tx, int idVenta,
                                                 int idArticulo, int cantidad, decimal precio, decimal costo)
    {
        await using var cmd = new SqlCommand(@"
            INSERT INTO dbo.Venta_Detalle (idVenta, idArticulo, Cantidad, Precio_Venta_Unitario, Costo_Unitario)
            VALUES (@IdVenta, @IdArticulo, @Cantidad, @Precio, @Costo);", cn, tx);
        cmd.Entero("@IdVenta", idVenta);
        cmd.Entero("@IdArticulo", idArticulo);
        cmd.Entero("@Cantidad", cantidad);
        cmd.Moneda("@Precio", precio);
        cmd.Moneda("@Costo", costo);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Suma (cambio positivo) o resta (negativo) unidades. Nunca deja el stock negativo.</summary>
    private static async Task CambiarStockAsync(SqlConnection cn, SqlTransaction tx, int idArticulo, int cambio)
    {
        await using var cmd = new SqlCommand(@"
            UPDATE dbo.CAT_Articulos SET Cantidad = Cantidad + @Cambio
            WHERE idArticulo = @Id AND Cantidad + @Cambio >= 0;", cn, tx);
        cmd.Entero("@Id", idArticulo);
        cmd.Entero("@Cambio", cambio);
        if (await cmd.ExecuteNonQueryAsync() != 1)
            throw new InvalidOperationException($"No se pudo actualizar el stock del artículo {idArticulo}.");
    }

    /// <summary>Lee la venta y sus líneas bloqueándolas para anular o devolver.</summary>
    private static async Task<(string? Estado, int IdUsuario, DateTime Fecha, List<VentaDetalle> Lineas)> LeerParaCambiarAsync(
        SqlConnection cn, SqlTransaction tx, int idVenta)
    {
        string? estado = null;
        int idUsuario = 0;
        DateTime fecha = default;
        await using (var cmd = new SqlCommand(
            "SELECT Estado, idUsuario, Fecha_Venta FROM dbo.Venta WITH (UPDLOCK) WHERE idVenta = @Id;", cn, tx))
        {
            cmd.Entero("@Id", idVenta);
            await using var rd = await cmd.ExecuteReaderAsync();
            if (await rd.ReadAsync())
            {
                estado = rd.GetString(0);
                idUsuario = rd.GetInt32(1);
                fecha = rd.GetDateTime(2);
            }
        }

        var lineas = new List<VentaDetalle>();
        if (estado is null) return (null, 0, default, lineas);

        await using (var cmd = new SqlCommand(@"
            SELECT D.idArticulo, A.Descripcion, D.Cantidad, D.Precio_Venta_Unitario, D.Costo_Unitario
            FROM dbo.Venta_Detalle D
            INNER JOIN dbo.CAT_Articulos A ON A.idArticulo = D.idArticulo
            WHERE D.idVenta = @Id ORDER BY D.idDetalleVenta;", cn, tx))
        {
            cmd.Entero("@Id", idVenta);
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
                lineas.Add(new VentaDetalle
                {
                    IdArticulo = rd.GetInt32(0),
                    Articulo = rd.GetString(1),
                    Cantidad = rd.GetInt32(2),
                    PrecioUnitario = rd.GetDecimal(3),
                    CostoUnitario = rd.GetDecimal(4)
                });
        }
        return (estado, idUsuario, fecha, lineas);
    }

    // ---------------------------------------------------------------- Anular
    /// <summary>
    /// Anula la venta completa en UNA transacción: devuelve al stock todas sus unidades
    /// y la marca como 'Anulada' (no se borra). Devuelve null si se anuló, o el motivo.
    /// </summary>
    public async Task<string?> AnularAsync(int idVenta)
    {
        await using var cn = await _db.AbrirAsync();
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync();
        try
        {
            var venta = await LeerParaCambiarAsync(cn, tx, idVenta);
            if (venta.Estado is null) { await tx.RollbackAsync(); return "La venta no existe."; }
            if (venta.Estado == "Anulada") { await tx.RollbackAsync(); return "Esta venta ya estaba anulada."; }

            foreach (var l in venta.Lineas)
                await CambiarStockAsync(cn, tx, l.IdArticulo, l.Cantidad);

            await MarcarAnuladaAsync(cn, tx, idVenta);
            await tx.CommitAsync();
            return null;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    private static async Task MarcarAnuladaAsync(SqlConnection cn, SqlTransaction tx, int idVenta)
    {
        await using var cmd = new SqlCommand("UPDATE dbo.Venta SET Estado = 'Anulada' WHERE idVenta = @Id;", cn, tx);
        cmd.Entero("@Id", idVenta);
        await cmd.ExecuteNonQueryAsync();
    }

    // ---------------------------------------------------------------- Devolución parcial
    /// <summary>
    /// El cliente devuelve SOLO ALGUNOS artículos. En UNA transacción:
    ///   1. las unidades devueltas regresan al stock,
    ///   2. la venta original se anula,
    ///   3. se crea una venta nueva con lo que el cliente se quedó, con los MISMOS precios, costos,
    ///      vendedor y fecha de la original (así los reportes siguen cuadrando).
    /// devolver: idArticulo → unidades que devuelve.
    /// Devuelve el error (o null), el número de la venta nueva (null si devolvió todo) y cuántas unidades regresaron.
    /// </summary>
    public async Task<(string? Error, int? VentaNueva, int Devueltas)> DevolverAsync(int idVenta, IDictionary<int, int> devolver)
    {
        await using var cn = await _db.AbrirAsync();
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync();
        try
        {
            var venta = await LeerParaCambiarAsync(cn, tx, idVenta);
            if (venta.Estado is null) { await tx.RollbackAsync(); return ("La venta no existe.", null, 0); }
            if (venta.Estado == "Anulada") { await tx.RollbackAsync(); return ("Esta venta ya está anulada.", null, 0); }

            // Revisar las cantidades
            foreach (var l in venta.Lineas)
            {
                int n = devolver.TryGetValue(l.IdArticulo, out var x) ? x : 0;
                if (n < 0 || n > l.Cantidad)
                {
                    await tx.RollbackAsync();
                    return ($"De «{l.Articulo}» se vendieron {l.Cantidad}: puede devolver entre 0 y {l.Cantidad}.", null, 0);
                }
            }
            int devueltas = venta.Lineas.Sum(l => devolver.TryGetValue(l.IdArticulo, out var x) ? x : 0);
            if (devueltas == 0)
            {
                await tx.RollbackAsync();
                return ("Escriba cuántas unidades de cada artículo devuelve el cliente.", null, 0);
            }

            // 1. Lo devuelto regresa al stock
            foreach (var l in venta.Lineas)
                if (devolver.TryGetValue(l.IdArticulo, out var n) && n > 0)
                    await CambiarStockAsync(cn, tx, l.IdArticulo, n);

            // 2. Anular la original
            await MarcarAnuladaAsync(cn, tx, idVenta);

            // 3. Venta nueva con lo que se queda el cliente (si se queda con algo)
            var quedan = venta.Lineas
                .Select(l => (Linea: l, Cantidad: l.Cantidad - (devolver.TryGetValue(l.IdArticulo, out var n) ? n : 0)))
                .Where(q => q.Cantidad > 0)
                .ToList();

            int? ventaNueva = null;
            if (quedan.Count > 0)
            {
                decimal total = quedan.Sum(q => Math.Round(q.Cantidad * q.Linea.PrecioUnitario, 2, MidpointRounding.AwayFromZero));
                await using (var cmd = new SqlCommand(@"
                    INSERT INTO dbo.Venta (idUsuario, Fecha_Venta, Total) VALUES (@IdUsuario, @Fecha, @Total);
                    SELECT CAST(SCOPE_IDENTITY() AS int);", cn, tx))
                {
                    cmd.Entero("@IdUsuario", venta.IdUsuario);
                    FechaHora(cmd, "@Fecha", venta.Fecha);
                    cmd.Moneda("@Total", total);
                    ventaNueva = (int)(await cmd.ExecuteScalarAsync())!;
                }
                foreach (var q in quedan)
                    await InsertarLineaAsync(cn, tx, ventaNueva.Value, q.Linea.IdArticulo, q.Cantidad,
                                             q.Linea.PrecioUnitario, q.Linea.CostoUnitario);
            }

            await tx.CommitAsync();
            return (null, ventaNueva, devueltas);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
