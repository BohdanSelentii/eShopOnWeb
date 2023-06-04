namespace Microsoft.eShopWeb.ApplicationCore;

public class ServerlessFunctionsSettings
{
    public const string ConfigSection = "ServerlessFunctions";

    public string DeliveryOrderProcessorUrl { get; set; } = string.Empty;

    public string DeliveryOrderProcessorKey { get; set; } = string.Empty;
}
