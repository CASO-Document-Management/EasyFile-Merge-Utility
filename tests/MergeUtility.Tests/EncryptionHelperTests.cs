using System.Runtime.Versioning;
using FluentAssertions;
using MergeUtility.Console;

namespace MergeUtility.Tests;

[SupportedOSPlatform("windows")]
public class EncryptionHelperTests
{
    [Fact]
    public void Encrypt_ProducesEncPrefix()
    {
        var result = EncryptionHelper.Encrypt("secret");
        result.Should().StartWith("ENC:");
    }

    [Fact]
    public void Roundtrip_ReturnsOriginalPlaintext()
    {
        var original = "my-secret-value";
        var decrypted = EncryptionHelper.Decrypt(EncryptionHelper.Encrypt(original));
        decrypted.Should().Be(original);
    }

    [Fact]
    public void IsEncrypted_ReturnsTrueForEncryptedValue()
    {
        // Callers should check IsEncrypted before calling Encrypt to avoid double-encoding.
        // Passing an already-encrypted value to Encrypt again produces a double-encoded result,
        // which EncryptKeysCommand guards against via this check.
        var encrypted = EncryptionHelper.Encrypt("x");
        EncryptionHelper.IsEncrypted(encrypted).Should().BeTrue();
    }
}
