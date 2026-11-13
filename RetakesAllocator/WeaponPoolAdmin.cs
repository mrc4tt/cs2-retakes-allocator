using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using RetakesAllocatorCore;
using RetakesAllocatorCore.Config;

namespace RetakesAllocator;

/// <summary>
/// Changes the server's weapon pool (UsableWeapons) at runtime and saves it to config.json.
/// Shared by the in-game admin menu and the css_allocator_weapon / css_allocator_preset console
/// commands, so admins without FTP access can manage it from chat, the server console or RCON.
/// </summary>
public class WeaponPoolAdmin
{
    private readonly Action _onChanged;

    /// <param name="onChanged">Called after a successful save, e.g. to drop cached gun-menu lists.</param>
    public WeaponPoolAdmin(Action onChanged)
    {
        _onChanged = onChanged;
    }

    public static HashSet<CsItem> CurrentPool() => Configs.GetConfigData().UsableWeapons.ToHashSet();

    public static bool IsEnabled(CsItem weapon) => Configs.GetConfigData().UsableWeapons.Contains(weapon);

    /// <param name="enable">true/false to set, null to toggle.</param>
    public bool SetWeapon(CsItem weapon, bool? enable, Action<string> reply)
    {
        var pool = CurrentPool();
        var newState = enable ?? !pool.Contains(weapon);
        if (newState)
        {
            pool.Add(weapon);
        }
        else
        {
            pool.Remove(weapon);
        }

        if (!Save(pool, reply))
        {
            return false;
        }

        var key = newState ? "admin_weapons.enabled" : "admin_weapons.disabled";
        Reply(reply, Translator.Instance[key, WeaponCatalog.GetDisplayName(weapon)]);
        ReportProblems(reply);
        return true;
    }

    /// <summary>Turns several weapons on or off with a single save.</summary>
    public bool SetWeapons(IReadOnlyCollection<CsItem> weapons, bool enable, Action<string> reply)
    {
        var pool = CurrentPool();
        foreach (var weapon in weapons)
        {
            if (enable)
            {
                pool.Add(weapon);
            }
            else
            {
                pool.Remove(weapon);
            }
        }

        if (!Save(pool, reply))
        {
            return false;
        }

        var key = enable ? "admin_weapons.enabled" : "admin_weapons.disabled";
        var names = string.Join(", ", WeaponCatalog.SortForConfig(weapons).Select(WeaponCatalog.GetDisplayName));
        Reply(reply, Translator.Instance[key, names]);
        ReportProblems(reply);
        return true;
    }

    public bool SetCategory(WeaponCatalog.WeaponCategory category, bool enable, Action<string> reply)
    {
        var pool = CurrentPool();
        foreach (var weapon in category.Weapons)
        {
            if (enable)
            {
                pool.Add(weapon);
            }
            else
            {
                pool.Remove(weapon);
            }
        }

        if (!Save(pool, reply))
        {
            return false;
        }

        var key = enable ? "admin_weapons.enabled" : "admin_weapons.disabled";
        Reply(reply, Translator.Instance[key, category.DisplayName]);
        ReportProblems(reply);
        return true;
    }

    public bool ApplyPreset(WeaponCatalog.WeaponPreset preset, Action<string> reply)
    {
        if (!Save(preset.Weapons, reply))
        {
            return false;
        }

        Reply(reply, Translator.Instance["admin_weapons.preset_applied", preset.Key, preset.Weapons.Count]);
        ReportProblems(reply);
        return true;
    }

    /// <summary>One line per category: "Rifles (5/7): AK-47, M4A4, ... | off: SG 553, AUG".</summary>
    public static IEnumerable<string> DescribePool()
    {
        var pool = CurrentPool();
        foreach (var category in WeaponCatalog.Categories)
        {
            var on = category.Weapons.Where(pool.Contains).Select(WeaponCatalog.GetDisplayName).ToList();
            var off = category.Weapons.Where(w => !pool.Contains(w)).Select(WeaponCatalog.GetDisplayName).ToList();
            var line = $"{category.DisplayName} ({on.Count}/{category.Weapons.Count}): " +
                       (on.Count > 0 ? string.Join(", ", on) : "-");
            if (off.Count > 0)
            {
                line += $" | off: {string.Join(", ", off)}";
            }

            yield return line;
        }
    }

    private bool Save(IEnumerable<CsItem> pool, Action<string> reply)
    {
        try
        {
            Configs.SaveUsableWeapons(pool);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to save UsableWeapons: {e}");
            Reply(reply, Translator.Instance["admin_weapons.save_failed", e.Message]);
            return false;
        }

        Log.Info($"UsableWeapons updated and saved to '{Configs.ConfigFilePath}'.");
        _onChanged();
        return true;
    }

    /// <summary>
    /// Warns about round slots that are now empty for a team (players would get no gun there) and
    /// about DefaultWeapons entries that point at a disabled gun (that default is skipped).
    /// </summary>
    private static void ReportProblems(Action<string> reply)
    {
        var slots = new[]
        {
            (WeaponAllocationType.PistolRound, "roundtype.Pistol"),
            (WeaponAllocationType.HalfBuyPrimary, "roundtype.HalfBuy"),
            (WeaponAllocationType.FullBuyPrimary, "roundtype.FullBuy"),
        };
        var teams = new[]
        {
            (CsTeam.Terrorist, "teams.terrorist_short"),
            (CsTeam.CounterTerrorist, "teams.counter_terrorist_short"),
        };

        foreach (var (team, teamKey) in teams)
        {
            foreach (var (slot, slotKey) in slots)
            {
                if (WeaponHelpers.GetPossibleWeaponsForAllocationType(slot, team).Count == 0)
                {
                    Reply(reply, Translator.Instance["admin_weapons.empty_slot",
                        Translator.Instance[slotKey], Translator.Instance[teamKey]]);
                }
            }
        }
    }

    private static void Reply(Action<string> reply, string message)
    {
        Helpers.WriteNewlineDelimited(message, reply);
    }
}
