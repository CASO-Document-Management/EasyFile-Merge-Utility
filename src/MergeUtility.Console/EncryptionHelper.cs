using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace MergeUtility.Console;

[SupportedOSPlatform("windows")]
internal static class EncryptionHelper
{
    private const string Prefix = "ENC:";
    private static readonly byte[] Entropy = "CASO.MergeUtility.v1"u8.ToArray();

    public static string Encrypt(string plaintext)
    {
        var bytes = Encoding.UTF8.GetBytes(plaintext);
        var cipher = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.LocalMachine);
        return Prefix + Convert.ToBase64String(cipher);
    }

    public static string Decrypt(string value)
    {
        if (!IsEncrypted(value)) return value;
        var cipher = Convert.FromBase64String(value[Prefix.Length..]);
        var plain = ProtectedData.Unprotect(cipher, Entropy, DataProtectionScope.LocalMachine);
        return Encoding.UTF8.GetString(plain);
    }

    public static bool IsEncrypted(string value) =>
        value.StartsWith(Prefix, StringComparison.Ordinal);
}
