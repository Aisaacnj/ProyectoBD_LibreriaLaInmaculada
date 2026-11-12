using System.Security.Claims;

namespace LibreriaLaInmaculada.Backend.Seguridad;

/// <summary>Atajos para leer los datos del usuario que inició sesión (User en controladores y vistas).</summary>
public static class UsuarioActual
{
    public const string ClaimNombreCompleto = "NombreCompleto";

    public static int IdUsuario(this ClaimsPrincipal usuario) =>
        int.Parse(usuario.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    public static string NombreCompleto(this ClaimsPrincipal usuario) =>
        usuario.FindFirstValue(ClaimNombreCompleto) ?? usuario.Identity?.Name ?? "";

    public static string Rol(this ClaimsPrincipal usuario) =>
        usuario.FindFirstValue(ClaimTypes.Role) ?? "";

    public static bool EsAdministrador(this ClaimsPrincipal usuario) =>
        usuario.IsInRole(Roles.Administrador);

    /// <summary>Iniciales para el círculo del menú: "Jorge Porras" → "JP".</summary>
    public static string Iniciales(this ClaimsPrincipal usuario)
    {
        var palabras = usuario.NombreCompleto().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (palabras.Length == 0) return "?";
        if (palabras.Length == 1) return palabras[0].Substring(0, Math.Min(2, palabras[0].Length)).ToUpper();
        return $"{palabras[0][0]}{palabras[1][0]}".ToUpper();
    }
}
