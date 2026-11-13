using System.Data;
using LibreriaLaInmaculada.Backend.Modelos;
using Microsoft.Data.SqlClient;

namespace LibreriaLaInmaculada.Backend.AccesoDatos;

/// <summary>
/// Compras a proveedores.
/// Registrar y anular se hacen dentro de una SqlTransaction: o se guarda TODO
/// (la compra, sus líneas y el stock), o no se guarda nada.
/// </summary>
public class CompraRepositorio
{
    private readonly Db _db;

    public CompraRepositorio(Db db) => _db = db;

    private static void Fecha(SqlCommand cmd, string nombre, DateTime? valor) =>
        cmd.Parameters.Add(nombre, SqlDbType.Date).Value = valor.HasValue ? (object)valor.Value.Date : DBNull.Value;

    // ---------------------------------------------------------------- Lista
    public async Task<List<Compra>> ListarAsync(FiltroCompras f)
    {
        const string sql = @"
            SELECT C.idCompra, C.Num_Factura_Proveedor, C.idProveedor, C.Proveedor, C.Fecha_Compra,
                   C.SubTotal, C.IVA, C.Total, C.Estado, C.Registrado_Por, C.Fecha_Registro,
                   (SELECT COUNT(*)                 FROM dbo.Compra_Detalle D WHERE D.idCompra = C.idCompra) AS Lineas,
                   (SELECT ISNULL(SUM(D.Cantidad),0) FROM dbo.Compra_Detalle D WHERE D.idCompra = C.idCompra) AS Unidades
            FROM dbo.V_Compra C
            WHERE (@Desde IS NULL OR C.Fecha_Compra >= @Desde)
              AND (@Hasta IS NULL OR C.Fecha_Compra <= @Hasta)
              AND (@IdProveedor IS NULL OR C.idProveedor = @IdProveedor)
              AND (@Estado = 'Todas' OR C.Estado = @Estado)
            ORDER BY C.Fecha_Compra DESC, C.idCompra DESC;";

        var lista = new List<Compra>();
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand(sql, cn);
        Fecha(cmd, "@Desde", f.Desde);
        Fecha(cmd, "@Hasta", f.Hasta);
        cmd.Entero("@IdProveedor", f.IdProveedor);
        cmd.Texto("@Estado", f.Estado, 10);

        await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
        {
            var c = LeerEncabezado(rd);
            c.TotalLineas = rd.GetInt32(11);
            c.TotalUnidades = rd.GetInt32(12);
            lista.Add(c);
        }
        return lista;
    }

    private static Compra LeerEncabezado(SqlDataReader rd) => new()
    {
        IdCompra = rd.GetInt32(0),
        NumFactura = rd.GetString(1),
        IdProveedor = rd.GetInt32(2),
        Proveedor = rd.GetString(3),
        FechaCompra = rd.GetDateTime(4),
        SubTotal = rd.GetDecimal(5),
        IVA = rd.GetDecimal(6),
        Total = rd.GetDecimal(7),
        Estado = rd.GetString(8),
        RegistradoPor = rd.GetString(9),
        FechaRegistro = rd.GetDateTime(10)
    };

    /// <summary>Compras activas del mes actual: cuántas, total pagado e IVA pagado.</summary>
    public async Task<(int Cantidad, decimal Total, decimal IVA)> ObtenerResumenMesAsync()
    {
        const string sql = @"
            SELECT COUNT(*), ISNULL(SUM(Total), 0), ISNULL(SUM(IVA), 0)
            FROM dbo.Compra
            WHERE Estado = 'Activa'
              AND Fecha_Compra >= DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1)
              AND Fecha_Compra <  DATEADD(MONTH, 1, DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1));";

        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand(sql, cn);
        await using var rd = await cmd.ExecuteReaderAsync();
        await rd.ReadAsync();
        return (rd.GetInt32(0), rd.GetDecimal(1), rd.GetDecimal(2));
    }

    // ---------------------------------------------------------------- Detalle
    public async Task<Compra?> ObtenerAsync(int id)
    {
        const string sqlEncabezado = @"
            SELECT idCompra, Num_Factura_Proveedor, idProveedor, Proveedor, Fecha_Compra,
                   SubTotal, IVA, Total, Estado, Registrado_Por, Fecha_Registro
            FROM dbo.V_Compra WHERE idCompra = @Id;";

        const string sqlLineas = @"
            SELECT idArticulo, Nombre_Articulo, Categoria, Cantidad, Precio_Compra_Unitario,
                   Porcentaje_IVA, SubTotal, IVA, Total
            FROM dbo.V_CompraDetalle WHERE idCompra = @Id
            ORDER BY idDetalleCompra;";

        await using var cn = await _db.AbrirAsync();

        Compra? compra;
        await using (var cmd = new SqlCommand(sqlEncabezado, cn))
        {
            cmd.Entero("@Id", id);
            await using var rd = await cmd.ExecuteReaderAsync();
            if (!await rd.ReadAsync()) return null;
            compra = LeerEncabezado(rd);
        }

        await using (var cmd = new SqlCommand(sqlLineas, cn))
        {
            cmd.Entero("@Id", id);
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
                compra.Lineas.Add(new CompraDetalle
                {
                    IdArticulo = rd.GetInt32(0),
                    Articulo = rd.GetString(1),
                    Categoria = rd.GetString(2),
                    Cantidad = rd.GetInt32(3),
                    PrecioUnitario = rd.GetDecimal(4),
                    PorcentajeIVA = rd.GetDecimal(5),
                    SubTotal = rd.GetDecimal(6),
                    IVA = rd.GetDecimal(7),
                    Total = rd.GetDecimal(8)
                });
        }
        return compra;
    }

    /// <summary>¿Ya se registró esa factura de ese proveedor? (aunque esté anulada)</summary>
    public async Task<bool> ExisteFacturaAsync(int idProveedor, string numFactura)
    {
        const string sql = @"
            SELECT COUNT(*) FROM dbo.Compra
            WHERE idProveedor = @IdProveedor AND Num_Factura_Proveedor = @Num;";
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Entero("@IdProveedor", idProveedor);
        cmd.Texto("@Num", numFactura, 30);
        return (int)(await cmd.ExecuteScalarAsync() ?? 0) > 0;
    }

    // ---------------------------------------------------------------- Registrar
    /// <summary>
    /// Guarda la compra en UNA transacción:
    ///   1. el encabezado (dbo.Compra) con SubTotal e IVA ya sumados,
    ///   2. cada línea (dbo.Compra_Detalle),
    ///   3. suma las unidades al stock y guarda el costo de esta compra como Precio_Compra del artículo.
    /// Si algo falla, se deshace todo (Rollback). Devuelve el idCompra nuevo.
    /// </summary>
    public async Task<int> RegistrarAsync(Compra compra, int idUsuario)
    {
        foreach (var l in compra.Lineas) l.CalcularImportes();
        compra.SubTotal = compra.Lineas.Sum(l => l.SubTotal);
        compra.IVA = compra.Lineas.Sum(l => l.IVA);

        const string sqlCompra = @"
            INSERT INTO dbo.Compra (Num_Factura_Proveedor, idProveedor, idUsuario, Fecha_Compra, SubTotal, IVA)
            VALUES (@Num, @IdProveedor, @IdUsuario, @Fecha, @SubTotal, @IVA);
            SELECT CAST(SCOPE_IDENTITY() AS int);";

        const string sqlLinea = @"
            INSERT INTO dbo.Compra_Detalle (idCompra, idArticulo, Cantidad, Precio_Compra_Unitario, Porcentaje_IVA)
            VALUES (@IdCompra, @IdArticulo, @Cantidad, @Precio, @PorcIVA);";

        const string sqlStock = @"
            UPDATE dbo.CAT_Articulos
            SET Cantidad = Cantidad + @Cantidad,
                Precio_Compra = @Precio
            WHERE idArticulo = @IdArticulo;";

        await using var cn = await _db.AbrirAsync();
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync();
        try
        {
            int idCompra;
            await using (var cmd = new SqlCommand(sqlCompra, cn, tx))
            {
                cmd.Texto("@Num", compra.NumFactura, 30);
                cmd.Entero("@IdProveedor", compra.IdProveedor);
                cmd.Entero("@IdUsuario", idUsuario);
                Fecha(cmd, "@Fecha", compra.FechaCompra);
                cmd.Moneda("@SubTotal", compra.SubTotal);
                cmd.Moneda("@IVA", compra.IVA);
                idCompra = (int)(await cmd.ExecuteScalarAsync())!;
            }

            foreach (var l in compra.Lineas)
            {
                await using (var cmd = new SqlCommand(sqlLinea, cn, tx))
                {
                    cmd.Entero("@IdCompra", idCompra);
                    cmd.Entero("@IdArticulo", l.IdArticulo);
                    cmd.Entero("@Cantidad", l.Cantidad);
                    cmd.Moneda("@Precio", l.PrecioUnitario);
                    cmd.Moneda("@PorcIVA", l.PorcentajeIVA);
                    await cmd.ExecuteNonQueryAsync();
                }

                await using (var cmd = new SqlCommand(sqlStock, cn, tx))
                {
                    cmd.Entero("@IdArticulo", l.IdArticulo);
                    cmd.Entero("@Cantidad", l.Cantidad);
                    cmd.Moneda("@Precio", l.PrecioUnitario);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            await tx.CommitAsync();
            return idCompra;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // ---------------------------------------------------------------- Anular
    /// <summary>
    /// Anula la compra en UNA transacción: resta del stock las unidades que entraron
    /// y marca la compra como 'Anulada' (no se borra).
    /// No se puede si parte de esas unidades ya se vendió (el stock quedaría negativo).
    /// Devuelve null si se anuló, o el motivo por el que no se pudo.
    /// </summary>
    public async Task<string?> AnularAsync(int idCompra)
    {
        await using var cn = await _db.AbrirAsync();
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync();
        try
        {
            // 1. ¿Existe y sigue activa? (UPDLOCK: nadie más la toca mientras tanto)
            string? estado;
            await using (var cmd = new SqlCommand(
                "SELECT Estado FROM dbo.Compra WITH (UPDLOCK) WHERE idCompra = @Id;", cn, tx))
            {
                cmd.Entero("@Id", idCompra);
                estado = await cmd.ExecuteScalarAsync() as string;
            }
            if (estado is null) { await tx.RollbackAsync(); return "La compra no existe."; }
            if (estado == "Anulada") { await tx.RollbackAsync(); return "Esta compra ya estaba anulada."; }

            // 2. ¿Queda suficiente stock de cada artículo para devolver lo que entró?
            const string sqlFalta = @"
                SELECT TOP (1) A.Descripcion, D.Cantidad, A.Cantidad
                FROM dbo.Compra_Detalle D
                INNER JOIN dbo.CAT_Articulos A WITH (UPDLOCK) ON A.idArticulo = D.idArticulo
                WHERE D.idCompra = @Id AND A.Cantidad < D.Cantidad;";
            await using (var cmd = new SqlCommand(sqlFalta, cn, tx))
            {
                cmd.Entero("@Id", idCompra);
                await using var rd = await cmd.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                {
                    string motivo = $"No se puede anular: con esta compra entraron {rd.GetInt32(1)} de «{rd.GetString(0)}», " +
                                    $"pero solo quedan {rd.GetInt32(2)} en stock (el resto ya se vendió o se dio de baja).";
                    await rd.CloseAsync();
                    await tx.RollbackAsync();
                    return motivo;
                }
            }

            // 3. Restar el stock y marcar la compra como anulada
            const string sqlAnular = @"
                UPDATE A SET A.Cantidad = A.Cantidad - D.Cantidad
                FROM dbo.CAT_Articulos A
                INNER JOIN dbo.Compra_Detalle D ON D.idArticulo = A.idArticulo
                WHERE D.idCompra = @Id;

                UPDATE dbo.Compra SET Estado = 'Anulada' WHERE idCompra = @Id;";
            await using (var cmd = new SqlCommand(sqlAnular, cn, tx))
            {
                cmd.Entero("@Id", idCompra);
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
