using System.Security.Cryptography;
using System.Text;

namespace MusicStore.Infrastructure.Utils;

internal static class DeterministicHash
{
    public static ulong ToUInt64(params string[] parts)
    {
        var joined = string.Join("|", parts);
        var bytes = Encoding.UTF8.GetBytes(joined);
        var hash = SHA256.HashData(bytes);

        return BitConverter.ToUInt64(hash, 0);
    }

    public static ulong Mix(ulong a, ulong b)
    {
        ulong x = a ^ (b + 0x9E3779B97F4A7C15UL + (a << 6) + (a >> 2));
        x ^= x >> 33;
        x *= 0xff51afd7ed558ccdUL;
        x ^= x >> 33;
        x *= 0xc4ceb9fe1a85ec53UL;
        x ^= x >> 33;

        return x;
    }
}