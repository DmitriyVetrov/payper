using Azure;
using Azure.AI.DocumentIntelligence;
using Microsoft.Extensions.Options;
using ReceiptBot.Configuration;

namespace ReceiptBot.Services;

public sealed class AzureDocIntelligenceClientFactory
{
    private readonly AzureDocIntelOptions _options;

    public AzureDocIntelligenceClientFactory(IOptions<AzureDocIntelOptions> options)
        => _options = options.Value;

    public DocumentIntelligenceClient Create()
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint) || string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("Azure Document Intelligence is not configured. Set AZURE_DI_ENDPOINT and AZURE_DI_KEY.");

        return new DocumentIntelligenceClient(new Uri(_options.Endpoint), new AzureKeyCredential(_options.ApiKey));
    }

    public string ModelId => string.IsNullOrWhiteSpace(_options.ModelId) ? "prebuilt-receipt" : _options.ModelId;
}
