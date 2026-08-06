using System.Security.Cryptography;

namespace LogiApp.Servicios;

public static class SeguridadPassword
{
    private const int Iteraciones = 210_000;
    private const int LongitudSalt = 16;
    private const int LongitudHash = 32;
    private const string Prefijo = "PBKDF2";

    public static string CrearHash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(LongitudSalt);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iteraciones, HashAlgorithmName.SHA256, LongitudHash);
        return $"{Prefijo}${Iteraciones}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool EsHash(string valor) => valor?.StartsWith(Prefijo + "$", StringComparison.Ordinal) == true;

    public static bool Verificar(string password, string valorGuardado)
    {
        if (!EsHash(valorGuardado))
            return CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(password),
                System.Text.Encoding.UTF8.GetBytes(valorGuardado ?? string.Empty));

        string[] partes = valorGuardado.Split('$');
        if (partes.Length != 4 || !int.TryParse(partes[1], out int iteraciones)) return false;

        try
        {
            byte[] salt = Convert.FromBase64String(partes[2]);
            byte[] esperado = Convert.FromBase64String(partes[3]);
            byte[] calculado = Rfc2898DeriveBytes.Pbkdf2(password, salt, iteraciones, HashAlgorithmName.SHA256, esperado.Length);
            return CryptographicOperations.FixedTimeEquals(calculado, esperado);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
