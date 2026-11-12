using LibreriaLaInmaculada.Backend.Modelos;
using LibreriaLaInmaculada.Backend.Seguridad;
using Microsoft.Data.SqlClient;

namespace LibreriaLaInmaculada.Backend.AccesoDatos;

public class UsuarioRepositorio
{
    private readonly Db _db;

    public UsuarioRepositorio(Db db) => _db = db;

    private const string Columnas = "idUsuario, Usuario, Nombre_Completo, Password, Rol, Activo, Fecha_Registro";

    private static Usuario Leer(SqlDataReader rd) => new()
    {
        IdUsuario = rd.GetInt32(0),
        NombreUsuario = rd.GetString(1),
        NombreCompleto = rd.TextoONulo(2),
        Password = rd.GetString(3),
        Rol = rd.GetString(4),
        Activo = rd.GetBoolean(5),
        FechaRegistro = rd.GetDateTime(6)
    };

    /// <summary>Busca un usuario por su nombre de inicio de sesión (para el login).</summary>
    public async Task<Usuario?> ObtenerPorNombreAsync(string nombreUsuario)
    {
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand($"SELECT {Columnas} FROM dbo.Usuarios WHERE Usuario = @Usuario;", cn);
        cmd.Texto("@Usuario", nombreUsuario, 50);
        await using var rd = await cmd.ExecuteReaderAsync();
        return await rd.ReadAsync() ? Leer(rd) : null;
    }

    public async Task<Usuario?> ObtenerPorIdAsync(int id)
    {
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand($"SELECT {Columnas} FROM dbo.Usuarios WHERE idUsuario = @Id;", cn);
        cmd.Entero("@Id", id);
        await using var rd = await cmd.ExecuteReaderAsync();
        return await rd.ReadAsync() ? Leer(rd) : null;
    }

    public async Task<List<Usuario>> ListarAsync()
    {
        var lista = new List<Usuario>();
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand($"SELECT {Columnas} FROM dbo.Usuarios ORDER BY Activo DESC, Rol, Usuario;", cn);
        await using var rd = await cmd.ExecuteReaderAsync();
        while (await rd.ReadAsync())
            lista.Add(Leer(rd));
        return lista;
    }

    public async Task<bool> ExisteUsuarioAsync(string nombreUsuario)
    {
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Usuarios WHERE Usuario = @Usuario;", cn);
        cmd.Texto("@Usuario", nombreUsuario, 50);
        return (int)(await cmd.ExecuteScalarAsync() ?? 0) > 0;
    }

    /// <summary>Cuántos administradores activos hay, sin contar al usuario indicado.</summary>
    public async Task<int> ContarOtrosAdministradoresActivosAsync(int excluirId)
    {
        const string sql = @"
            SELECT COUNT(*) FROM dbo.Usuarios
            WHERE Rol = @Rol AND Activo = 1 AND idUsuario <> @Excluir;";
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Texto("@Rol", Roles.Administrador, 20);
        cmd.Entero("@Excluir", excluirId);
        return (int)(await cmd.ExecuteScalarAsync() ?? 0);
    }

    /// <summary>Crea un usuario. passwordHash debe venir de PasswordHasher.Hash().</summary>
    public async Task CrearAsync(string nombreUsuario, string nombreCompleto, string passwordHash, string rol)
    {
        const string sql = @"
            INSERT INTO dbo.Usuarios (Usuario, Nombre_Completo, Password, Rol)
            VALUES (@Usuario, @Nombre, @Password, @Rol);";
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Texto("@Usuario", nombreUsuario, 50);
        cmd.Texto("@Nombre", nombreCompleto, 100);
        cmd.Texto("@Password", passwordHash, 256);
        cmd.Texto("@Rol", rol, 20);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ActualizarAsync(int id, string nombreCompleto, string rol)
    {
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand(
            "UPDATE dbo.Usuarios SET Nombre_Completo = @Nombre, Rol = @Rol WHERE idUsuario = @Id;", cn);
        cmd.Entero("@Id", id);
        cmd.Texto("@Nombre", nombreCompleto, 100);
        cmd.Texto("@Rol", rol, 20);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task CambiarPasswordAsync(int id, string passwordHash)
    {
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand("UPDATE dbo.Usuarios SET Password = @Password WHERE idUsuario = @Id;", cn);
        cmd.Entero("@Id", id);
        cmd.Texto("@Password", passwordHash, 256);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task CambiarEstadoAsync(int id, bool activo)
    {
        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand("UPDATE dbo.Usuarios SET Activo = @Activo WHERE idUsuario = @Id;", cn);
        cmd.Entero("@Id", id);
        cmd.Bit("@Activo", activo);
        await cmd.ExecuteNonQueryAsync();
    }
}
