using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MergeUtility.Core.Interfaces;
using MergeUtility.Core.Models.Configuration;

namespace MergeUtility.Console;

internal sealed class ConnectionTestWorker : BackgroundService
{
    private readonly ITokenManager _tokenManager;
    private readonly ICabinetService _cabinetService;
    private readonly MergeOptions _mergeOptions;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<ConnectionTestWorker> _logger;
    private readonly string[] _args;

    public ConnectionTestWorker(
        ITokenManager tokenManager,
        ICabinetService cabinetService,
        IOptions<MergeOptions> mergeOptions,
        IHostApplicationLifetime lifetime,
        ILogger<ConnectionTestWorker> logger)
    {
        _tokenManager = tokenManager;
        _cabinetService = cabinetService;
        _mergeOptions = mergeOptions.Value;
        _lifetime = lifetime;
        _logger = logger;
        _args = Environment.GetCommandLineArgs();
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

            if (_args.Any(a => a == "--test-search"))
            {
                await TestSearchAsync(stoppingToken);
            }
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

    private async Task TestSearchAsync(CancellationToken ct)
    {
        var cabinet = _mergeOptions.CabinetName;
        var field = _mergeOptions.SearchFieldName;

        // Derive test value from first matching file in SourceDirectory
        var sourceDir = _mergeOptions.SourceDirectory;
        var pattern = _mergeOptions.FilePattern;
        var regex = new Regex(_mergeOptions.IdentifierRegex);

        var firstFile = Directory.EnumerateFiles(sourceDir, pattern).FirstOrDefault();
        if (firstFile == null)
        {
            System.Console.WriteLine($"[SKIP] No files matching '{pattern}' in {sourceDir}");
            return;
        }

        var match = regex.Match(Path.GetFileNameWithoutExtension(firstFile));
        if (!match.Success)
        {
            System.Console.WriteLine($"[SKIP] File '{Path.GetFileName(firstFile)}' does not match IdentifierRegex");
            return;
        }

        var testValue = match.Groups[1].Value;

        System.Console.WriteLine();
        System.Console.WriteLine($"Testing search: cabinet={cabinet}, field=\"{field}\", value=\"{testValue}\"...");

        try
        {
            var result = await _cabinetService.SearchAsync(cabinet, field, testValue, ct);

            System.Console.WriteLine($"[OK] Search returned {result.Data.Count} row(s) (TotalCount={result.TotalCount})");

            foreach (var row in result.Data.Take(5))
            {
                System.Console.WriteLine($"     {row.ToString()[..Math.Min(row.ToString().Length, 200)]}");
            }

            _logger.LogInformation("Search test passed. Rows={Count} Total={Total}", result.Data.Count, result.TotalCount);
        }
        catch (HttpRequestException ex)
        {
            System.Console.WriteLine($"[FAIL] Search failed: {ex.Message}");
            System.Console.WriteLine($"       Status: {ex.StatusCode}");
            _logger.LogError(ex, "Search test failed");
            throw;
        }
    }
}
