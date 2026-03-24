using MergeUtility.Core.Models;

namespace MergeUtility.Core.Interfaces;

public interface ICabinetService
{
    Task<DataResultResponse> SearchAsync(string cabinetName, string fieldName, string value, CancellationToken ct);
}
