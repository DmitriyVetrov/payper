using DotNetEnv;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ReceiptBot.Configuration;
using ReceiptBot.Persistence;
using ReceiptBot.Services;
using ReceiptBot.Storage;

// Load .env if present (LOCAL dev)
Env.TraversePath().Load();

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices((ctx, services) =>
{
    // Options from env / appsettings
    services.Configure<BotOptions>(o =>
    {
        o.TelegramBotToken = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN") ?? string.Empty;
    });
    services.Configure<AzureDocIntelOptions>(o =>
    {
        o.Endpoint = Environment.GetEnvironmentVariable("AZURE_DI_ENDPOINT") ?? "";
        o.ApiKey   = Environment.GetEnvironmentVariable("AZURE_DI_KEY") ?? "";
        o.ModelId  = string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AZURE_DI_MODEL"))
                     ? "prebuilt-receipt"
                     : Environment.GetEnvironmentVariable("AZURE_DI_MODEL")!;
    });

    // Infra
    services.AddSingleton<AzureDocIntelligenceClientFactory>();
    services.AddSingleton<IFileStorage, LocalTempFileStorage>();

    // Domain
    services.AddSingleton<IReceiptRepository, InMemoryReceiptRepository>();
    services.AddSingleton<ReceiptProcessor>();
    services.AddSingleton<ReceiptFormatter>();

    // Background poller
    services.AddHostedService<TelegramPollingService>();
});

await builder.Build().RunAsync();
