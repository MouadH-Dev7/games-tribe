using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SilksongArabicLauncher.Core;

internal static class TransactionIntegrity
{
    public static void Seal(TransactionManifest manifest)
    {
        manifest.IntegritySha256 = "";
        manifest.IntegritySha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(manifest, JsonDefaults.Options));
    }

    public static bool Verify(TransactionManifest manifest)
    {
        if (!SafePaths.IsSha256(manifest.IntegritySha256)) return false;
        var expected = manifest.IntegritySha256;
        manifest.IntegritySha256 = "";
        var actual = Hash(JsonSerializer.SerializeToUtf8Bytes(manifest, JsonDefaults.Options));
        manifest.IntegritySha256 = expected;
        return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expected), Convert.FromHexString(actual));
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
