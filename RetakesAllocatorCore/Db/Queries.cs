using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.EntityFrameworkCore;

namespace RetakesAllocatorCore.Db;

public class Queries
{
    public static async Task<UserSetting?> GetUserSettings(ulong userId)
    {
        await using var db = Db.Create();
        return await db.UserSettings.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
    }

    private static async Task<UserSetting?> UpsertUserSettings(ulong userId, Action<UserSetting> mutation)
    {
        if (userId == 0)
        {
            Log.Debug("Encountered userid 0, not upserting user settings");
            return null;
        }
        
        Log.Debug($"Upserting settings for {userId}");

        // Upserts are read-modify-write on one row; run them one at a time so two quick preference
        // changes from the same player cannot overwrite each other.
        await UpsertLock.WaitAsync();
        try
        {
            await using var db = Db.Create();
            var userSettings = await db.UserSettings.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
            var isNew = userSettings is null;
            userSettings ??= new UserSetting {UserId = userId};

            db.Entry(userSettings).State = isNew ? EntityState.Added : EntityState.Modified;

            mutation(userSettings);

            await db.SaveChangesAsync();

            return userSettings;
        }
        finally
        {
            UpsertLock.Release();
        }
    }

    private static readonly SemaphoreSlim UpsertLock = new(1, 1);

    /// <summary>
    /// For callers that start a write without awaiting it: logs a failure instead of leaving it unobserved.
    /// </summary>
    public static void LogFailure(Task task)
    {
        task.ContinueWith(
            t => Log.Error($"Failed to save weapon preference: {t.Exception?.GetBaseException()}"),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    // Preference writes are fire-and-forget from the game thread; log failures instead of losing them
    // as unobserved task exceptions.
    private static void RunInBackground(Func<Task> work)
    {
        Task.Run(async () =>
        {
            try
            {
                await work();
            }
            catch (Exception e)
            {
                Log.Error($"Failed to save weapon preference: {e}");
            }
        });
    }

    public static async Task SetWeaponPreferenceForUserAsync(ulong userId, CsTeam team,
        WeaponAllocationType weaponAllocationType,
        CsItem? item)
    {
        await UpsertUserSettings(userId,
            userSetting => { userSetting.SetWeaponPreference(team, weaponAllocationType, item); });
    }

    public static void SetWeaponPreferenceForUser(ulong userId, CsTeam team, WeaponAllocationType weaponAllocationType,
        CsItem? item)
    {
        RunInBackground(() => SetWeaponPreferenceForUserAsync(userId, team, weaponAllocationType, item));
    }

    public static async Task ClearWeaponPreferencesForUserAsync(ulong userId)
    {
        await UpsertUserSettings(userId, userSetting => { userSetting.WeaponPreferences = new(); });
    }

    public static void ClearWeaponPreferencesForUser(ulong userId)
    {
        RunInBackground(() => ClearWeaponPreferencesForUserAsync(userId));
    }

    public static async Task SetPreferredWeaponPreferenceAsync(ulong userId, CsItem? item)
    {
        await UpsertUserSettings(userId, userSetting =>
        {
            userSetting.SetWeaponPreference(CsTeam.Terrorist, WeaponAllocationType.Preferred,
                WeaponHelpers.CoercePreferredTeam(item, CsTeam.Terrorist));
            userSetting.SetWeaponPreference(CsTeam.CounterTerrorist, WeaponAllocationType.Preferred,
                WeaponHelpers.CoercePreferredTeam(item, CsTeam.CounterTerrorist));
        });
    }

    public static void SetPreferredWeaponPreference(ulong userId, CsItem? item)
    {
        RunInBackground(() => SetPreferredWeaponPreferenceAsync(userId, item));
    }

    public static IDictionary<ulong, UserSetting> GetUsersSettings(ICollection<ulong> userIds)
    {
        using var db = Db.Create();
        var userSettingsList = db
            .UserSettings
            .AsNoTracking()
            .Where(u => userIds.Contains(u.UserId))
            .ToList();
        if (userSettingsList.Count == 0)
        {
            return new Dictionary<ulong, UserSetting>();
        }

        return userSettingsList
            .GroupBy(p => p.UserId)
            .ToDictionary(g => g.Key, g => g.First());
    }

    public static void Migrate()
    {
        using var db = Db.Create();
        db.Database.Migrate();
    }

    public static void Wipe()
    {
        using var db = Db.Create();
        db.UserSettings.ExecuteDelete();
    }

    public static void Disconnect()
    {
        Db.Disconnect();
    }
}