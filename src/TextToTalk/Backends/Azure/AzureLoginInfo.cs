namespace TextToTalk.Backends.Azure;

public class AzureLoginInfo
{
    public string Region { get; set; } = "";

    public string SubscriptionKey { get; set; } = "";

    public void Deconstruct(out string region, out string subscriptionKey)
    {
        region = Region;
        subscriptionKey = SubscriptionKey;
    }
}