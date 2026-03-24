using System.Text.Json;

namespace MergeUtility.Core.Models;

public class DataResultResponse
{
    public List<JsonElement> Data { get; set; } = [];
    public int TotalCount { get; set; }
}
