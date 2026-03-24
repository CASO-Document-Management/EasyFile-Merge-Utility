using System.Net.Http.Headers;
using MergeUtility.Core.Interfaces;

namespace MergeUtility.EasyFileREST;

public class DocumentService : BaseEasyFileService, IDocumentSource
{
    public DocumentService(HttpClient httpClient, ITokenManager tokenManager)
        : base(httpClient, tokenManager) { }

    public async Task<Stream> DownloadAsync(int docId, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"api/{ApiVersion}/documents/{docId}/download");

        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            var msg = ApiErrorHelper.ParseErrorResponse(response, body);
            response.Dispose();
            throw new HttpRequestException(msg, null, response.StatusCode);
        }

        var stream = await response.Content.ReadAsStreamAsync(ct);
        return new ResponseOwningStream(stream, response);
    }

    public async Task CheckoutAsync(int docId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/{ApiVersion}/documents/{docId}/checkout");
        var response = await _httpClient.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            var msg = ApiErrorHelper.ParseErrorResponse(response, body);
            throw new HttpRequestException(msg, null, response.StatusCode);
        }
    }

    public async Task CheckinAsync(int docId, string mergedFilePath, string comments, CancellationToken ct)
    {
        using var content = new MultipartFormDataContent();

        var fileStream = new FileStream(mergedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "File", Path.GetFileName(mergedFilePath));
        content.Add(new StringContent(_tokenManager.CurrentUser), "UserId");
        content.Add(new StringContent(comments), "Comment");
        content.Add(new StringContent("true"), "CreateNewVersion");

        var request = new HttpRequestMessage(HttpMethod.Post, $"api/{ApiVersion}/documents/{docId}/checkin")
        {
            Content = content
        };

        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            var msg = ApiErrorHelper.ParseErrorResponse(response, body);
            throw new HttpRequestException(msg, null, response.StatusCode);
        }
    }

    public async Task UndoCheckoutAsync(int docId, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/{ApiVersion}/documents/{docId}/undo-checkout");
        var response = await _httpClient.SendAsync(request, ct);

        // Best-effort — don't throw on failure during cleanup
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            var msg = ApiErrorHelper.ParseErrorResponse(response, body);
            System.Diagnostics.Debug.WriteLine($"UndoCheckout failed for doc {docId}: {msg}");
        }
    }
}
