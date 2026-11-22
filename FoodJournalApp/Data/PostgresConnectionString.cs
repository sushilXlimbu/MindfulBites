using FoodJournalApp.Security;
using Npgsql;

namespace FoodJournalApp.Data;

// Works out the Npgsql connection string from configuration.
//
// Precedence:
//   1. DATABASE_URL (env var, or user-secrets) if set. Railway/Render expose Postgres this way.
//   2. ConnectionStrings:Default (env var ConnectionStrings__Default, or user-secrets).
//
// Either value may be a normal key-value string ("Host=...;Database=...") OR a URI
// ("postgres://user:pass@host:5432/db?sslmode=require"). URIs are converted to key-value
// form, because Npgsql does not accept URIs directly.
public static class PostgresConnectionString
{
    public const string DatabaseUrlKey = "DATABASE_URL";
    public const string ConnectionStringKey = "ConnectionStrings:Default";

    public static string Resolve(IConfiguration config)
    {
        var databaseUrl = config[DatabaseUrlKey];
        var raw = !string.IsNullOrWhiteSpace(databaseUrl)
            ? databaseUrl
            : SecretConfig.Require(config, ConnectionStringKey); // throws a helpful error if unset

        return Normalize(raw);
    }

    public static string Normalize(string raw)
    {
        raw = raw.Trim();
        return IsUri(raw) ? FromUri(raw) : raw;
    }

    private static bool IsUri(string value) =>
        value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);

    private static string FromUri(string value)
    {
        var uri = new Uri(value);

        // User info is "user:password", each part percent-encoded (passwords often contain @, :, /).
        var userInfo = uri.UserInfo.Split(':', 2);
        var username = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null;

        var database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Username = username,
            Database = database
        };
        if (password is not null)
            builder.Password = password;

        // Carry over query options such as ?sslmode=require. Keys Npgsql doesn't know
        // are ignored rather than crashing startup.
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(kv[0]);
            var val = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : "";

            if (key.Equals("sslmode", StringComparison.OrdinalIgnoreCase))
            {
                // libpq values: disable/allow/prefer/require/verify-ca/verify-full
                builder.SslMode = val.ToLowerInvariant() switch
                {
                    "disable" => SslMode.Disable,
                    "allow" => SslMode.Allow,
                    "prefer" => SslMode.Prefer,
                    "require" => SslMode.Require,
                    "verify-ca" => SslMode.VerifyCA,
                    "verify-full" => SslMode.VerifyFull,
                    _ => builder.SslMode
                };
            }
            else
            {
                try { builder[key] = val; }
                catch (ArgumentException) { /* unknown key: skip */ }
            }
        }

        return builder.ConnectionString;
    }
}
