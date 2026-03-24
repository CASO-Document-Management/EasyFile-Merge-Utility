using System.Text.Json;

namespace MergeUtility.EasyFileREST;

public static class ApiErrorHelper
{
    public static string ParseErrorResponse(System.Net.Http.HttpResponseMessage response, string errorContent)
    {
        try
        {
            using var doc = JsonDocument.Parse(errorContent);
            if (doc.RootElement.TryGetProperty("message", out var msg))
                return msg.GetString() ?? $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
        }
        catch { }

        return $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
    }
}
