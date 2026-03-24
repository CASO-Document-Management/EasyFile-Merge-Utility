namespace MergeUtility.Core.Models.Configuration;

public class EasyFileApiOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string AuthBaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string ProfileKey { get; set; } = string.Empty;
    public string LoggedInUser { get; set; } = string.Empty;
    public string CallingApp { get; set; } = string.Empty;
}
