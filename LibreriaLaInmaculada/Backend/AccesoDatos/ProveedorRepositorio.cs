using LibreriaLaInmaculada.Backend.Modelos;
using Microsoft.Data.SqlClient;

namespace LibreriaLaInmaculada.Backend.AccesoDatos;

public class ProveedorRepositorio
{
    private readonly Db _db;

    public ProveedorRepositorio(Db db) => _db = db;

    private const string Columnas = @"
        P.idProveedor, P.Nombre, P.Contacto_Nombre, P.Telefono, P.Email, P.Direccion,
        P.Activo, P.Fecha_Registro, P.Fecha_Actualizacion";

    private static Proveedor Leer(SqlDataReader rd) => new()
    {
        IdProveedor = rd.GetInt32(0),
        Nombre = rd.GetString(1),
        ContactoNombre = rd.TextoONulo(2),
        Telefono = rd.TextoONulo(3),
        Email = rd.TextoONulo(4),
        Direccion = rd.TextoONulo(5),
        Activo = rd.GetBoolean(6),
        FechaRegistro = rd.GetDateTime(7),
        FechaActualizacion = rd.GetDateTime(8)
    };

    public async Task<List<Proveedor>> ListarAsync(string? buscar, bool incluirInactivos)
    {
        string sql = $@"
            SELECT {Columnas},
                   (SELECT COUNT(*) FROM dbo.Compra C
                    WHERE C.idProveedor = P.idProveedor AND C.Estado = 'Activa') AS TotalCompras
            FROM dbo.CAT_Proveedor P
            WHERE (@Buscar IS NULL OR P.Nombre LIKE '%' + @Buscar + '%' OR P.Contacto_Nombre LIKE '%' + @Buscar + '%')
              AND (@IncluirInactivos = 1 OR P.Activo = 1)
            ORDER BY P.Activo DESC, P.Nombre;";

        var lista = new List<Proveedor>();
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Texto("@Buscar", buscar, 100);
        cmd.Bit("@IncluirInactivos", incluirInactivos);
        await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
        {
            var p = Leer(rd);
            p.TotalCompras = rd.GetInt32(9);
            lista.Add(p);
        }
        return lista;
    }

    public async Task<Proveedor?> ObtenerAsync(int id)
    {
        string sql = $"SELECT {Columnas} FROM dbo.CAT_Proveedor P WHERE P.idProveedor = @Id;";
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Entero("@Id", id);
        await using var rd = await cmd.ExecuteReaderAsync();
        return await rd.ReadAsync() ? Leer(rd) : null;
    }

    public async Task<bool> ExisteNombreAsync(string nombre, int? excluirId)
    {
        const string sql = @"
            SELECT COUNT(*) FROM dbo.CAT_Proveedor
            WHERE Nombre = @Nombre AND (@Excluir IS NULL OR idProveedor <> @Excluir);";
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Texto("@Nombre", nombre, 100);
        cmd.Entero("@Excluir", excluirId);
        return (int)(await cmd.ExecuteScalarAsync() ?? 0) > 0;
    }

    public async Task CrearAsync(Proveedor p)
    {
        const string sql = @"
            INSERT INTO dbo.CAT_Proveedor (Nombre, Contacto_Nombre, Telefono, Email, Direccion)
            VALUES (@Nombre, @Contacto, @Telefono, @Email, @Direccion);";
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand(sql, cn);
        AgregarDatos(cmd, p);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ActualizarAsync(Proveedor p)
    {
        const string sql = @"
            UPDATE dbo.CAT_Proveedor
            SET Nombre = @Nombre, Contacto_Nombre = @Contacto, Telefono = @Telefono,
                Email = @Email, Direccion = @Direccion
            WHERE idProveedor = @Id;";
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Entero("@Id", p.IdProveedor);
        AgregarDatos(cmd, p);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task CambiarEstadoAsync(int id, bool activo)
    {
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand("UPDATE dbo.CAT_Proveedor SET Activo = @Activo WHERE idProveedor = @Id;", cn);
        cmd.Entero("@Id", id);
        cmd.Bit("@Activo", activo);
        await cmd.ExecuteNonQueryAsync();
    }

    private static void AgregarDatos(SqlCommand cmd, Proveedor p)
    {
        cmd.Texto("@Nombre", p.Nombre, 100);
        cmd.Texto("@Contacto", p.ContactoNombre, 100);
        cmd.Texto("@Telefono", p.Telefono, 20);
        cmd.Texto("@Email", p.Email, 100);
        cmd.Texto("@Direccion", p.Direccion, 255);
    }
}
