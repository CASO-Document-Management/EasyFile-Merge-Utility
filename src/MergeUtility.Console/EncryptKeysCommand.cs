using System.Text.Json;
using System.Text.Json.Nodes;

namespace MergeUtility.Console;

internal static class EncryptKeysCommand
{
    public static void Run()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var section = root["EasyFileApi"]!.AsObject();

        foreach (var key in new[] { "ApiKey", "ClientSecret" })
        {
            var value = section[key]?.GetValue<string>() ?? string.Empty;
            if (string.IsNullOrEmpty(value))         { System.Console.WriteLine($"{key}: empty, skipping"); continue; }
            if (EncryptionHelper.IsEncrypted(value)) { System.Console.WriteLine($"{key}: already encrypted, skipping"); continue; }
            section[key] = EncryptionHelper.Encrypt(value);
            System.Console.WriteLine($"{key}: encrypted");
        }

        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        System.Console.WriteLine("appsettings.json updated.");
    }
}
