using LibreriaLaInmaculada.Backend.Modelos;
using Microsoft.Data.SqlClient;

namespace LibreriaLaInmaculada.Backend.AccesoDatos;

public class ArticuloRepositorio
{
    private readonly Db _db;

    public ArticuloRepositorio(Db db) => _db = db;

    private const string Columnas = @"
        idArticulo, idCategoria, Categoria, Descripcion, Stock_Disponible, Stock_Minimo,
        Precio_Compra, Precio_Venta, Activo, Fecha_Registro, Fecha_Actualizacion";

    private static Articulo Leer(SqlDataReader rd) => new()
    {
        IdArticulo = rd.GetInt32(0),
        IdCategoria = rd.GetInt32(1),
        Categoria = rd.GetString(2),
        Descripcion = rd.GetString(3),
        Cantidad = rd.GetInt32(4),
        StockMinimo = rd.GetInt32(5),
        PrecioCompra = rd.GetDecimal(6),
        PrecioVenta = rd.GetDecimal(7),
        Activo = rd.GetBoolean(8),
        FechaRegistro = rd.GetDateTime(9),
        FechaActualizacion = rd.GetDateTime(10)
    };

    public async Task<List<Articulo>> ListarAsync(FiltroInventario f)
    {
        string sql = $@"
            SELECT {Columnas}
            FROM dbo.V_Articulos
            WHERE (@Buscar IS NULL OR Descripcion LIKE '%' + @Buscar + '%')
              AND (@IdCategoria IS NULL OR idCategoria = @IdCategoria)
              AND (@IncluirInactivos = 1 OR Activo = 1)
              AND (@Estado IS NULL
                   OR (@Estado = 'bajo'      AND Bajo_Stock = 1 AND Stock_Disponible > 0)
                   OR (@Estado = 'agotado'   AND Stock_Disponible = 0)
                   OR (@Estado = 'inactivos' AND Activo = 0))
            ORDER BY Descripcion;";

        var lista = new List<Articulo>();
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Texto("@Buscar", f.Buscar, 150);
        cmd.Entero("@IdCategoria", f.IdCategoria);
        cmd.Texto("@Estado", f.Estado, 20);
        cmd.Bit("@IncluirInactivos", f.IncluirInactivos);

        await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
            lista.Add(Leer(rd));
        return lista;
    }

    /// <summary>Totales de artículos activos para las tarjetas de arriba.</summary>
    public async Task<(int Activos, int BajoStock, int Agotados, decimal ValorInventario)> ObtenerResumenAsync()
    {
        const string sql = @"
            SELECT COUNT(*),
                   ISNULL(SUM(CASE WHEN Bajo_Stock = 1 AND Stock_Disponible > 0 THEN 1 ELSE 0 END), 0),
                   ISNULL(SUM(CASE WHEN Stock_Disponible = 0 THEN 1 ELSE 0 END), 0),
                   ISNULL(SUM(Stock_Disponible * Precio_Compra), 0)
            FROM dbo.V_Articulos
            WHERE Activo = 1;";

        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand(sql, cn);
        await using var rd = await cmd.ExecuteReaderAsync();
        await rd.ReadAsync();
        return (rd.GetInt32(0), rd.GetInt32(1), rd.GetInt32(2), rd.GetDecimal(3));
    }

    public async Task<Articulo?> ObtenerAsync(int id)
    {
        string sql = $"SELECT {Columnas} FROM dbo.V_Articulos WHERE idArticulo = @Id;";
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Entero("@Id", id);
        await using var rd = await cmd.ExecuteReaderAsync();
        return await rd.ReadAsync() ? Leer(rd) : null;
    }

    public async Task<bool> ExisteDescripcionAsync(string descripcion, int? excluirId)
    {
        const string sql = @"
            SELECT COUNT(*) FROM dbo.CAT_Articulos
            WHERE Descripcion = @Descripcion AND (@Excluir IS NULL OR idArticulo <> @Excluir);";
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Texto("@Descripcion", descripcion, 150);
        cmd.Entero("@Excluir", excluirId);
        return (int)(await cmd.ExecuteScalarAsync() ?? 0) > 0;
    }

    /// <summary>Crea un artículo nuevo con stock 0 (el stock entra con las compras).</summary>
    public async Task<int> CrearAsync(Articulo a)
    {
        const string sql = @"
            INSERT INTO dbo.CAT_Articulos (idCategoria, Descripcion, Cantidad, Stock_Minimo, Precio_Compra, Precio_Venta)
            VALUES (@IdCategoria, @Descripcion, 0, @StockMinimo, @PrecioCompra, @PrecioVenta);
            SELECT CAST(SCOPE_IDENTITY() AS int);";
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Entero("@IdCategoria", a.IdCategoria);
        cmd.Texto("@Descripcion", a.Descripcion, 150);
        cmd.Entero("@StockMinimo", a.StockMinimo);
        cmd.Moneda("@PrecioCompra", a.PrecioCompra);
        cmd.Moneda("@PrecioVenta", a.PrecioVenta);
        return (int)(await cmd.ExecuteScalarAsync() ?? 0);
    }

    /// <summary>Actualiza los datos del artículo. NO toca la cantidad en stock.</summary>
    public async Task ActualizarAsync(Articulo a)
    {
        const string sql = @"
            UPDATE dbo.CAT_Articulos
            SET idCategoria = @IdCategoria, Descripcion = @Descripcion, Stock_Minimo = @StockMinimo,
                Precio_Compra = @PrecioCompra, Precio_Venta = @PrecioVenta
            WHERE idArticulo = @Id;";
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Entero("@Id", a.IdArticulo);
        cmd.Entero("@IdCategoria", a.IdCategoria);
        cmd.Texto("@Descripcion", a.Descripcion, 150);
        cmd.Entero("@StockMinimo", a.StockMinimo);
        cmd.Moneda("@PrecioCompra", a.PrecioCompra);
        cmd.Moneda("@PrecioVenta", a.PrecioVenta);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task CambiarEstadoAsync(int id, bool activo)
    {
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand("UPDATE dbo.CAT_Articulos SET Activo = @Activo WHERE idArticulo = @Id;", cn);
        cmd.Entero("@Id", id);
        cmd.Bit("@Activo", activo);
        await cmd.ExecuteNonQueryAsync();
    }
}
