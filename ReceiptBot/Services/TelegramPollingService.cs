using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReceiptBot.Configuration;
using ReceiptBot.Persistence;
using ReceiptBot.Services;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ReceiptBot.Services;

/// <summary>BackgroundService that connects to Telegram and handles updates.</summary>
public sealed class TelegramPollingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly BotOptions _botOptions;
    private readonly ILogger<TelegramPollingService> _log;

    private ITelegramBotClient _bot = default!;

    public TelegramPollingService(
        IServiceProvider serviceProvider,
        IOptions<BotOptions> botOptions,
        ILogger<TelegramPollingService> log)
    {
        _serviceProvider = serviceProvider;
        _botOptions = botOptions.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_botOptions.TelegramBotToken))
            throw new InvalidOperationException("TELEGRAM_BOT_TOKEN is not set.");

        _bot = new TelegramBotClient(_botOptions.TelegramBotToken);

        var me = await _bot.GetMe(stoppingToken);
        _log.LogInformation("Telegram bot @{Username} is running...", me.Username);

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        _bot.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            stoppingToken);
    }

    private Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, CancellationToken ct)
    {
        _log.LogError(ex, "Telegram polling error");
        return Task.CompletedTask;
    }

    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        var msg = update.Message;
        if (msg is null) return;

        if (msg.Text is { } text)
        {
            var command = text.Trim().ToLowerInvariant();
            
            if (command.StartsWith("/start"))
            {
                await bot.SendMessage(
                    chatId: msg.Chat,
                    text: "👋 Send me a photo or PDF of a receipt to analyze.\n\n" +
                          "Commands:\n" +
                          "/expenses - Show total expenses this month\n" +
                          "/merchants - Show expenses by merchant\n" +
                          "/categories - Show expenses by category",
                    cancellationToken: ct);
                return;
            }

            if (command.StartsWith("/expenses"))
            {
                using var scope = _serviceProvider.CreateScope();
                var analysisService = scope.ServiceProvider.GetRequiredService<ExpenseAnalysisService>();
                
                var startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var total = await analysisService.GetTotalExpensesAsync(startOfMonth, ct: ct);
                var totalFormatted = $"€{total:F2}";
                
                await bot.SendMessage(msg.Chat, $"💰 Total expenses this month: {totalFormatted}", cancellationToken: ct);
                return;
            }

            if (command.StartsWith("/merchants"))
            {
                using var scope = _serviceProvider.CreateScope();
                var analysisService = scope.ServiceProvider.GetRequiredService<ExpenseAnalysisService>();
                
                var startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var merchants = await analysisService.GetExpensesByMerchantAsync(startOfMonth, ct: ct);
                
                if (merchants.Any())
                {
                    var response = "🏪 Expenses by Merchant this month:\n\n" +
                                  string.Join("\n", merchants
                                      .OrderByDescending(kvp => kvp.Value)
                                      .Take(10)
                                      .Select(kvp => $"{kvp.Key}: {kvp.Value:C}"));
                    
                    await bot.SendMessage(msg.Chat, response, cancellationToken: ct);
                }
                else
                {
                    await bot.SendMessage(msg.Chat, "📊 No expenses found for this month.", cancellationToken: ct);
                }
                return;
            }

            if (command.StartsWith("/categories"))
            {
                using var scope = _serviceProvider.CreateScope();
                var analysisService = scope.ServiceProvider.GetRequiredService<ExpenseAnalysisService>();
                
                var startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                var categories = await analysisService.GetExpensesByCategoryAsync(startOfMonth, ct: ct);
                
                if (categories.Any())
                {
                    var response = "📊 Expenses by category this month:\n\n" +
                                  string.Join("\n", categories
                                      .OrderByDescending(kvp => kvp.Value)
                                      .Select(kvp => $"{kvp.Key}: {kvp.Value:C}"));
                    
                    await bot.SendMessage(msg.Chat, response, cancellationToken: ct);
                }
                else
                {
                    await bot.SendMessage(msg.Chat, "📊 No categorized expenses found for this month.", cancellationToken: ct);
                }
                return;
            }
        }

        // Handle receipt photos/documents
        var hasPhoto = msg.Photo?.Any() ?? false;
        var hasDoc = msg.Document is not null;
        if (!(hasPhoto || hasDoc)) return;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<ReceiptProcessor>();
            var repository = scope.ServiceProvider.GetRequiredService<IReceiptRepository>();

            await using var contentStream = await processor.DownloadTelegramFileAsync(bot, msg, ct);
            var summary = await processor.ProcessAsync(contentStream, ct);

            // Check for duplicate receipt
            var receiptHash = summary.ComputeHash();
            var isDuplicate = await repository.ExistsByHashAsync(receiptHash, ct);
            
            if (isDuplicate)
            {
                await bot.SendMessage(
                    msg.Chat,
                    "⚠️ This receipt appears to be a duplicate and was not saved.",
                    cancellationToken: ct);
                return;
            }

            // Save to database
            await repository.SaveAsync(summary, ct);

            var totalFormatted = summary.Total.HasValue 
                ? $"€{summary.Total.Value:F2}" 
                : "Unknown";

            var reply =
                $"✅ Receipt saved!\n\n" +
                $"Merchant: {summary.MerchantName ?? "Unknown"}\n" +
                $"Total: {totalFormatted}\n" +  // Now shows €72.91 instead of ¤72.91
                $"Date: {summary.TransactionDate?.ToString("yyyy-MM-dd")} {summary.TransactionTime}\n" +
                $"Items: {summary.Items.Count}";

            await bot.SendMessage(msg.Chat, reply, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to process receipt");
            await bot.SendMessage(
                msg.Chat,
                "Sorry, I couldn't read that receipt. Please try a clearer photo or a PDF.",
                cancellationToken: ct);
        }
    }
}