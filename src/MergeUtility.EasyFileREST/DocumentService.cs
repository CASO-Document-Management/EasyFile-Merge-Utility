using System.Net.Http.Headers;
using System.Text.Json;
using MergeUtility.Core.Interfaces;

namespace MergeUtility.EasyFileREST;

public class DocumentService : BaseEasyFileService, IDocumentSource
{
    public DocumentService(HttpClient httpClient, ITokenManager tokenManager)
        : base(httpClient, tokenManager) { }

    public async Task<Stream> DownloadAsync(int docId, CancellationToken ct)
    {
        var versionId = await ResolveLatestVersionIdAsync(docId, ct);

        var request = new HttpRequestMessage(HttpMethod.Get, $"api/{ApiVersion}/documents/{versionId}/download");

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

    public async Task ReplaceAsync(int docId, string mergedFilePath, string comments, CancellationToken ct)
    {
        // Replace uses DOC_ID directly (not version ID)
        using var content = new MultipartFormDataContent();

        var fileStream = new FileStream(mergedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "File", Path.GetFileName(mergedFilePath));
        content.Add(new StringContent(_tokenManager.CurrentUser), "UserId");
        content.Add(new StringContent(comments), "Comment");
        content.Add(new StringContent(Path.GetFileName(mergedFilePath)), "FileName");

        var request = new HttpRequestMessage(HttpMethod.Put, $"api/{ApiVersion}/documents/{docId}/replace")
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

    private async Task<int> ResolveLatestVersionIdAsync(int docId, CancellationToken ct)
    {
        var response = await GetAsync<List<JsonElement>>($"api/{ApiVersion}/documents/{docId}", ct);
        var versions = response.Data;

        if (versions == null || versions.Count == 0)
            throw new HttpRequestException($"No document versions found for DOC_ID {docId}");

        var latest = versions
            .OrderByDescending(v =>
                v.TryGetProperty("revisionNo", out var rev) &&
                int.TryParse(rev.GetString(), out var r) ? r : 0)
            .First();

        return int.Parse(latest.GetProperty("id").GetString()!);
    }
}
