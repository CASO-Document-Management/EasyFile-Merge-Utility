using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MergeUtility.Core.Interfaces;

namespace MergeUtility.Console;

internal sealed class ConnectionTestWorker : BackgroundService
{
    private readonly ITokenManager _tokenManager;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<ConnectionTestWorker> _logger;

    public ConnectionTestWorker(
        ITokenManager tokenManager,
        IHostApplicationLifetime lifetime,
        ILogger<ConnectionTestWorker> logger)
    {
        _tokenManager = tokenManager;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            System.Console.WriteLine("Testing connection to EasyFile...");
            await _tokenManager.EnsureValidTokenAsync(stoppingToken);

            System.Console.WriteLine("[OK] Authentication successful.");
            System.Console.WriteLine($"     User   : {_tokenManager.CurrentUser}");
            System.Console.WriteLine($"     Expires: {_tokenManager.TokenExpiry:u}");
            _logger.LogInformation("Connection test passed. User={User} Expiry={Expiry}",
                _tokenManager.CurrentUser, _tokenManager.TokenExpiry);
        }
        catch (HttpRequestException ex)
        {
            System.Console.WriteLine($"[FAIL] {ex.Message}");
            _logger.LogError(ex, "Connection test failed");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[FAIL] Unexpected error: {ex.Message}");
            _logger.LogError(ex, "Connection test unexpected error");
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }
}
