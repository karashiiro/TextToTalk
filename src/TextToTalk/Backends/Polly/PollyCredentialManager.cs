using AdysTech.CredentialManager;
using System.Net;

namespace TextToTalk.Backends.Polly;

public static class PollyCredentialManager
{
    private const string CredentialsTarget = "TextToTalk_AccessKeys_AmazonPolly";

    public static NetworkCredential LoadCredentials()
    {
        var credentials = CredentialManager.GetCredentials(CredentialsTarget);
        return credentials;
    }

    public static void SaveCredentials(string username, string password)
    {
        var credentials = new NetworkCredential(username, password);
        CredentialManager.SaveCredentials(CredentialsTarget, credentials);
    }

    public static void DeleteCredentials()
    {
        CredentialManager.RemoveCredentials(CredentialsTarget);
    }
}