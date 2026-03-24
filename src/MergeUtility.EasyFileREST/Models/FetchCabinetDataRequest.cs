namespace MergeUtility.EasyFileREST.Models;

public class FetchCabinetDataRequest
{
    public string UserId { get; set; } = string.Empty;
    public int Start { get; set; } = 0;
    public int Length { get; set; } = 100;
    public string SearchText { get; set; } = string.Empty;
    public string SearchKey { get; set; } = string.Empty;
    public string AdvanceSearch { get; set; } = string.Empty;
    public string OrderBy { get; set; } = string.Empty;
    public string OrderDir { get; set; } = "asc";
    public string Gateway { get; set; } = string.Empty;
    public string GatewayCriteria { get; set; } = string.Empty;
    public string FullTextSearchType { get; set; } = string.Empty;
    public string AppName { get; set; } = string.Empty;
    public string SearchOption { get; set; } = "{}";
}
