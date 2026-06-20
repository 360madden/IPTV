using System.Security.Cryptography;
using System.Text;

namespace Iptv.Core;

public static class StableId
{
    public static Guid Create(params string?[] parts)
    {
        string value = string.Join('\u001f', parts.Select(part => part ?? string.Empty));
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        return new Guid(bytes);
    }
}
