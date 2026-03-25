namespace ShareSafely.API.Services;

public interface IKeyVaultService
{
    Task<string> GetSecretAsync(string secretName);
}
