using Microsoft.Extensions.Options;
using MergeUtility.Core.Models.Configuration;

namespace MergeUtility.Console;

internal sealed class DecryptEasyFileApiOptions : IConfigureOptions<EasyFileApiOptions>
{
    public void Configure(EasyFileApiOptions options)
    {
        options.ApiKey       = EncryptionHelper.Decrypt(options.ApiKey);
        options.ClientSecret = EncryptionHelper.Decrypt(options.ClientSecret);
    }
}
