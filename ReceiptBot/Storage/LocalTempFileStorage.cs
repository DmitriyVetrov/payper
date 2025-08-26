using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ReceiptBot.Storage
{
    /// <summary>
    /// Downloads a Telegram photo/document to a temp file and returns an open read stream.
    /// Temp file will be deleted when the returned stream is disposed.
    /// </summary>
    public sealed class LocalTempFileStorage : IFileStorage
    {
        public async Task<Stream> DownloadTelegramFileAsStreamAsync(
            ITelegramBotClient bot,
            Message msg,
            CancellationToken ct = default)
        {
            if (msg is null)
                throw new ArgumentNullException(nameof(msg));

            // 1) Decide which Telegram file to download: the largest photo or the document as-is
            string fileId;

            if (msg.Photo?.Any() == true)
            {
                var largest = msg.Photo
                    .OrderBy(p => p.FileSize ?? 0)
                    .Last();

                fileId = largest.FileId;
            }
            else if (msg.Document is { } doc)
            {
                fileId = doc.FileId;
            }
            else
            {
                throw new InvalidOperationException("Message contains no photo or document.");
            }

            // 2) Resolve Telegram's internal file path for the chosen file id
            var file = await bot.GetFile(fileId, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(file.FilePath))
                throw new InvalidOperationException("Telegram returned an empty file path.");

            // 3) Create a temp file and download the content into it
            var tempPath = Path.Combine(Path.GetTempPath(), $"tg_{Guid.NewGuid():N}");
            await using (var fs = File.Create(tempPath))
            {
                await bot.DownloadFile(file.FilePath, fs, ct).ConfigureAwait(false);
            }

            // 4) Open the temp file for reading; it will be deleted automatically on dispose
            return new FileStream(
                tempPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                options: FileOptions.DeleteOnClose);
        }
    }
}
