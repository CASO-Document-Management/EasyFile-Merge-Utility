using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using MergeUtility.Core.Interfaces;
using MergeUtility.Core.Models.Configuration;

namespace MergeUtility.EasyFileREST;

public class AuthorizationDelegatingHandler : DelegatingHandler
{
    private readonly ITokenManager _tokenManager;
    private readonly EasyFileApiOptions _options;

    public AuthorizationDelegatingHandler(ITokenManager tokenManager, IOptions<EasyFileApiOptions> options)
    {
        _tokenManager = tokenManager;
        _options = options.Value;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await _tokenManager.EnsureValidTokenAsync(cancellationToken);
        ApplyHeaders(request);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _tokenManager.Invalidate();
            await _tokenManager.EnsureValidTokenAsync(cancellationToken);

            // Do not retry multipart/stream requests — content is already consumed and not replayable
            if (request.Content is MultipartContent)
                return response;

            // Clone the request — HttpRequestMessage cannot be sent twice
            var retryRequest = await CloneRequestAsync(request);
            ApplyHeaders(retryRequest);
            response.Dispose();
            response = await base.SendAsync(retryRequest, cancellationToken);
        }

        return response;
    }

    private void ApplyHeaders(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenManager.AccessToken);

        if (!string.IsNullOrEmpty(_tokenManager.CurrentUser))
            request.Headers.TryAddWithoutValidation("X-Logged-In-User", _tokenManager.CurrentUser);

        if (!string.IsNullOrEmpty(_options.CallingApp))
            request.Headers.TryAddWithoutValidation("X-Calling-App", _options.CallingApp);
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);

        foreach (var header in original.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (original.Content != null)
        {
            var bytes = await original.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(bytes);

            foreach (var header in original.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
