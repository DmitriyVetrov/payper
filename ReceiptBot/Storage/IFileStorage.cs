using Telegram.Bot;
using Telegram.Bot.Types;

namespace ReceiptBot.Storage;

/// <summary>Abstraction for downloading Telegram media into a stream.</summary>
public interface IFileStorage
{
    Task<Stream> DownloadTelegramFileAsStreamAsync(ITelegramBotClient bot, Message msg, CancellationToken ct = default);
}
