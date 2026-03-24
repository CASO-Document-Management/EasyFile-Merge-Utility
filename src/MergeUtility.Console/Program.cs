using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MergeUtility.EasyFileREST;
using MergeUtility.Console;
using MergeUtility.Core.Interfaces;
using MergeUtility.Core.Models.Configuration;
using MergeUtility.Core.Services;

if (args.Contains("--encrypt-keys"))
{
    EncryptKeysCommand.Run();
    return;
}

bool isConnectionTest = args.Contains("--test-connection");
bool isSearchTest = args.Contains("--test-search");

var hostArgs = args.Where(a => !a.StartsWith("--test-connection") && !a.StartsWith("--encrypt-keys") && !a.StartsWith("--test-search")).ToArray();

var host = Host.CreateDefaultBuilder(hostArgs)
    .UseContentRoot(AppContext.BaseDirectory)
    .ConfigureLogging((ctx, logging) =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
        logging.AddFilter("Microsoft", LogLevel.Warning);
        logging.AddFilter("System", LogLevel.Warning);

        var workDir = ctx.Configuration["MergeOptions:WorkingDirectory"];
        if (!string.IsNullOrEmpty(workDir))
        {
            var logDir = Path.Combine(workDir, "logs");
            Directory.CreateDirectory(logDir);
            logging.AddProvider(new FileLoggerProvider(logDir));
        }
    })
    .ConfigureServices((ctx, services) =>
    {
        // Configuration binding
        services.Configure<EasyFileApiOptions>(ctx.Configuration.GetSection("EasyFileApi"));
        services.Configure<MergeOptions>(ctx.Configuration.GetSection("MergeOptions"));
        services.AddSingleton<IConfigureOptions<EasyFileApiOptions>, DecryptEasyFileApiOptions>();

        // Core services
        services.AddSingleton<IIdentifierExtractor, RegexIdentifierExtractor>();
        services.AddSingleton<IFileScanner, FileScanner>();
        services.AddSingleton<IPdfMergeService, PdfMergeService>();
        services.AddSingleton<IReportGenerator, CsvReportGenerator>();
        services.AddSingleton<ProcessedFileLog>();
        services.AddSingleton<IMergeProcessor, MergeProcessor>();
        services.AddSingleton<IMergeOrchestrator, MergeOrchestrator>();

        // API services
        services.AddSingleton<ITokenManager, TokenManager>();
        services.AddTransient<AuthorizationDelegatingHandler>();

        // Auth HTTP client — no auth handler on this one, it IS the auth endpoint
        services.AddHttpClient<ITokenManager, TokenManager>(c =>
        {
            c.BaseAddress = new Uri(ctx.Configuration["EasyFileApi:AuthBaseUrl"]!.TrimEnd('/') + "/");
        });

        // Document HTTP client with resilience (extended timeout for large file transfers)
        services.AddHttpClient<IDocumentSource, DocumentService>(c =>
        {
            c.BaseAddress = new Uri(ctx.Configuration["EasyFileApi:BaseUrl"]!.TrimEnd('/') + "/");
            c.DefaultRequestHeaders.Add("X-API-Key",
                EncryptionHelper.Decrypt(ctx.Configuration["EasyFileApi:ApiKey"]!));
        })
        .AddHttpMessageHandler<AuthorizationDelegatingHandler>()
        .AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.Delay = TimeSpan.FromSeconds(2);
            options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
            options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
            options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(3);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(7);
        });

        // Cabinet HTTP client with resilience
        services.AddHttpClient<ICabinetService, CabinetService>(c =>
        {
            c.BaseAddress = new Uri(ctx.Configuration["EasyFileApi:BaseUrl"]!.TrimEnd('/') + "/");
            c.DefaultRequestHeaders.Add("X-API-Key",
                EncryptionHelper.Decrypt(ctx.Configuration["EasyFileApi:ApiKey"]!));
        })
        .AddHttpMessageHandler<AuthorizationDelegatingHandler>()
        .AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.Delay = TimeSpan.FromSeconds(2);
            options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
        });

        if (isConnectionTest || isSearchTest)
            services.AddHostedService<ConnectionTestWorker>();
        else
            services.AddHostedService<MergeWorker>();
    })
    .Build();

await host.RunAsync();
