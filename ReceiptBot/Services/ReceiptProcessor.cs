using System.Text.RegularExpressions;
using Azure;
using Azure.AI.DocumentIntelligence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReceiptBot.Configuration;
using ReceiptBot.Models;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ReceiptBot.Services;

/// <summary>
/// Sends the incoming image/PDF to Azure Document Intelligence and maps the SDK result
/// into our compact <see cref="ReceiptSummary"/>.
/// </summary>
public sealed class ReceiptProcessor
{
    private readonly AzureDocIntelligenceClientFactory _factory;
    private readonly AzureDocIntelOptions _options;
    private readonly ILogger<ReceiptProcessor> _log;

    public ReceiptProcessor(
        AzureDocIntelligenceClientFactory factory,
        IOptions<AzureDocIntelOptions> options,
        ILogger<ReceiptProcessor> log)
    {
        _factory = factory;
        _options = options.Value;
        _log = log;
    }

    public async Task<Stream> DownloadTelegramFileAsync(ITelegramBotClient bot, Message msg, CancellationToken ct)
    {
        if (msg is null) throw new ArgumentNullException(nameof(msg));

        // 1) Берём file_id из фото (самое большое) или из документа
        string fileId = null;

        if (msg.Photo?.Any() == true)
        {
            var largest = msg.Photo.OrderBy(p => p.FileSize ?? 0).Last();
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

        // 2) Узнаём file_path по file_id
        var file = await bot.GetFile(fileId, ct);
        if (string.IsNullOrWhiteSpace(file.FilePath))
            throw new InvalidOperationException($"Telegram returned empty file_path for fileId={fileId}.");

        // 3) Качаем во временный файл (файл удалится при закрытии Stream)
        var tempPath = Path.Combine(Path.GetTempPath(), $"tg_{Guid.NewGuid():N}");
        var fs = new FileStream(
            tempPath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.Read,
            bufferSize: 4096,
            options: FileOptions.DeleteOnClose);

        await bot.DownloadFile(file.FilePath, fs, ct);
        fs.Position = 0; // на начало, чтобы дальше можно было читать

        return fs;
    }

    /// <summary>
    /// Analyzes the stream with the prebuilt receipt model and maps fields to <see cref="ReceiptSummary"/>.
    /// </summary>
    public async Task<ReceiptSummary> ProcessAsync(Stream content, CancellationToken ct = default)
    {
        var client = _factory.Create();

        var op = await client.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            _factory.ModelId,                  // "prebuilt-receipt" by default
            BinaryData.FromStream(content),
            ct);

        var result = op.Value;
        if (result.Documents.Count == 0)
            throw new InvalidOperationException("No receipt detected in document.");

        var receipt = result.Documents[0];

        // --- top-level fields ----------------------------------------------------
        var summary = new ReceiptSummary
        {
            CountryRegion = GetString(receipt.Fields, "CountryRegion"),
            MerchantName = GetString(receipt.Fields, "MerchantName"),
            MerchantPhone = GetString(receipt.Fields, "MerchantPhoneNumber"),
            ReceiptType = GetString(receipt.Fields, "ReceiptType"),
            TransactionDate = GetDateOnly(receipt.Fields, "TransactionDate"),
            TransactionTime = GetTimeOnly(receipt.Fields, "TransactionTime"),
            Total = GetCurrencyOrNumber(receipt.Fields, "Total"),
        };

        // --- items list ----------------------------------------------------------
        if (receipt.Fields.TryGetValue("Items", out var itemsField) &&
            itemsField is not null &&
            itemsField.FieldType == DocumentFieldType.List &&
            itemsField.ValueList is { } list)
        {
            foreach (var itemField in list)
            {
                if (itemField is null || itemField.FieldType != DocumentFieldType.Dictionary)
                    continue;

                var dict = itemField.ValueDictionary;

                var item = new ReceiptItem
                {
                    Description = GetString(dict, "Description"),
                    Category = GetString(dict, "Category"),
                    Quantity = GetCurrencyOrNumber(dict, "Quantity"), // quantity is numeric; same parser works
                    Unit = GetString(dict, "Unit"),
                    Price = GetCurrencyOrNumber(dict, "Price"),
                    TotalPrice = GetCurrencyOrNumber(dict, "TotalPrice"),
                };

                summary.Items.Add(item);
            }
        }

        return summary;
    }

    // ---------------------------- helpers ---------------------------------------

    private static string? GetString(IReadOnlyDictionary<string, DocumentField> fields, string key)
    {
        if (!fields.TryGetValue(key, out var f) || f is null) return null;

        if (f.FieldType == DocumentFieldType.String && f.ValueString is string s)
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        var raw = f.Content;
        return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
    }

    private static DateOnly? GetDateOnly(IReadOnlyDictionary<string, DocumentField> fields, string key)
    {
        if (!fields.TryGetValue(key, out var f) || f is null) return null;

        if (f.FieldType == DocumentFieldType.Date && f.ValueDate is DateTimeOffset dto)
            return DateOnly.FromDateTime(dto.Date);

        var raw = f.Content;
        if (string.IsNullOrWhiteSpace(raw)) return null;

        string[] fmts = { "yyyy-MM-dd", "dd.MM.yyyy", "MM/dd/yyyy", "dd/MM/yyyy", "yyyy/MM/dd" };
        if (DateTime.TryParseExact(raw.Trim(), fmts,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var parsed))
            return DateOnly.FromDateTime(parsed);

        return null;
    }

    private static TimeOnly? GetTimeOnly(IReadOnlyDictionary<string, DocumentField> fields, string key)
    {
        if (!fields.TryGetValue(key, out var f) || f is null) return null;

        // Try Content via regex to be resilient
        var raw = f.Content;
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var m = Regex.Match(raw, @"\b([01]?\d|2[0-3]):[0-5]\d(:[0-5]\d)?\b");
        if (!m.Success) return null;

        return TimeOnly.TryParse(m.Value, out var t) ? t : null;
    }
    
    private static decimal? GetCurrencyOrNumber(IReadOnlyDictionary<string, DocumentField> fields, string key)
    {
        if (!fields.TryGetValue(key, out var f) || f is null) return null;

        // 1) Валюта
        if (f.FieldType == DocumentFieldType.Currency &&
            f.ValueCurrency is CurrencyValue cur &&
            cur.Amount is double amount)
        {
            return (decimal)amount;
        }

        // 2) Double
        if (f.FieldType == DocumentFieldType.Double &&
            f.ValueDouble is double dval)
        {
            return (decimal)dval;
        }

        // 3) Int64
        if (f.FieldType == DocumentFieldType.Int64 &&
            f.ValueInt64 is long lval)
        {
            return lval;
        }

        // 4) Фолбэк: парсим текст (нормализуем EU-форматы)
        var text = f.Content;
        if (string.IsNullOrWhiteSpace(text)) return null;

        var cleaned = text.Trim()
            .Replace('\u00A0', ' ')
            .Replace("EUR", "", StringComparison.OrdinalIgnoreCase)
            .Replace("USD", "", StringComparison.OrdinalIgnoreCase)
            .Replace("€", "").Replace("$", "")
            .Trim();

        if (cleaned.Contains(',') && !cleaned.Contains('.'))
            cleaned = cleaned.Replace(',', '.');

        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"[^\d\.\-]", "");

        return decimal.TryParse(
            cleaned,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;
    }
}
