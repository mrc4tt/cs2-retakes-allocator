using System.Text.Json;
using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace RetakesAllocatorCore.Config;

public static class Configs
{
    public static class Shared
    {
        public static string? Module { get; set; }
    }
    // Legacy config lived inside the plugin folder: plugins/RetakesAllocator/config/config.json.
    private static readonly string ConfigDirectoryName = "config";
    private static readonly string ConfigFileName = "config.json";

    // CSS-standard config lives outside the plugin folder so it survives plugin
    // updates: addons/counterstrikesharp/configs/plugins/RetakesAllocator/config.json.
    private static readonly string PluginName = "RetakesAllocator";

    private static string? _configFilePath;
    private static ConfigData? _configData;

    private static readonly JsonSerializerOptions SerializationOptions = new()
    {
        Converters =
        {
            new JsonStringEnumConverter()
        },
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// The commented config file written to disk on first run. System.Text.Json
    /// can READ // comments (ReadCommentHandling.Skip) but cannot WRITE them, so
    /// the default config is shipped as this verbatim template instead of being
    /// serialized from <see cref="ConfigData"/>. Only written when no config.json
    /// exists yet; existing files are never overwritten (see Load), so upgrading
    /// the plugin cannot lose an admin's settings. Keep values in sync with the
    /// ConfigData record defaults.
    /// </summary>
    public const string DefaultConfigTemplate =
"""
{
  // ############################################################################
  //  RETAKES ALLOCATOR CONFIG
  //  Comments (// ...) and trailing commas are allowed — the plugin ignores them.
  //  Weapon names list: https://github.com/roflmuffin/CounterStrikeSharp/blob/main/managed/CounterStrikeSharp.API/Modules/Entities/Constants/CsItem.cs
  //
  //  The settings YOU usually change are at the top, each with a guide.
  //  Everything below the "ADVANCED" line rarely needs touching.
  // ############################################################################


  // ┌──────────────────────────────────────────────────────────────────────────┐
  // │ 1) UsableWeapons — the full pool of guns the plugin may hand out.          │
  // │                                                                            │
  // │   • DELETE a name to remove that gun from the server completely.           │
  // │       e.g. remove "AWP" + "SSG08" + "SCAR20" + "G3SG1"  -> no snipers.     │
  // │   • A gun NOT in this list is never given, and (with EnableCanAcquireHook  │
  // │     = true, see ADVANCED) cannot be bought either.                         │
  // │   • Grouped by type below just for readability — order does not matter.    │
  // └──────────────────────────────────────────────────────────────────────────┘
  "UsableWeapons": [
    // Pistols
    "Deagle", "Glock", "USPS", "HKP2000", "Elite", "Tec9", "P250", "CZ", "FiveSeven", "Revolver",
    // SMGs
    "Mac10", "MP9", "MP7", "P90", "MP5SD", "Bizon", "UMP45",
    // Shotguns
    "XM1014", "Nova", "MAG7", "SawedOff",
    // Machine guns
    "M249", "Negev",
    // Rifles
    "AK47", "M4A1S", "M4A1", "GalilAR", "Famas", "SG556", "AUG",
    // Snipers
    "AWP", "SSG08", "SCAR20", "G3SG1"
  ],


  // ┌──────────────────────────────────────────────────────────────────────────┐
  // │ 2) AllowedWeaponSelectionTypes — HOW each player's gun is decided.         │
  // │                                                                            │
  // │   Each round the plugin tries these IN THIS ORDER and uses the first one   │
  // │   that is enabled and produces a gun:                                      │
  // │       "PlayerChoice" -> the gun the player picked with !guns / !gun        │
  // │       "Random"       -> a random gun from UsableWeapons                    │
  // │       "Default"      -> the fixed gun from DefaultWeapons (section 3)       │
  // │                                                                            │
  // │   COMMON SETUPS — copy the one you want:                                   │
  // │       Varied guns + players can pick : ["PlayerChoice","Random","Default"] │
  // │       Random guns for everyone       : ["Random","Default"]                │
  // │       Same fixed guns every round    : ["Default"]                         │
  // │       Players pick, else random      : ["PlayerChoice","Random"]           │
  // │                                                                            │
  // │   NOTE: if the list is ["Default"] only, EVERYONE gets the same M4/Deagle  │
  // │   set every round. Add "Random" to fix that.                              │
  // └──────────────────────────────────────────────────────────────────────────┘
  "AllowedWeaponSelectionTypes": [
    "PlayerChoice",
    "Random",
    "Default"
  ],


  // ┌──────────────────────────────────────────────────────────────────────────┐
  // │ 3) DefaultWeapons — the fixed fallback gun for each team + round slot.     │
  // │                                                                            │
  // │   Used when "Default" is active, or when PlayerChoice/Random gave nothing. │
  // │   Slots:                                                                   │
  // │       PistolRound    = gun on the opening pistol round                     │
  // │       Secondary      = pistol on rifle/eco rounds                          │
  // │       HalfBuyPrimary = main gun on half-buy (eco) rounds  (SMG/shotgun)    │
  // │       FullBuyPrimary = main gun on full-buy rounds        (rifle)          │
  // │   Each value must be a name from UsableWeapons above.                      │
  // └──────────────────────────────────────────────────────────────────────────┘
  "DefaultWeapons": {
    "Terrorist": {
      "PistolRound": "Glock",
      "Secondary": "Deagle",
      "HalfBuyPrimary": "Mac10",
      "FullBuyPrimary": "AK47"
    },
    "CounterTerrorist": {
      "PistolRound": "USPS",
      "Secondary": "Deagle",
      "HalfBuyPrimary": "MP9",
      "FullBuyPrimary": "M4A1S"
    }
  },


  // ┌──────────────────────────────────────────────────────────────────────────┐
  // │ 4) MaxNades — max grenades PER PLAYER, per team.                           │
  // │                                                                            │
  // │   "GLOBAL" applies to every map. To override one map, add its name as a    │
  // │   key (e.g. "de_mirage") with only the values you want to change.          │
  // │   Valid T nades : Flashbang, Smoke, Molotov,    HighExplosive              │
  // │   Valid CT nades: Flashbang, Smoke, Incendiary, HighExplosive              │
  // │   (Molotov/Incendiary mixups are auto-corrected.)                          │
  // │                                                                            │
  // │   Example map override (uncomment to allow 2 CT smokes on Mirage only):    │
  // │     "de_mirage": { "CounterTerrorist": { "Smoke": 2 } }                    │
  // └──────────────────────────────────────────────────────────────────────────┘
  "MaxNades": {
    "GLOBAL": {
      "Terrorist": {
        "Flashbang": 2,
        "Smoke": 1,
        "Molotov": 1,
        "HighExplosive": 1
      },
      "CounterTerrorist": {
        "Flashbang": 2,
        "Smoke": 1,
        "Incendiary": 2,
        "HighExplosive": 1
      }
    }
  },


  // ┌──────────────────────────────────────────────────────────────────────────┐
  // │ 5) MaxTeamNades — max grenades for the WHOLE TEAM combined, per round type.│
  // │                                                                            │
  // │   Same "GLOBAL" + per-map structure as MaxNades. Values per round type:    │
  // │       A number as text: "One" .. "Ten",  or  "None"                        │
  // │       Or scale to team size (all round up):                                │
  // │         "AveragePointFivePerPlayer"   (~1 per 2 players)                    │
  // │         "AverageOnePerPlayer"                                              │
  // │         "AverageOnePointFivePerPlayer"                                     │
  // │         "AverageTwoPerPlayer"                                              │
  // └──────────────────────────────────────────────────────────────────────────┘
  "MaxTeamNades": {
    "GLOBAL": {
      "Terrorist": {
        "Pistol": "AverageOnePerPlayer",
        "HalfBuy": "AverageOnePointFivePerPlayer",
        "FullBuy": "AverageOnePointFivePerPlayer"
      },
      "CounterTerrorist": {
        "Pistol": "AverageOnePerPlayer",
        "HalfBuy": "AverageOnePointFivePerPlayer",
        "FullBuy": "AverageOnePointFivePerPlayer"
      }
    }
  },


  // ┌──────────────────────────────────────────────────────────────────────────┐
  // │ 6) Round type mix — how Pistol / HalfBuy / FullBuy rounds are scheduled.   │
  // │                                                                            │
  // │   RoundTypeSelection picks WHICH system below is used:                     │
  // │       "Random"            -> use RoundTypePercentages    (weighted random) │
  // │       "RandomFixedCounts" -> use RoundTypeRandomFixedCounts (random order) │
  // │       "ManualOrdering"    -> use RoundTypeManualOrdering (exact order)     │
  // │   Only the matching block is read; the other two are ignored.              │
  // └──────────────────────────────────────────────────────────────────────────┘
  "RoundTypeSelection": "Random",

  // Used by "Random". Chance of each round type. MUST add up to exactly 100.
  "RoundTypePercentages": {
    "Pistol": 15,
    "HalfBuy": 25,
    "FullBuy": 60
  },

  // Used by "RandomFixedCounts". Exact count of each type per block, shuffled.
  // Example: over 30 rounds -> exactly 5 pistol, 10 half, 15 full, random order.
  "RoundTypeRandomFixedCounts": {
    "Pistol": 5,
    "HalfBuy": 10,
    "FullBuy": 15
  },

  // Used by "ManualOrdering". Plays in this EXACT order, then repeats.
  // Example below = 5 pistol, then 10 half-buy, then 15 full-buy, then loop.
  "RoundTypeManualOrdering": [
    { "Type": "Pistol",  "Count": 5  },
    { "Type": "HalfBuy", "Count": 10 },
    { "Type": "FullBuy", "Count": 15 }
  ],


  // ############################################################################
  //  ADVANCED — most servers leave everything below at these values.
  // ############################################################################

  "ZeusPreference": "Never",
  "AllowPreferredWeaponForEveryone": false,
  "ChanceForPreferredWeapon": 100,
  "NumberOfExtraVipChancesForPreferredWeapon": 1,
  "MaxPreferredWeaponsPerTeam": { "Terrorist": 1, "CounterTerrorist": 1 },
  "MinPlayersPerTeamForPreferredWeapon": { "Terrorist": 1, "CounterTerrorist": 1 },

  "EnableCanAcquireHook": true,
  "EnableBuyMenu": true,            // false = disable buying; players keep only the allocated loadout
  "AllowAllocationAfterFreezeTime": true,
  "UseOnTickFeatures": true,
  "CapabilityWeaponPaints": true,
  "AutoUpdateSignatures": true,
  "MigrateOnStartup": true,
  "ResetStateOnGameRestart": true,

  "EnableRoundTypeAnnouncement": true,
  "EnableRoundTypeAnnouncementCenter": false,
  "EnableBombSiteAnnouncementCenter": false,
  "BombSiteAnnouncementCenterToCTOnly": false,
  "DisableDefaultBombPlantedCenterMessage": false,
  "ForceCloseBombSiteAnnouncementCenterOnPlant": true,
  "BombSiteAnnouncementCenterDelay": 1,
  "BombSiteAnnouncementCenterShowTimer": 5,
  "EnableBombSiteAnnouncementChat": false,
  "EnableNextRoundTypeVoting": false,

  "InGameGunMenuCenterCommands": "gunsmenu,gunmenu,!gunmenu,!gunsmenu,!menugun,!menuguns,/gunsmenu,/gunmenu",
  "InGameGunMenuChatCommands": "guns,!guns,/guns",
  "ChatMessagePluginName": "Retakes",
  "ChatMessagePluginPrefix": null,

  "LogLevel": "Information",
  "DatabaseProvider": "Sqlite",
  "DatabaseConnectionString": "Data Source=data.db; Pooling=False"
}
""";

    public static bool IsLoaded()
    {
        return _configData is not null;
    }

    public static ConfigData GetConfigData()
    {
        if (_configData is null)
        {
            throw new Exception("Config not yet loaded.");
        }

        return _configData;
    }

    /// <summary>
    /// Resolves where config.json lives, preferring the CSS-standard
    /// configs/plugins/RetakesAllocator/ location (which survives plugin updates).
    /// When <paramref name="migrateLegacy"/> is true and a config only exists at the
    /// old in-plugin-folder location, it is moved to the new path so existing servers
    /// keep their settings after the move. The move preserves the file verbatim, so
    /// any // comments and custom values are kept.
    /// </summary>
    private static string ResolveConfigFilePath(string modulePath, bool migrateLegacy)
    {
        // .../plugins/RetakesAllocator  ->  .../configs/plugins/RetakesAllocator
        var newDir = Path.GetFullPath(
            Path.Combine(modulePath, "..", "..", "configs", "plugins", PluginName));
        Directory.CreateDirectory(newDir);
        var newPath = Path.Combine(newDir, ConfigFileName);

        var legacyPath = Path.Combine(modulePath, ConfigDirectoryName, ConfigFileName);

        if (migrateLegacy && !File.Exists(newPath) && File.Exists(legacyPath))
        {
            try
            {
                File.Move(legacyPath, newPath);
                Log.Info($"Migrated config from '{legacyPath}' to '{newPath}'.");
            }
            catch (Exception e)
            {
                // Fall back to a copy so a failed move never loses the admin's config.
                try
                {
                    File.Copy(legacyPath, newPath, overwrite: false);
                    Log.Info(
                        $"Copied config from '{legacyPath}' to '{newPath}' " +
                        $"(move failed: {e.Message}). The old file was left in place.");
                }
                catch (Exception e2)
                {
                    Log.Warn(
                        $"Failed to migrate config from '{legacyPath}' to '{newPath}': {e2.Message}. " +
                        $"Using the legacy location instead.");
                    return legacyPath;
                }
            }
        }

        return newPath;
    }

    public static ConfigData Load(string modulePath, bool saveAfterLoad = false)
    {
        _configFilePath = ResolveConfigFilePath(modulePath, migrateLegacy: saveAfterLoad);
        if (File.Exists(_configFilePath))
        {
            // Existing file: read it but never rewrite it. Re-serializing would
            // strip the // comments in the shipped template (System.Text.Json
            // cannot write comments). Any config keys the admin removed simply
            // fall back to the ConfigData record defaults at runtime.
            _configData =
                JsonSerializer.Deserialize<ConfigData>(File.ReadAllText(_configFilePath), SerializationOptions);
        }
        else
        {
            // First run: ship the documented, commented template verbatim so the
            // admin's config file IS the manual. saveAfterLoad gates whether we
            // persist it (false in read-only/test loads).
            if (saveAfterLoad)
            {
                File.WriteAllText(_configFilePath, DefaultConfigTemplate);
            }

            _configData =
                JsonSerializer.Deserialize<ConfigData>(DefaultConfigTemplate, SerializationOptions);
        }

        if (_configData is null)
        {
            throw new Exception("Failed to load configs.");
        }

        _configData.Validate();

        return _configData;
    }

    public static ConfigData? ParseConfigDataForTests(string json)
    {
        return JsonSerializer.Deserialize<ConfigData>(json, SerializationOptions);
    }

    public static ConfigData OverrideConfigDataForTests(
        ConfigData configData
    )
    {
        configData.Validate();
        _configData = configData;
        return _configData;
    }

    private static void SaveConfigData(ConfigData configData)
    {
        if (_configFilePath is null)
        {
            throw new Exception("Config not yet loaded.");
        }

        File.WriteAllText(_configFilePath, JsonSerializer.Serialize(configData, SerializationOptions));
    }

    public static string? StringifyConfig(string? configName)
    {
        var configData = GetConfigData();
        if (configName is null)
        {
            return JsonSerializer.Serialize(configData, SerializationOptions);
        }
        var property = configData.GetType().GetProperty(configName);
        if (property is null)
        {
            return null;
        }
        return JsonSerializer.Serialize(property.GetValue(configData), SerializationOptions);
    }
}

public enum WeaponSelectionType
{
    PlayerChoice,
    Random,
    Default,
}

public enum DatabaseProvider
{
    Sqlite,
    MySql,
}

public enum RoundTypeSelectionOption
{
    Random,
    RandomFixedCounts,
    ManualOrdering,
}

public record RoundTypeManualOrderingItem(RoundType Type, int Count);

public record ConfigData
{
    public List<CsItem> UsableWeapons { get; set; } = WeaponHelpers.AllWeapons;

    public List<WeaponSelectionType> AllowedWeaponSelectionTypes { get; set; } =
        Enum.GetValues<WeaponSelectionType>().ToList();

    public Dictionary<CsTeam, Dictionary<WeaponAllocationType, CsItem>> DefaultWeapons { get; set; } =
        WeaponHelpers.DefaultWeaponsByTeamAndAllocationType;

    public Dictionary<
        string,
        Dictionary<
            CsTeam,
            Dictionary<CsItem, int>
        >
    > MaxNades { get; set; } = new()
    {
        {
            NadeHelpers.GlobalSettingName, new()
            {
                {
                    CsTeam.Terrorist, new()
                    {
                        {CsItem.Flashbang, 2},
                        {CsItem.Smoke, 1},
                        {CsItem.Molotov, 1},
                        {CsItem.HE, 1},
                    }
                },
                {
                    CsTeam.CounterTerrorist, new()
                    {
                        {CsItem.Flashbang, 2},
                        {CsItem.Smoke, 1},
                        {CsItem.Incendiary, 2},
                        {CsItem.HE, 1},
                    }
                },
            }
        }
    };

    public Dictionary<
        string,
        Dictionary<
            CsTeam,
            Dictionary<RoundType, MaxTeamNadesSetting>
        >
    > MaxTeamNades { get; set; } = new()
    {
        {
            NadeHelpers.GlobalSettingName, new()
            {
                {
                    CsTeam.Terrorist, new()
                    {
                        {RoundType.Pistol, MaxTeamNadesSetting.AverageOnePerPlayer},
                        {RoundType.HalfBuy, MaxTeamNadesSetting.AverageOnePointFivePerPlayer},
                        {RoundType.FullBuy, MaxTeamNadesSetting.AverageOnePointFivePerPlayer},
                    }
                },
                {
                    CsTeam.CounterTerrorist, new()
                    {
                        {RoundType.Pistol, MaxTeamNadesSetting.AverageOnePerPlayer},
                        {RoundType.HalfBuy, MaxTeamNadesSetting.AverageOnePointFivePerPlayer},
                        {RoundType.FullBuy, MaxTeamNadesSetting.AverageOnePointFivePerPlayer},
                    }
                },
            }
        }
    };

    public RoundTypeSelectionOption RoundTypeSelection { get; set; } = RoundTypeSelectionOption.Random;

    public Dictionary<RoundType, int> RoundTypePercentages { get; set; } = new()
    {
        {RoundType.Pistol, 15},
        {RoundType.HalfBuy, 25},
        {RoundType.FullBuy, 60},
    };

    public Dictionary<RoundType, int> RoundTypeRandomFixedCounts { get; set; } = new()
    {
        {RoundType.Pistol, 5},
        {RoundType.HalfBuy, 10},
        {RoundType.FullBuy, 15},
    };

    public List<RoundTypeManualOrderingItem> RoundTypeManualOrdering { get; set; } = new()
    {
        new RoundTypeManualOrderingItem(RoundType.Pistol, 5),
        new RoundTypeManualOrderingItem(RoundType.HalfBuy, 10),
        new RoundTypeManualOrderingItem(RoundType.FullBuy, 15),
    };

    public bool MigrateOnStartup { get; set; } = true;
    public bool ResetStateOnGameRestart { get; set; } = true;
    public bool AllowAllocationAfterFreezeTime { get; set; } = true;
    public bool UseOnTickFeatures { get; set; } = true;
    public bool CapabilityWeaponPaints { get; set; } = true;
    public bool EnableRoundTypeAnnouncement { get; set; } = true;
    public bool EnableRoundTypeAnnouncementCenter { get; set; } = false;
    public bool EnableBombSiteAnnouncementCenter { get; set; } = false;
    public bool BombSiteAnnouncementCenterToCTOnly { get; set; } = false;
    public bool DisableDefaultBombPlantedCenterMessage { get; set; } = false;
    public bool ForceCloseBombSiteAnnouncementCenterOnPlant { get; set; } = true;
    public float BombSiteAnnouncementCenterDelay { get; set; } = 1.0f;
    public float BombSiteAnnouncementCenterShowTimer { get; set; } = 5.0f;
    public bool EnableBombSiteAnnouncementChat { get; set; } = false;
    public bool EnableNextRoundTypeVoting { get; set; } = false;
    public int NumberOfExtraVipChancesForPreferredWeapon { get; set; } = 1;
    public bool AllowPreferredWeaponForEveryone { get; set; } = false;

    public double ChanceForPreferredWeapon { get; set; } = 100;

    public Dictionary<CsTeam, int> MaxPreferredWeaponsPerTeam { get; set; } = new()
    {
        {CsTeam.Terrorist, 1},
        {CsTeam.CounterTerrorist, 1},
    };

    public Dictionary<CsTeam, int> MinPlayersPerTeamForPreferredWeapon { get; set; } = new()
    {
        {CsTeam.Terrorist, 1},
        {CsTeam.CounterTerrorist, 1},
    };

    public bool EnableCanAcquireHook { get; set; } = true;

    // When true, players can open the buy menu (the CanAcquire hook still limits
    // them to weapons valid for the round). When false, buying is disabled so
    // players keep only the plugin-allocated loadout. Implemented by toggling
    // mp_buy_anywhere each round, since retakes spawns have no buy zone.
    public bool EnableBuyMenu { get; set; } = true;

    public LogLevel LogLevel { get; set; } = LogLevel.Information;
    public string ChatMessagePluginName { get; set; } = "Retakes";
    public string? ChatMessagePluginPrefix { get; set; }

    public string InGameGunMenuCenterCommands { get; set; } =
        "gunsmenu,gunmenu,!gunmenu,!gunsmenu,!menugun,!menuguns,/gunsmenu,/gunmenu";

    public string InGameGunMenuChatCommands { get; set; } = "guns,!guns,/guns";
    public ZeusPreference ZeusPreference { get; set; } = ZeusPreference.Never;

    public DatabaseProvider DatabaseProvider { get; set; } = DatabaseProvider.Sqlite;
    public string DatabaseConnectionString { get; set; } = "Data Source=data.db; Pooling=False";
    public bool AutoUpdateSignatures { get; set; } = true;

    public IList<string> Validate()
    {
        if (RoundTypePercentages.Values.Sum() != 100)
        {
            throw new Exception("'RoundTypePercentages' values must add up to 100");
        }

        var warnings = new List<string>();
        warnings.AddRange(ValidateDefaultWeapons(CsTeam.Terrorist));
        warnings.AddRange(ValidateDefaultWeapons(CsTeam.CounterTerrorist));
        warnings.AddRange(ValidateWeaponSelectionTypes());

        foreach (var warning in warnings)
        {
            Log.Warn($"[CONFIG WARNING] {warning}");
        }

        return warnings;
    }

    private ICollection<string> ValidateWeaponSelectionTypes()
    {
        var warnings = new List<string>();

        if (AllowedWeaponSelectionTypes.Count == 0)
        {
            warnings.Add(
                "AllowedWeaponSelectionTypes is empty — no weapons will be allocated. " +
                "Add at least one of: PlayerChoice, Random, Default.");
            return warnings;
        }

        // The common newbie confusion: only "Default" is active, so every player
        // gets the same fixed DefaultWeapons set every round (e.g. M4 + Deagle).
        // Point them at "Random" so they know how to get variety.
        if (CanAssignDefaultWeapons() && !CanAssignRandomWeapons() && !CanPlayersSelectWeapons())
        {
            warnings.Add(
                "AllowedWeaponSelectionTypes only contains 'Default', so every player gets the " +
                "fixed DefaultWeapons set every round. Add 'Random' for varied weapons, or " +
                "'PlayerChoice' to let players pick with !guns.");
        }

        if (UsableWeapons.Count == 0)
        {
            warnings.Add("UsableWeapons is empty — no weapons can be allocated or bought.");
        }

        return warnings;
    }

    private ICollection<string> ValidateDefaultWeapons(CsTeam team)
    {
        var warnings = new List<string>();
        if (!DefaultWeapons.TryGetValue(team, out var defaultWeapons))
        {
            warnings.Add($"Missing {team} in DefaultWeapons config.");
            return warnings;
        }

        if (defaultWeapons.ContainsKey(WeaponAllocationType.Preferred))
        {
            throw new Exception(
                $"Preferred is not a valid default weapon allocation type " +
                $"for config DefaultWeapons.{team}.");
        }

        var allocationTypes = WeaponHelpers.WeaponAllocationTypes;
        allocationTypes.Remove(WeaponAllocationType.Preferred);

        foreach (var allocationType in allocationTypes)
        {
            if (!defaultWeapons.TryGetValue(allocationType, out var w))
            {
                warnings.Add($"Missing {allocationType} in DefaultWeapons.{team} config.");
                continue;
            }

            if (!WeaponHelpers.IsWeapon(w))
            {
                throw new Exception($"{w} is not a valid weapon in config DefaultWeapons.{team}.{allocationType}.");
            }

            if (!UsableWeapons.Contains(w))
            {
                warnings.Add(
                    $"{w} in the DefaultWeapons.{team}.{allocationType} config " +
                    $"is not in the UsableWeapons list.");
            }
        }

        return warnings;
    }

    public double GetRoundTypePercentage(RoundType roundType)
    {
        return Math.Round(RoundTypePercentages[roundType] / 100.0, 2);
    }

    public bool CanPlayersSelectWeapons()
    {
        return AllowedWeaponSelectionTypes.Contains(WeaponSelectionType.PlayerChoice);
    }

    public bool CanAssignRandomWeapons()
    {
        return AllowedWeaponSelectionTypes.Contains(WeaponSelectionType.Random);
    }

    public bool CanAssignDefaultWeapons()
    {
        return AllowedWeaponSelectionTypes.Contains(WeaponSelectionType.Default);
    }
}
