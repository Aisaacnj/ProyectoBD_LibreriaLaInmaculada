using System.Security.Cryptography;

namespace LibreriaLaInmaculada.Backend.Seguridad;

/// <summary>
/// Hash de contraseñas con PBKDF2-SHA256.
/// Formato guardado en Usuarios.Password:  PBKDF2-SHA256$iteraciones$salt_base64$hash_base64
/// Es el mismo formato que usa el script de datos, así que ADMIN y jporras pueden entrar.
/// </summary>
public static class PasswordHasher
{
    private const string Prefijo = "PBKDF2-SHA256";
    private const int Iteraciones = 100_000;
    private const int TamanoSalt = 16;
    private const int TamanoHash = 32;

    /// <summary>Genera el hash para guardar una contraseña nueva.</summary>
    public static string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(TamanoSalt);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iteraciones, HashAlgorithmName.SHA256, TamanoHash);
        return $"{Prefijo}${Iteraciones}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>Comprueba si la contraseña escrita corresponde al hash guardado.</summary>
    public static bool Verificar(string password, string guardado)
    {
        string[] partes = guardado.Split('$');
        if (partes.Length != 4 || partes[0] != Prefijo || !int.TryParse(partes[1], out int iteraciones))
            return false;

        byte[] salt, esperado;
        try
        {
            salt = Convert.FromBase64String(partes[2]);
            esperado = Convert.FromBase64String(partes[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        byte[] calculado = Rfc2898DeriveBytes.Pbkdf2(password, salt, iteraciones, HashAlgorithmName.SHA256, esperado.Length);
        return CryptographicOperations.FixedTimeEquals(calculado, esperado);
    }
}
