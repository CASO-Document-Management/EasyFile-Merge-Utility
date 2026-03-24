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

        // Return the response stream directly — caller is responsible for disposing.
        // The HttpResponseMessage is not disposed here because the stream is backed by the connection.
        return await response.Content.ReadAsStreamAsync(ct);
    }

    public async Task ReplaceAsync(int docId, string mergedFilePath, string comments, CancellationToken ct)
    {
        using var content = new MultipartFormDataContent();

        var fileStream = new FileStream(mergedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", Path.GetFileName(mergedFilePath));
        content.Add(new StringContent(_tokenManager.CurrentUser), "userId");
        content.Add(new StringContent(comments), "comments");

        var request = new HttpRequestMessage(HttpMethod.Put, $"api/{ApiVersion}/documents/{docId}/replace")
        {
            Content = content
        };

        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            var msg = ApiErrorHelper.ParseErrorResponse(response, body);
            throw new System.Net.Http.HttpRequestException(msg, null, response.StatusCode);
        }
    }
}
