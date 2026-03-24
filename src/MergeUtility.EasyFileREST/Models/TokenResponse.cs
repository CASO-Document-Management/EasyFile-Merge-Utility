namespace MergeUtility.EasyFileREST.Models;

public class TokenResponse
{
    public string access_token { get; set; } = string.Empty;
    public string? refresh_token { get; set; }
    public DateTime expires_in { get; set; }
}
