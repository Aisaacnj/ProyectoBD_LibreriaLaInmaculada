using System.Data;
using Microsoft.Data.SqlClient;

namespace LibreriaLaInmaculada.Backend.AccesoDatos;

/// <summary>
/// Atajos para agregar parámetros a un SqlCommand con su tipo correcto.
/// Ejemplo: cmd.Texto("@Nombre", nombre, 100);
/// Un texto vacío o un número null se guardan como NULL en la base.
/// </summary>
public static class Parametros
{
    public static void Texto(this SqlCommand cmd, string nombre, string? valor, int tamano) =>
        cmd.Parameters.Add(nombre, SqlDbType.VarChar, tamano).Value =
            string.IsNullOrWhiteSpace(valor) ? DBNull.Value : (object)valor.Trim();

    public static void Entero(this SqlCommand cmd, string nombre, int? valor) =>
        cmd.Parameters.Add(nombre, SqlDbType.Int).Value =
            valor.HasValue ? (object)valor.Value : DBNull.Value;

    public static void Moneda(this SqlCommand cmd, string nombre, decimal valor)
    {
        var p = cmd.Parameters.Add(nombre, SqlDbType.Decimal);
        p.Precision = 10;
        p.Scale = 2;
        p.Value = valor;
    }

    public static void Bit(this SqlCommand cmd, string nombre, bool valor) =>
        cmd.Parameters.Add(nombre, SqlDbType.Bit).Value = valor;

    /// <summary>Lee un texto que puede venir NULL.</summary>
    public static string? TextoONulo(this SqlDataReader rd, int columna) =>
        rd.IsDBNull(columna) ? null : rd.GetString(columna);
}

/// <summary>Errores de SQL Server que el sistema reconoce.</summary>
public static class SqlErrores
{
    /// <summary>Se violó una restricción UNIQUE o PRIMARY KEY (nombre repetido).</summary>
    public static bool EsDuplicado(SqlException ex) => ex.Number is 2627 or 2601;
}
