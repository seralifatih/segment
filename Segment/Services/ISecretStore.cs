namespace Segment.App.Services
{
    public interface ISecretStore
    {
        void SetSecret(string key, string value);
        string GetSecret(string key);
        void DeleteSecret(string key);
    }
}
