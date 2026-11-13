using System.Text.RegularExpressions;

namespace LibreriaLaInmaculada.Frontend.ModelosDeVista;

/// <summary>
/// Reglas de formato que usa todo el sistema, en un solo lugar.
/// Las expresiones regulares se usan en los atributos [RegularExpression] de los modelos de vista:
/// el navegador las revisa antes de enviar y el servidor las vuelve a revisar al guardar.
/// </summary>
public static class Validaciones
{
    /// <summary>8 dígitos de Nicaragua: fijos empiezan con 2, celulares con 5, 7 u 8. Acepta 22660000, 2266-0000 o 2266 0000.</summary>
    public const string Telefono = @"^[2578][0-9]{3}[- ]?[0-9]{4}$";

    /// <summary>algo@dominio.ext (sin espacios, una sola arroba, con punto después de la arroba).</summary>
    public const string Correo = @"^[^@\s]+@[^@\s]+\.[A-Za-z]{2,}$";

    /// <summary>Solo letras (con tildes y ñ) y espacios.</summary>
    public const string NombrePersona = @"^[A-Za-zÁÉÍÓÚÜÑáéíóúüñ ]+$";

    /// <summary>Al menos una letra y al menos un número (el largo mínimo lo pone [StringLength]).</summary>
    public const string Contrasena = @"^(?=.*[A-Za-z])(?=.*[0-9]).+$";

    /// <summary>Número de factura del proveedor: letras, números y guiones. Ej.: GP-0029541.</summary>
    public const string NumFactura = @"^[A-Za-z0-9-]+$";

    // Límites de las compras
    public const int CantidadMaxima = 10000;
    public const decimal CostoMaximo = 100000m;
    public static readonly DateTime FechaMinima = new(2020, 1, 1);

    /// <summary>
    /// Quita espacios al inicio y al final, y deja un solo espacio entre palabras:
    /// "  Lápiz   HB " → "Lápiz HB". Así no se cuelan nombres repetidos que solo difieren en espacios.
    /// </summary>
    public static string? LimpiarTexto(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        return Regex.Replace(texto.Trim(), @"\s+", " ");
    }

    /// <summary>"22660000" o "2266 0000" → "2266-0000". Así todos los teléfonos se guardan igual.</summary>
    public static string? FormatearTelefono(string? telefono)
    {
        if (string.IsNullOrWhiteSpace(telefono)) return null;
        var digitos = new string(telefono.Where(char.IsDigit).ToArray());
        return digitos.Length == 8 ? $"{digitos[..4]}-{digitos[4..]}" : telefono.Trim();
    }
}
