using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReceiptBot.Configuration;
using ReceiptBot.Services;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace ReceiptBot.Services;

/// <summary>BackgroundService that connects to Telegram and handles updates.</summary>
public sealed class TelegramPollingService : BackgroundService
{
    private readonly ReceiptProcessor _processor;
    private readonly BotOptions _botOptions;
    private readonly ILogger<TelegramPollingService> _log;

    private ITelegramBotClient _bot = default!;

    public TelegramPollingService(
        ReceiptProcessor processor,
        IOptions<BotOptions> botOptions,
        ILogger<TelegramPollingService> log)
    {
        _processor   = processor;
        _botOptions  = botOptions.Value;
        _log         = log;
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

        if (msg.Text is { } text && text.Trim().StartsWith("/start", StringComparison.OrdinalIgnoreCase))
        {
            await bot.SendMessage(
                chatId: msg.Chat,
                text: "👋 Send me a photo or a PDF of a receipt. I will parse it with Azure Document Intelligence.",
                cancellationToken: ct);
            return;
        }

        var hasPhoto = msg.Photo?.Any() ?? false;
        var hasDoc   = msg.Document is not null;
        if (!(hasPhoto || hasDoc)) return;

        try
        {
            await using var contentStream = await _processor.DownloadTelegramFileAsync(bot, msg, ct);
            var summary = await _processor.ProcessAsync(contentStream, ct);

            var reply =
                $"Merchant: {summary.MerchantName}\n" +
                $"Total: {summary.Total}\n" +
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
