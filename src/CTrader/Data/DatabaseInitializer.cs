using System.Data;
using Microsoft.EntityFrameworkCore;

namespace CTrader.Data;

/// <summary>
/// Applies EF Core migrations at startup. Handles the transition from the old
/// EnsureCreated() approach: if a database already has the schema but no
/// __EFMigrationsHistory table, the initial migration is "baselined" (recorded
/// as applied without recreating tables) so existing data - including API keys
/// stored in the Parameters table - is preserved.
/// </summary>
public static class DatabaseInitializer
{
    public static void MigrateWithBaseline(DbContext context, ILogger logger)
    {
        var historyExists = TableExists(context, "__EFMigrationsHistory");
        var schemaExists = TableExists(context, "Parameters");

        if (schemaExists && !historyExists)
        {
            var initialMigration = context.Database.GetMigrations().First();
            logger.LogWarning(
                "Existing EnsureCreated() database detected without migration history. " +
                "Baselining migration {Migration} to preserve existing data.", initialMigration);
            BaselineMigration(context, initialMigration);
        }

        context.Database.Migrate();
        logger.LogInformation("Database migrations applied");
    }

    private static bool TableExists(DbContext context, string tableName)
    {
        var connection = context.Database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;
        if (wasClosed) connection.Open();
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
            var p = command.CreateParameter();
            p.ParameterName = "$name";
            p.Value = tableName;
            command.Parameters.Add(p);
            return Convert.ToInt64(command.ExecuteScalar()) > 0;
        }
        finally
        {
            if (wasClosed) connection.Close();
        }
    }

    private static void BaselineMigration(DbContext context, string migrationId)
    {
        var productVersion = typeof(DbContext).Assembly.GetName().Version?.ToString() ?? "8.0.0";
        var connection = context.Database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;
        if (wasClosed) connection.Open();
        try
        {
            using var create = connection.CreateCommand();
            create.CommandText =
                "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (" +
                "\"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, " +
                "\"ProductVersion\" TEXT NOT NULL);";
            create.ExecuteNonQuery();

            using var insert = connection.CreateCommand();
            insert.CommandText =
                "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ($id, $ver);";
            var pid = insert.CreateParameter();
            pid.ParameterName = "$id";
            pid.Value = migrationId;
            insert.Parameters.Add(pid);
            var pver = insert.CreateParameter();
            pver.ParameterName = "$ver";
            pver.Value = productVersion;
            insert.Parameters.Add(pver);
            insert.ExecuteNonQuery();
        }
        finally
        {
            if (wasClosed) connection.Close();
        }
    }
}
