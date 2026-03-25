using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using ShareSafely.API.Services;

namespace ShareSafely.API.Services;

public class KeyVaultService : IKeyVaultService
{
    private readonly SecretClient _client;
    private readonly Dictionary<string, string> _cache = new();

    public KeyVaultService(IConfiguration config)
    {
        var vaultName = config["Azure:KeyVaultName"]
            ?? throw new InvalidOperationException("KeyVaultName not configured");

        _client = new SecretClient(
            new Uri($"https://{vaultName}.vault.azure.net"),
            new DefaultAzureCredential()
        );
    }

    public async Task<string> GetSecretAsync(string secretName)
    {
        // Consider updating this to use a more persistent cache?
        if (_cache.TryGetValue(secretName, out var cached))
            return cached;

        var secret = await _client.GetSecretAsync(secretName);
        _cache[secretName] = secret.Value.Value;
        return _cache[secretName];
    }
}