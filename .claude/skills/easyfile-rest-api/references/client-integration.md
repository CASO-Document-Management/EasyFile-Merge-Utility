# EasyFile REST API — Client Integration Guide

Ready-to-use C# patterns for building applications that consume the EasyFile REST API.

---

## Project Setup

### Required NuGet Packages

```xml
<PackageReference Include="Newtonsoft.Json" Version="13.*" />
<PackageReference Include="Microsoft.Extensions.Http" Version="9.*" />
<!-- Optional: Polly for retry/circuit breaker -->
<PackageReference Include="Microsoft.Extensions.Http.Polly" Version="9.*" />
<!-- Optional: Serilog for logging -->
<PackageReference Include="Serilog" Version="3.*" />
<PackageReference Include="Serilog.Sinks.Console" Version="5.*" />
```

### Configuration (appsettings.json)

```json
{
  "EasyFileApi": {
    "BaseUrl": "https://host/spwsrest",
    "AuthBaseUrl": "https://host/spwsrestauth",
    "ApiKey": "your-api-key",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "ProfileKey": "EasyFile2",
    "ApiVersion": "v1"
  }
}
```

---

## TokenManager

Thread-safe token lifecycle manager using OAuth2 client credentials grant.

```csharp
public class TokenManager
{
    private readonly HttpClient _httpClient;
    private readonly HttpClient _authHttpClient;
    private readonly string _apiKey;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string _profileKey;
    private readonly string _apiVersion;
    private readonly object _lock = new object();

    private string _accessToken;
    private string _refreshToken;
    private DateTime _tokenExpiration;
    private string _currentUser;

    public bool IsAuthenticated =>
        !string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiration;

    /// <summary>
    /// Set this after LoginAsync() to identify the user for audit tracking.
    /// Client credentials grant has no user context — this is set manually.
    /// </summary>
    public string CurrentUser
    {
        get { lock (_lock) { return _currentUser; } }
        set { lock (_lock) { _currentUser = value; } }
    }

    public TokenManager(
        HttpClient httpClient,
        HttpClient authHttpClient,
        string apiKey,
        string clientId,
        string clientSecret,
        string profileKey,
        string apiVersion)
    {
        _httpClient = httpClient;
        _authHttpClient = authHttpClient;
        _apiKey = apiKey;
        _clientId = clientId;
        _clientSecret = clientSecret;
        _profileKey = profileKey;
        _apiVersion = apiVersion;

        _httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
    }

    /// <summary>
    /// Authenticate using client credentials. Call once at startup.
    /// </summary>
    public async Task<bool> LoginAsync()
    {
        var requestBody =
            $"grant_type=client_credentials" +
            $"&client_id={Uri.EscapeDataString(_clientId)}" +
            $"&client_secret={Uri.EscapeDataString(_clientSecret)}" +
            $"&profile_key={Uri.EscapeDataString(_profileKey)}";

        var content = new StringContent(requestBody, Encoding.UTF8, "text/plain");
        var response = await _authHttpClient.PostAsync(
            $"/api/{_apiVersion}/OAuth2/token", content);

        if (!response.IsSuccessStatusCode) return false;

        var json = await response.Content.ReadAsStringAsync();
        var token = JsonConvert.DeserializeObject<TokenResponse>(json);

        lock (_lock)
        {
            _accessToken = token.access_token;
            _refreshToken = token.refresh_token;
            _tokenExpiration = token.expires_in;
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _accessToken);
        }

        return true;
    }

    /// <summary>
    /// Call before every API request. Auto-refreshes if token expires within 5 minutes.
    /// </summary>
    public async Task EnsureValidTokenAsync()
    {
        bool needsRefresh;
        lock (_lock)
        {
            needsRefresh = DateTime.UtcNow.AddMinutes(5) >= _tokenExpiration;
        }

        if (needsRefresh)
            await RefreshTokenAsync();
    }

    public void Logout()
    {
        lock (_lock)
        {
            _accessToken = null;
            _refreshToken = null;
            _tokenExpiration = DateTime.MinValue;
            _currentUser = null;
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }

    private async Task RefreshTokenAsync()
    {
        string currentRefreshToken;
        lock (_lock) { currentRefreshToken = _refreshToken; }

        if (string.IsNullOrEmpty(currentRefreshToken))
            throw new Exception("No refresh token. Please login again.");

        var requestBody =
            $"grant_type=refresh_token" +
            $"&refresh_token={Uri.EscapeDataString(currentRefreshToken)}" +
            $"&client_id={Uri.EscapeDataString(_clientId)}" +
            $"&client_secret={Uri.EscapeDataString(_clientSecret)}";

        var content = new StringContent(requestBody, Encoding.UTF8, "text/plain");
        var response = await _authHttpClient.PostAsync(
            $"/api/{_apiVersion}/OAuth2/token", content);

        if (!response.IsSuccessStatusCode)
        {
            lock (_lock)
            {
                _accessToken = null;
                _refreshToken = null;
                _tokenExpiration = DateTime.MinValue;
                _httpClient.DefaultRequestHeaders.Authorization = null;
            }
            throw new Exception("Session expired. Please re-authenticate.");
        }

        var json = await response.Content.ReadAsStringAsync();
        var token = JsonConvert.DeserializeObject<TokenResponse>(json);

        lock (_lock)
        {
            _accessToken = token.access_token;
            _tokenExpiration = token.expires_in;
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _accessToken);
        }
    }
}

public class TokenResponse
{
    public string access_token { get; set; }
    public string refresh_token { get; set; }
    public DateTime expires_in { get; set; }
}
```

---

## API Response Model

```csharp
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public T Data { get; set; }
}
```

---

## Error Helper

```csharp
public static class ApiErrorHelper
{
    public static string ParseErrorResponse(
        HttpResponseMessage response, string errorContent)
    {
        try
        {
            dynamic error = JsonConvert.DeserializeObject(errorContent);
            return error?.message?.ToString()
                ?? $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
        }
        catch
        {
            return $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
        }
    }
}
```

---

## Service Layer Patterns

### Base Service Pattern

Every service follows this pattern:

```csharp
public class BaseEasyFileService
{
    protected readonly HttpClient _httpClient;
    protected readonly TokenManager _tokenManager;
    protected readonly string _apiVersion;

    public BaseEasyFileService(
        HttpClient httpClient, TokenManager tokenManager, string apiVersion)
    {
        _httpClient = httpClient;
        _tokenManager = tokenManager;
        _apiVersion = apiVersion;
    }

    /// <summary>
    /// Standard GET with automatic token refresh and error handling.
    /// </summary>
    protected async Task<ApiResponse<T>> GetAsync<T>(string endpoint)
    {
        await _tokenManager.EnsureValidTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        AddUserHeaders(request);

        var response = await _httpClient.SendAsync(request);
        return await HandleResponse<T>(response);
    }

    /// <summary>
    /// Standard POST with JSON body.
    /// </summary>
    protected async Task<ApiResponse<T>> PostJsonAsync<T>(
        string endpoint, object body)
    {
        await _tokenManager.EnsureValidTokenAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Content = new StringContent(
            JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
        AddUserHeaders(request);

        var response = await _httpClient.SendAsync(request);
        return await HandleResponse<T>(response);
    }

    protected void AddUserHeaders(HttpRequestMessage request)
    {
        if (!string.IsNullOrEmpty(_tokenManager.CurrentUser))
            request.Headers.Add("X-Logged-In-User", _tokenManager.CurrentUser);
    }

    protected async Task<ApiResponse<T>> HandleResponse<T>(
        HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var errorMsg = ApiErrorHelper.ParseErrorResponse(response, content);
            throw new Exception(errorMsg);
        }

        var result = JsonConvert.DeserializeObject<ApiResponse<T>>(content);
        if (!result.Success)
            throw new Exception(result.Message ?? "API returned failure");

        return result;
    }
}
```

### Document Service

```csharp
public class DocumentService : BaseEasyFileService
{
    public DocumentService(
        HttpClient httpClient, TokenManager tokenManager, string apiVersion)
        : base(httpClient, tokenManager, apiVersion) { }

    public async Task<ApiResponse<DocumentUploadResponse>> UploadAsync(
        string cabinet,
        string filePath,
        List<Attr> attributes = null,
        string comment = "",
        string easyFileBaseUrl = null)
    {
        await _tokenManager.EnsureValidTokenAsync();

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(cabinet), "cabinet");

        if (attributes != null)
            content.Add(new StringContent(
                JsonConvert.SerializeObject(attributes)), "attributes");

        content.Add(new StringContent(
            JsonConvert.SerializeObject(new[] { comment })), "fileComments");

        var fileBytes = File.ReadAllBytes(filePath);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType =
            MediaTypeHeaderValue.Parse("application/octet-stream");
        content.Add(fileContent, "files", Path.GetFileName(filePath));

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/{_apiVersion}/documents/upload");
        request.Content = content;
        AddUserHeaders(request);

        if (!string.IsNullOrEmpty(easyFileBaseUrl))
            request.Headers.Add("X-EasyFile-Base-Url", easyFileBaseUrl);

        var response = await _httpClient.SendAsync(request);
        return await HandleResponse<DocumentUploadResponse>(response);
    }

    public async Task<ApiResponse<DocumentResponseDetails>> GetInfoAsync(int docId)
    {
        return await GetAsync<DocumentResponseDetails>(
            $"/api/{_apiVersion}/documents/{docId}/info");
    }

    public async Task<Stream> DownloadAsync(int docId)
    {
        await _tokenManager.EnsureValidTokenAsync();
        var response = await _httpClient.GetAsync(
            $"/api/{_apiVersion}/documents/{docId}/download");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStreamAsync();
    }

    public async Task<ApiResponse<DocumentSasUrlResponse>> GetSasUrlAsync(
        int docId, int expiryMinutes = 60)
    {
        return await GetAsync<DocumentSasUrlResponse>(
            $"/api/{_apiVersion}/documents/{docId}/sas-url?expiryMinutes={expiryMinutes}");
    }
}
```

### Cabinet Service

```csharp
public class CabinetService : BaseEasyFileService
{
    public CabinetService(
        HttpClient httpClient, TokenManager tokenManager, string apiVersion)
        : base(httpClient, tokenManager, apiVersion) { }

    public async Task<ApiResponse<object>> GetCabinetsAsync(string userId)
    {
        return await GetAsync<object>(
            $"/api/{_apiVersion}/cabinets?userId={Uri.EscapeDataString(userId)}");
    }

    public async Task<ApiResponse<CabinetResponse>> GetCabinetAsync(string cabinetName)
    {
        return await GetAsync<CabinetResponse>(
            $"/api/{_apiVersion}/cabinets/{Uri.EscapeDataString(cabinetName)}");
    }

    public async Task<ApiResponse<DataResultResponse>> FetchDataAsync(
        string cabinetName, FetchCabinetDataRequest request)
    {
        return await PostJsonAsync<DataResultResponse>(
            $"/api/{_apiVersion}/cabinets/{Uri.EscapeDataString(cabinetName)}/data",
            request);
    }
}
```

### Basket Service

```csharp
public class BasketService : BaseEasyFileService
{
    public BasketService(
        HttpClient httpClient, TokenManager tokenManager, string apiVersion)
        : base(httpClient, tokenManager, apiVersion) { }

    public async Task<ApiResponse<BasketResponse>> GetByNameAsync(string name)
    {
        return await GetAsync<BasketResponse>(
            $"/api/{_apiVersion}/baskets/by-name/{Uri.EscapeDataString(name)}");
    }

    public async Task UploadToBasketAsync(
        string basketNo, string filePath, string comment = "")
    {
        await _tokenManager.EnsureValidTokenAsync();

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(
            JsonConvert.SerializeObject(new[] { comment })), "fileComments");

        var fileBytes = File.ReadAllBytes(filePath);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType =
            MediaTypeHeaderValue.Parse("application/octet-stream");
        content.Add(fileContent, "files", Path.GetFileName(filePath));

        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/{_apiVersion}/baskets/{basketNo}/documents");
        request.Content = content;
        AddUserHeaders(request);
        request.Headers.Add("X-Calling-App", "EasyFile");

        var response = await _httpClient.SendAsync(request);
        await HandleResponse<object>(response);
    }
}
```

### Lookup Service

```csharp
public class LookupService : BaseEasyFileService
{
    public LookupService(
        HttpClient httpClient, TokenManager tokenManager, string apiVersion)
        : base(httpClient, tokenManager, apiVersion) { }

    public async Task AddLookupDataAsync(
        string lookupTable, string lookupColumn, string value)
    {
        await PostJsonAsync<object>(
            $"/api/{_apiVersion}/lookup/tables/{Uri.EscapeDataString(lookupTable)}/data",
            new { lookupColumn, value });
    }
}
```

---

## Full Client Wiring Example

```csharp
// Program.cs or Startup
var config = builder.Configuration.GetSection("EasyFileApi");

var authClient = new HttpClient
{
    BaseAddress = new Uri(config["AuthBaseUrl"]!)
};

var apiClient = new HttpClient
{
    BaseAddress = new Uri(config["BaseUrl"]!)
};

var tokenManager = new TokenManager(
    apiClient, authClient,
    config["ApiKey"]!,
    config["ClientId"]!,
    config["ClientSecret"]!,
    config["ProfileKey"]!,
    config["ApiVersion"]!);

// Authenticate
bool authenticated = await tokenManager.LoginAsync();
if (!authenticated)
    throw new Exception("Failed to authenticate with EasyFile API");

// Set user context for audit
tokenManager.CurrentUser = "service-account";

// Create services
var documentService = new DocumentService(apiClient, tokenManager, config["ApiVersion"]!);
var cabinetService = new CabinetService(apiClient, tokenManager, config["ApiVersion"]!);
var basketService = new BasketService(apiClient, tokenManager, config["ApiVersion"]!);
var lookupService = new LookupService(apiClient, tokenManager, config["ApiVersion"]!);

// Use them
var cabinets = await cabinetService.GetCabinetsAsync("myuser");
var uploadResult = await documentService.UploadAsync(
    "FT_MYCABINET", @"C:\docs\report.pdf",
    new List<Attr> { new() { ColumnName = "Category", Value = "Reports" } },
    "Uploaded via integration");
```

---

## DI Registration Example

```csharp
// In Program.cs with dependency injection
builder.Services.AddSingleton<TokenManager>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>()
        .GetSection("EasyFileApi");
    var authClient = new HttpClient
        { BaseAddress = new Uri(config["AuthBaseUrl"]!) };
    var apiClient = new HttpClient
        { BaseAddress = new Uri(config["BaseUrl"]!) };

    return new TokenManager(
        apiClient, authClient,
        config["ApiKey"]!, config["ClientId"]!,
        config["ClientSecret"]!, config["ProfileKey"]!,
        config["ApiVersion"]!);
});

builder.Services.AddScoped<DocumentService>(sp =>
{
    var tm = sp.GetRequiredService<TokenManager>();
    var config = sp.GetRequiredService<IConfiguration>()
        .GetSection("EasyFileApi");
    return new DocumentService(tm._httpClient, tm, config["ApiVersion"]!);
});
```

---

## Checklist Before Making API Calls

1. Initialize `TokenManager` with API key, client credentials, and profile key
2. Call `LoginAsync()` once to authenticate
3. Set `TokenManager.CurrentUser` for audit tracking
4. Call `EnsureValidTokenAsync()` before each request (services do this internally)
5. Always include `X-Logged-In-User` header for endpoints that require it
6. Handle both HTTP-level errors and API-level errors (`success: false`)
7. Use `ApiErrorHelper.ParseErrorResponse()` for consistent error messages
8. For file uploads, use `MultipartFormDataContent` with proper content types
