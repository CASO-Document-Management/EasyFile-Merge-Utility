using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MergeUtility.Core.Interfaces;
using MergeUtility.Core.Models;

namespace MergeUtility.EasyFileREST;

public abstract class BaseEasyFileService
{
    protected const string ApiVersion = "v1";

    protected readonly HttpClient _httpClient;
    protected readonly ITokenManager _tokenManager;

    private static readonly JsonSerializerOptions _jsonOptions = new();

    private static readonly JsonSerializerOptions _deserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    protected BaseEasyFileService(HttpClient httpClient, ITokenManager tokenManager)
    {
        _httpClient = httpClient;
        _tokenManager = tokenManager;
    }

    protected async Task<ApiResponse<T>> GetAsync<T>(string endpoint, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        var response = await _httpClient.SendAsync(request, ct);
        return await HandleResponse<T>(response);
    }

    protected async Task<ApiResponse<T>> PostJsonAsync<T>(string endpoint, object body, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Content = new StringContent(
            JsonSerializer.Serialize(body, _jsonOptions), Encoding.UTF8, "application/json");
        var response = await _httpClient.SendAsync(request, ct);
        return await HandleResponse<T>(response);
    }

    protected async Task<ApiResponse<T>> HandleResponse<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var errorMsg = ApiErrorHelper.ParseErrorResponse(response, content);
            throw new HttpRequestException(errorMsg, null, response.StatusCode);
        }

        var result = JsonSerializer.Deserialize<ApiResponse<T>>(content, _deserializeOptions);

        if (result == null)
            throw new InvalidOperationException("Failed to deserialize API response");

        if (!result.Success)
            throw new HttpRequestException(result.Message ?? "API returned failure");

        return result;
    }
}
