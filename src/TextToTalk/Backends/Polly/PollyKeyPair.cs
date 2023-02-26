namespace TextToTalk.Backends.Polly;

public class PollyKeyPair
{
    public string AccessKey { get; set; } = "";

    public string SecretKey { get; set; } = "";

    public void Deconstruct(out string accessKey, out string secretKey)
    {
        accessKey = AccessKey;
        secretKey = SecretKey;
    }
}