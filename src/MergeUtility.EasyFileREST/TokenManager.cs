using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MergeUtility.EasyFileREST.Models;
using MergeUtility.Core.Interfaces;
using MergeUtility.Core.Models.Configuration;

namespace MergeUtility.EasyFileREST;

public class TokenManager : ITokenManager
{
    private readonly HttpClient _authHttpClient;
    private readonly EasyFileApiOptions _options;
    private readonly ILogger<TokenManager> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly Lock _stateLock = new();

    private string? _accessToken;
    private string? _refreshToken;
    private DateTime _tokenExpiration = DateTime.MinValue;
    private string _currentUser = string.Empty;

    public string AccessToken
    {
        get { lock (_stateLock) { return _accessToken ?? string.Empty; } }
    }

    public string CurrentUser
    {
        get { lock (_stateLock) { return _currentUser; } }
        set { lock (_stateLock) { _currentUser = value; } }
    }

    public bool IsAuthenticated
    {
        get { lock (_stateLock) { return !string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiration; } }
    }

    public DateTime TokenExpiry
    {
        get { lock (_stateLock) { return _tokenExpiration; } }
    }

    public TokenManager(HttpClient authHttpClient, IOptions<EasyFileApiOptions> options, ILogger<TokenManager> logger)
    {
        _authHttpClient = authHttpClient;
        _options = options.Value;
        _logger = logger;
        _currentUser = _options.LoggedInUser;
    }

    public async Task EnsureValidTokenAsync(CancellationToken ct = default)
    {
        bool needsRefresh;
        lock (_stateLock)
        {
            needsRefresh = DateTime.UtcNow.AddMinutes(5) >= _tokenExpiration;
        }

        if (!needsRefresh) return;

        await _semaphore.WaitAsync(ct);
        try
        {
            // Double-check after acquiring semaphore
            lock (_stateLock)
            {
                if (DateTime.UtcNow.AddMinutes(5) < _tokenExpiration) return;
            }

            await LoginAsync(ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Invalidate()
    {
        lock (_stateLock)
        {
            _tokenExpiration = DateTime.MinValue;
        }
    }

    private async Task LoginAsync(CancellationToken ct)
    {
        _logger.LogDebug("Refreshing EasyFile access token");

        var body = $"grant_type=client_credentials" +
                   $"&client_id={Uri.EscapeDataString(_options.ClientId)}" +
                   $"&client_secret={Uri.EscapeDataString(_options.ClientSecret)}" +
                   $"&profile_key={Uri.EscapeDataString(_options.ProfileKey)}";

        // EasyFile's OAuth2 endpoint requires text/plain (non-standard; rejects application/x-www-form-urlencoded)
        var content = new StringContent(body, Encoding.UTF8, "text/plain");
        var response = await _authHttpClient.PostAsync(
            "api/v1/OAuth2/token", content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Authentication failed: HTTP {(int)response.StatusCode} — {error}",
                null, response.StatusCode);
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var token = JsonSerializer.Deserialize<TokenResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to deserialize token response");

        lock (_stateLock)
        {
            _accessToken = token.access_token;
            _refreshToken = token.refresh_token;
            _tokenExpiration = token.expires_in;
        }

        _logger.LogDebug("Token acquired, expires at {Expiry}", token.expires_in);
    }
}
