using System.Data;
using LibreriaLaInmaculada.Backend.Modelos;
using Microsoft.Data.SqlClient;

namespace LibreriaLaInmaculada.Backend.AccesoDatos;

public class UsuarioRepositorio
{
    private readonly Db _db;

    public UsuarioRepositorio(Db db) => _db = db;

    /// <summary>Busca un usuario por su nombre de inicio de sesión. Devuelve null si no existe.</summary>
    public async Task<Usuario?> ObtenerPorNombreAsync(string nombreUsuario)
    {
        const string sql = @"
            SELECT idUsuario, Usuario, Nombre_Completo, Password, Rol, Activo, Fecha_Registro
            FROM dbo.Usuarios
            WHERE Usuario = @Usuario;";

        await using var cn = await _db.AbrirAsync();
        await using var cmd = new SqlCommand(sql, cn);
        cmd.Parameters.Add("@Usuario", SqlDbType.VarChar, 50).Value = nombreUsuario;

        await using var rd = await cmd.ExecuteReaderAsync();
        if (!await rd.ReadAsync()) return null;

        return new Usuario
        {
            IdUsuario      = rd.GetInt32(0),
            NombreUsuario  = rd.GetString(1),
            NombreCompleto = rd.IsDBNull(2) ? null : rd.GetString(2),
            Password       = rd.GetString(3),
            Rol            = rd.GetString(4),
            Activo         = rd.GetBoolean(5),
            FechaRegistro  = rd.GetDateTime(6)
        };
    }
}
