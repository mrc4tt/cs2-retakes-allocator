using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.EntityFrameworkCore;
using RetakesAllocatorCore.Config;

namespace RetakesAllocatorCore.Db;

public class Db : DbContext
{
    public DbSet<UserSetting> UserSettings { get; set; }

    /// <summary>
    /// Creates a context for a single unit of work. A DbContext is not thread-safe and the queries run from
    /// both the game thread and background tasks, so every operation gets its own short-lived instance.
    /// The provider's connection pool makes this cheap. Callers must dispose it.
    /// </summary>
    public static Db Create()
    {
        return new Db();
    }

    public static void Disconnect()
    {
        // Contexts are disposed per operation; only the pooled connections are left to release.
        try
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            MySqlConnector.MySqlConnection.ClearAllPools();
        }
        catch (Exception e)
        {
            Log.Warn($"Failed to clear database connection pools: {e.Message}");
        }
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

        // TODO This whole thing needs to be fixed per
        // https://jasonwatmore.com/post/2020/01/03/aspnet-core-ef-core-migrations-for-multiple-databases-sqlite-and-sql-server
        var configData = Configs.IsLoaded() ? Configs.GetConfigData() : new ConfigData();
        var databaseConnectionString = configData.DatabaseConnectionString;
        switch (configData.DatabaseProvider)
        {
            case DatabaseProvider.Sqlite:
                Utils.SetupSqlite(databaseConnectionString, optionsBuilder);
                break;
            case DatabaseProvider.MySql:
                Utils.SetupMySql(databaseConnectionString, optionsBuilder);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserSetting>()
            .Property(e => e.WeaponPreferences)
            .IsRequired(false);
        base.OnModelCreating(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        UserSetting.Configure(configurationBuilder);
        configurationBuilder
            .Properties<CsItem>()
            .HaveConversion<CsItemConverter>();
    }
}
