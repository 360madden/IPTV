using System.Security.Cryptography;
using System.Text;

namespace Iptv.App.ViewModels;

internal static class ParentalPinService
{
    public static string? NormalizePin(string? pin)
    {
        if (string.IsNullOrWhiteSpace(pin))
        {
            return null;
        }

        string normalized = pin.Trim();
        return normalized.Length is >= 4 and <= 12 && normalized.All(char.IsDigit)
            ? normalized
            : null;
    }

    public static string CreateSalt()
    {
        Span<byte> salt = stackalloc byte[16];
        RandomNumberGenerator.Fill(salt);
        return Convert.ToBase64String(salt);
    }

    public static string Hash(string salt, string pin)
    {
        byte[] bytes = Encoding.UTF8.GetBytes($"{salt}:{pin}");
        return Convert.ToBase64String(SHA256.HashData(bytes));
    }

    public static bool Verify(string? salt, string? expectedHash, string pin)
    {
        if (salt is null || expectedHash is null)
        {
            return false;
        }

        try
        {
            byte[] expected = Convert.FromBase64String(expectedHash);
            byte[] actual = Convert.FromBase64String(Hash(salt, pin));
            return expected.Length == actual.Length && CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
