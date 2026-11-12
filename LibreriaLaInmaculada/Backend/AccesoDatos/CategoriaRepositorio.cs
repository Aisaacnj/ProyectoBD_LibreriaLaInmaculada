using LibreriaLaInmaculada.Backend.Modelos;
using Microsoft.Data.SqlClient;

namespace LibreriaLaInmaculada.Backend.AccesoDatos;

public class CategoriaRepositorio
{
    private readonly Db _db;

    public CategoriaRepositorio(Db db) => _db = db;

    /// <summary>Categorías con la cantidad de artículos activos de cada una.</summary>
    public async Task<List<Categoria>> ListarAsync(bool incluirInactivas)
    {
        const string sql = @"
            SELECT C.idCategoria, C.Nombre, C.Activo, C.Fecha_Registro, C.Fecha_Actualizacion,
                   COUNT(A.idArticulo) AS TotalArticulos
            FROM dbo.CAT_Categoria C
            LEFT JOIN dbo.CAT_Articulos A ON A.idCategoria = C.idCategoria AND A.Activo = 1
            WHERE (@IncluirInactivas = 1 OR C.Activo = 1)
            GROUP BY C.idCategoria, C.Nombre, C.Activo, C.Fecha_Registro, C.Fecha_Actualizacion
            ORDER BY C.Nombre;";

        var lista = new List<Categoria>();
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Bit("@IncluirInactivas", incluirInactivas);
        await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
            lista.Add(new Categoria
            {
                IdCategoria = rd.GetInt32(0),
                Nombre = rd.GetString(1),
                Activo = rd.GetBoolean(2),
                FechaRegistro = rd.GetDateTime(3),
                FechaActualizacion = rd.GetDateTime(4),
                TotalArticulos = rd.GetInt32(5)
            });
        return lista;
    }

    public async Task<Categoria?> ObtenerAsync(int id)
    {
        const string sql = @"
            SELECT idCategoria, Nombre, Activo, Fecha_Registro, Fecha_Actualizacion
            FROM dbo.CAT_Categoria WHERE idCategoria = @Id;";
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Entero("@Id", id);
        await using var rd = await cmd.ExecuteReaderAsync();
        if (!await rd.ReadAsync()) return null;
        return new Categoria
        {
            IdCategoria = rd.GetInt32(0),
            Nombre = rd.GetString(1),
            Activo = rd.GetBoolean(2),
            FechaRegistro = rd.GetDateTime(3),
            FechaActualizacion = rd.GetDateTime(4)
        };
    }

    public async Task<bool> ExisteNombreAsync(string nombre, int? excluirId)
    {
        const string sql = @"
            SELECT COUNT(*) FROM dbo.CAT_Categoria
            WHERE Nombre = @Nombre AND (@Excluir IS NULL OR idCategoria <> @Excluir);";
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Texto("@Nombre", nombre, 80);
        cmd.Entero("@Excluir", excluirId);
        return (int)(await cmd.ExecuteScalarAsync() ?? 0) > 0;
    }

    public async Task CrearAsync(string nombre)
    {
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand("INSERT INTO dbo.CAT_Categoria (Nombre) VALUES (@Nombre);", cn);
        cmd.Texto("@Nombre", nombre, 80);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ActualizarAsync(int id, string nombre)
    {
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand("UPDATE dbo.CAT_Categoria SET Nombre = @Nombre WHERE idCategoria = @Id;", cn);
        cmd.Entero("@Id", id);
        cmd.Texto("@Nombre", nombre, 80);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task CambiarEstadoAsync(int id, bool activo)
    {
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand("UPDATE dbo.CAT_Categoria SET Activo = @Activo WHERE idCategoria = @Id;", cn);
        cmd.Entero("@Id", id);
        cmd.Bit("@Activo", activo);
        await cmd.ExecuteNonQueryAsync();
    }
}
