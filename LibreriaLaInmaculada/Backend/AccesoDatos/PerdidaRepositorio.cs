using System.Data;
using LibreriaLaInmaculada.Backend.Modelos;
using Microsoft.Data.SqlClient;

namespace LibreriaLaInmaculada.Backend.AccesoDatos;

/// <summary>
/// Pérdidas: salidas de inventario que no son venta.
/// Registrar y corregir se hacen dentro de una SqlTransaction: el registro y el stock cambian juntos.
/// </summary>
public class PerdidaRepositorio
{
    /// <summary>Motivo que se usa cuando un cliente devuelve algo dañado.</summary>
    public const string MotivoDanado = "Producto dañado o defectuoso";

    private readonly Db _db;

    public PerdidaRepositorio(Db db) => _db = db;

    private static void Fecha(SqlCommand cmd, string nombre, DateTime? valor) =>
        cmd.Parameters.Add(nombre, SqlDbType.Date).Value = valor.HasValue ? (object)valor.Value.Date : DBNull.Value;

    private const string FiltroSql = @"
            WHERE (@Desde IS NULL OR P.Fecha_Registro >= @Desde)
              AND (@Hasta IS NULL OR P.Fecha_Registro <= @Hasta)
              AND (@IdMotivo IS NULL OR P.idMotivoPerdida = @IdMotivo)
              AND (@Buscar IS NULL OR A.Descripcion LIKE '%' + @Buscar + '%')";

    private static void ParametrosFiltro(SqlCommand cmd, FiltroPerdidas f)
    {
        Fecha(cmd, "@Desde", f.Desde);
        Fecha(cmd, "@Hasta", f.Hasta);
        cmd.Entero("@IdMotivo", f.IdMotivo);
        cmd.Texto("@Buscar", f.Buscar, 150);
    }

    // ---------------------------------------------------------------- Lista
    public async Task<List<Perdida>> ListarAsync(FiltroPerdidas f)
    {
        string sql = $@"
            SELECT P.idPerdida, P.idArticulo, A.Descripcion, P.idMotivoPerdida, M.Descripcion, P.Cantidad,
                   CAST(P.Cantidad * A.Precio_Compra AS DECIMAL(12,2)), P.Observacion, U.Usuario, P.Fecha_Registro
            FROM dbo.Perdidas_Devoluciones P
            INNER JOIN dbo.CAT_Articulos A      ON A.idArticulo      = P.idArticulo
            INNER JOIN dbo.CAT_Motivo_Perdida M ON M.idMotivoPerdida = P.idMotivoPerdida
            INNER JOIN dbo.Usuarios U           ON U.idUsuario       = P.idUsuario
            {FiltroSql}
            ORDER BY P.Fecha_Registro DESC, P.idPerdida DESC;";

        var lista = new List<Perdida>();
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand(sql, cn);
        ParametrosFiltro(cmd, f);
        await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
            lista.Add(new Perdida
            {
                IdPerdida = rd.GetInt32(0),
                IdArticulo = rd.GetInt32(1),
                Articulo = rd.GetString(2),
                IdMotivo = rd.GetInt32(3),
                Motivo = rd.GetString(4),
                Cantidad = rd.GetInt32(5),
                CostoAproximado = rd.GetDecimal(6),
                Observacion = rd.TextoONulo(7),
                RegistradoPor = rd.GetString(8),
                FechaRegistro = rd.GetDateTime(9)
            });
        return lista;
    }

    /// <summary>Totales por motivo de lo que cumple el filtro.</summary>
    public async Task<List<PerdidaPorMotivo>> TotalizarPorMotivoAsync(FiltroPerdidas f)
    {
        string sql = $@"
            SELECT M.Descripcion, COUNT(*), SUM(P.Cantidad), SUM(CAST(P.Cantidad * A.Precio_Compra AS DECIMAL(12,2)))
            FROM dbo.Perdidas_Devoluciones P
            INNER JOIN dbo.CAT_Articulos A      ON A.idArticulo      = P.idArticulo
            INNER JOIN dbo.CAT_Motivo_Perdida M ON M.idMotivoPerdida = P.idMotivoPerdida
            {FiltroSql}
            GROUP BY M.Descripcion
            ORDER BY SUM(CAST(P.Cantidad * A.Precio_Compra AS DECIMAL(12,2))) DESC;";

        var lista = new List<PerdidaPorMotivo>();
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand(sql, cn);
        ParametrosFiltro(cmd, f);
        await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
            lista.Add(new PerdidaPorMotivo
            {
                Motivo = rd.GetString(0),
                Registros = rd.GetInt32(1),
                Unidades = rd.GetInt32(2),
                Costo = rd.GetDecimal(3)
            });
        return lista;
    }

    public async Task<Perdida?> ObtenerAsync(int id)
    {
        var lista = await ListarPorIdAsync(id);
        return lista.FirstOrDefault();
    }

    private async Task<List<Perdida>> ListarPorIdAsync(int id)
    {
        const string sql = @"
            SELECT P.idPerdida, P.idArticulo, A.Descripcion, P.Cantidad, P.Fecha_Registro
            FROM dbo.Perdidas_Devoluciones P
            INNER JOIN dbo.CAT_Articulos A ON A.idArticulo = P.idArticulo
            WHERE P.idPerdida = @Id;";
        var lista = new List<Perdida>();
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Entero("@Id", id);
        await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
            lista.Add(new Perdida
            {
                IdPerdida = rd.GetInt32(0),
                IdArticulo = rd.GetInt32(1),
                Articulo = rd.GetString(2),
                Cantidad = rd.GetInt32(3),
                FechaRegistro = rd.GetDateTime(4)
            });
        return lista;
    }

    // ---------------------------------------------------------------- Motivos
    public async Task<List<MotivoPerdida>> ListarMotivosAsync()
    {
        const string sql = @"
            SELECT M.idMotivoPerdida, M.Descripcion,
                   (SELECT COUNT(*) FROM dbo.Perdidas_Devoluciones P WHERE P.idMotivoPerdida = M.idMotivoPerdida)
            FROM dbo.CAT_Motivo_Perdida M
            ORDER BY M.Descripcion;";
        var lista = new List<MotivoPerdida>();
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand(sql, cn);
        await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
            lista.Add(new MotivoPerdida { IdMotivo = rd.GetInt32(0), Descripcion = rd.GetString(1), TotalRegistros = rd.GetInt32(2) });
        return lista;
    }

    public async Task<bool> ExisteMotivoAsync(string descripcion)
    {
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.CAT_Motivo_Perdida WHERE Descripcion = @Desc;", cn);
        cmd.Texto("@Desc", descripcion, 150);
        return (int)(await cmd.ExecuteScalarAsync() ?? 0) > 0;
    }

    public async Task CrearMotivoAsync(string descripcion)
    {
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand("INSERT INTO dbo.CAT_Motivo_Perdida (Descripcion) VALUES (@Desc);", cn);
        cmd.Texto("@Desc", descripcion, 150);
        await cmd.ExecuteNonQueryAsync();
    }

    // ---------------------------------------------------------------- Registrar
    /// <summary>
    /// Registra la pérdida y saca las unidades del stock, en UNA transacción.
    /// Devuelve null si se guardó, o el motivo por el que no se pudo (por ejemplo, no hay tanto stock).
    /// </summary>
    public async Task<string?> RegistrarAsync(int idArticulo, int idMotivo, int cantidad, string? observacion, int idUsuario)
    {
        await using var cn = await _db.AbrirAsync();
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync();
        try
        {
            string? nombre = null;
            int stock = 0;
            await using (var cmd = new SqlCommand(
                "SELECT Descripcion, Cantidad FROM dbo.CAT_Articulos WITH (UPDLOCK) WHERE idArticulo = @Id;", cn, tx))
            {
                cmd.Entero("@Id", idArticulo);
                await using var rd = await cmd.ExecuteReaderAsync();
                if (await rd.ReadAsync()) { nombre = rd.GetString(0); stock = rd.GetInt32(1); }
            }
            if (nombre is null) { await tx.RollbackAsync(); return "El artículo no existe."; }
            if (stock < cantidad)
            {
                await tx.RollbackAsync();
                return stock == 0
                    ? $"«{nombre}» no tiene unidades en stock: no se puede registrar una pérdida."
                    : $"De «{nombre}» solo hay {stock} en stock: no se pueden dar de baja {cantidad}.";
            }

            await InsertarAsync(cn, tx, idArticulo, idMotivo, idUsuario, cantidad, observacion);
            await using (var cmd = new SqlCommand(
                "UPDATE dbo.CAT_Articulos SET Cantidad = Cantidad - @Cantidad WHERE idArticulo = @Id;", cn, tx))
            {
                cmd.Entero("@Id", idArticulo);
                cmd.Entero("@Cantidad", cantidad);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            return null;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Inserta el registro de pérdida dentro de una transacción ya abierta.
    /// Lo usan RegistrarAsync y la devolución de ventas (artículos que el cliente devolvió dañados).
    /// NO toca el stock: cada quien lo ajusta.
    /// </summary>
    internal static async Task InsertarAsync(SqlConnection cn, SqlTransaction tx, int idArticulo, int idMotivo,
                                             int idUsuario, int cantidad, string? observacion)
    {
        await using var cmd = new SqlCommand(@"
            INSERT INTO dbo.Perdidas_Devoluciones (idArticulo, idMotivoPerdida, idUsuario, Cantidad, Observacion)
            VALUES (@IdArticulo, @IdMotivo, @IdUsuario, @Cantidad, @Observacion);", cn, tx);
        cmd.Entero("@IdArticulo", idArticulo);
        cmd.Entero("@IdMotivo", idMotivo);
        cmd.Entero("@IdUsuario", idUsuario);
        cmd.Entero("@Cantidad", cantidad);
        cmd.Texto("@Observacion", observacion, 255);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Busca el id de un motivo por su nombre, dentro de una transacción ya abierta.</summary>
    internal static async Task<int?> BuscarMotivoAsync(SqlConnection cn, SqlTransaction tx, string descripcion)
    {
        await using var cmd = new SqlCommand(
            "SELECT idMotivoPerdida FROM dbo.CAT_Motivo_Perdida WHERE Descripcion = @Desc;", cn, tx);
        cmd.Texto("@Desc", descripcion, 150);
        return await cmd.ExecuteScalarAsync() as int?;
    }

    // ---------------------------------------------------------------- Corregir
    /// <summary>
    /// Corrige un error: elimina el registro de pérdida y devuelve las unidades al stock, en UNA transacción.
    /// Solo se permite el MISMO DÍA en que se registró, para que el historial no se pueda maquillar después.
    /// </summary>
    public async Task<string?> EliminarAsync(int idPerdida)
    {
        await using var cn = await _db.AbrirAsync();
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync();
        try
        {
            int idArticulo = 0, cantidad = 0;
            DateTime? fecha = null;
            await using (var cmd = new SqlCommand(
                "SELECT idArticulo, Cantidad, Fecha_Registro FROM dbo.Perdidas_Devoluciones WITH (UPDLOCK) WHERE idPerdida = @Id;", cn, tx))
            {
                cmd.Entero("@Id", idPerdida);
                await using var rd = await cmd.ExecuteReaderAsync();
                if (await rd.ReadAsync()) { idArticulo = rd.GetInt32(0); cantidad = rd.GetInt32(1); fecha = rd.GetDateTime(2); }
            }
            if (fecha is null) { await tx.RollbackAsync(); return "El registro no existe."; }
            if (fecha.Value.Date != DateTime.Today)
            {
                await tx.RollbackAsync();
                return "Solo se pueden corregir las pérdidas registradas hoy. Las de días anteriores quedan en el historial.";
            }

            await using (var cmd = new SqlCommand(@"
                UPDATE dbo.CAT_Articulos SET Cantidad = Cantidad + @Cantidad WHERE idArticulo = @IdArticulo;
                DELETE FROM dbo.Perdidas_Devoluciones WHERE idPerdida = @Id;", cn, tx))
            {
                cmd.Entero("@Id", idPerdida);
                cmd.Entero("@IdArticulo", idArticulo);
                cmd.Entero("@Cantidad", cantidad);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            return null;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
