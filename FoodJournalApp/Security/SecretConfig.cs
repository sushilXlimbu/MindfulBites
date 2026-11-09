namespace FoodJournalApp.Security;

// Reads a secret from configuration and refuses to start if it is missing or still the
// committed placeholder. Real values come from user-secrets (local, Development) or
// environment variables (deployed host, e.g. Jwt__Key) — never from appsettings.json.
public static class SecretConfig
{
    public const string PlaceholderPrefix = "REPLACE_VIA_USER_SECRETS";

    public static string Require(IConfiguration config, string key)
    {
        var value = config[key];
        if (string.IsNullOrWhiteSpace(value) ||
            value.StartsWith(PlaceholderPrefix, StringComparison.Ordinal))
        {
            var envName = key.Replace(":", "__");
            throw new InvalidOperationException(
                $"'{key}' is not configured. Locally: dotnet user-secrets set \"{key}\" \"<value>\". " +
                $"Deployed: set the environment variable {envName}.");
        }
        return value;
    }
}
