namespace MergeUtility.Core.Interfaces;

public interface ITokenManager
{
    string CurrentUser { get; set; }
    string AccessToken { get; }
    bool IsAuthenticated { get; }
    DateTime TokenExpiry { get; }
    Task EnsureValidTokenAsync(CancellationToken ct = default);
    void Invalidate();
}
