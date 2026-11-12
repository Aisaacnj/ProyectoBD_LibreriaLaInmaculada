using Microsoft.Data.SqlClient;

namespace LibreriaLaInmaculada.Backend.AccesoDatos;

/// <summary>
/// Abre conexiones a SQL Server con la cadena "Libreria" de appsettings.json.
/// Cada repositorio pide una conexión, la usa y la cierra (con "await using").
/// </summary>
public class Db
{
    private readonly string _cadena;

    public Db(IConfiguration configuracion)
    {
        _cadena = configuracion.GetConnectionString("Libreria")
            ?? throw new InvalidOperationException("Falta la cadena de conexión 'Libreria' en appsettings.json.");
    }

    public async Task<SqlConnection> AbrirAsync()
    {
        var conexion = new SqlConnection(_cadena);
        await conexion.OpenAsync();
        return conexion;
    }
}
