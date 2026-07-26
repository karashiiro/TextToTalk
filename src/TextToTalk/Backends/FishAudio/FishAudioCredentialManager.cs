using System.Net;
using AdysTech.CredentialManager;

namespace TextToTalk.Backends.FishAudio;

public class FishAudioCredentialManager
{
    private const string CredentialsTarget = "TextToTalk_AccessKeys_FishAudio";

    public static NetworkCredential? LoadCredentials()
    {
        var credentials = CredentialManager.GetCredentials(CredentialsTarget);
        return credentials;
    }

    public static void SaveCredentials(string apiKey)
    {
        var credentials = new NetworkCredential("null", apiKey);
        CredentialManager.SaveCredentials(CredentialsTarget, credentials);
    }

    public static void DeleteCredentials()
    {
        CredentialManager.RemoveCredentials(CredentialsTarget);
    }
}
