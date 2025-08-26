namespace ReceiptBot.Configuration;

public sealed class AzureDocIntelOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey   { get; set; } = string.Empty;

    /// <summary>Defaults to "prebuilt-receipt".</summary>
    public string ModelId  { get; set; } = "prebuilt-receipt";
}
