using Microsoft.Extensions.Options;
using MergeUtility.EasyFileREST.Models;
using MergeUtility.Core.Interfaces;
using MergeUtility.Core.Models;
using MergeUtility.Core.Models.Configuration;

namespace MergeUtility.EasyFileREST;

public class CabinetService : BaseEasyFileService, ICabinetService
{
    private readonly EasyFileApiOptions _apiOptions;

    public CabinetService(HttpClient httpClient, ITokenManager tokenManager, IOptions<EasyFileApiOptions> options)
        : base(httpClient, tokenManager)
    {
        _apiOptions = options.Value;
    }

    public async Task<DataResultResponse> SearchAsync(
        string cabinetName, string fieldName, string value, CancellationToken ct)
    {
        var request = new FetchCabinetDataRequest
        {
            UserId = _tokenManager.CurrentUser,
            Start = 0,
            Length = 200,
            SearchKey = fieldName,
            SearchText = value
        };

        var response = await PostJsonAsync<DataResultResponse>(
            $"api/{ApiVersion}/cabinets/{Uri.EscapeDataString(cabinetName)}/data",
            request,
            ct);

        return response.Data ?? new DataResultResponse();
    }
}
