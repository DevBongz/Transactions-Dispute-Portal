using Npgsql;

namespace DisputePortal.Api.Infrastructure;

/// <summary>
/// Normalises a connection string into the Npgsql key-value form. Managed hosts (Render, Heroku,
/// Railway, etc.) expose Postgres credentials as a <c>postgres://user:pass@host:port/db</c> URL,
/// which Npgsql does not accept directly. When such a URL is detected it is converted (with
/// <c>SSL Mode=Require</c>, which managed Postgres requires); any value already in key-value
/// form (e.g. local docker-compose) is returned unchanged.
/// </summary>
public static class NpgsqlConnectionString
{
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;

        var isUri =
            value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);
        if (!isUri) return value;

        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
            SslMode = SslMode.Require, // encrypt without CA validation — required by managed Postgres
        };

        return builder.ConnectionString;
    }
}
