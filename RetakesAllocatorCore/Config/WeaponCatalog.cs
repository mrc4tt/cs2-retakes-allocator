using CounterStrikeSharp.API.Modules.Entities.Constants;

namespace RetakesAllocatorCore.Config;

/// <summary>
/// The admin-facing view of every gun the plugin can hand out: grouped into the same categories as
/// the UsableWeapons block of the shipped config template, with the names that block uses (so a
/// rewritten config looks like the original) and readable display names for menus and chat.
/// Also holds the weapon-pool presets used by css_allocator_preset and the admin menu.
/// </summary>
public static class WeaponCatalog
{
    public record WeaponCategory(string Key, string DisplayName, IReadOnlyList<CsItem> Weapons);

    public record WeaponPreset(string Key, string Description, IReadOnlyList<CsItem> Weapons);

    // Order and grouping match the UsableWeapons block of Configs.DefaultConfigTemplate.
    public static readonly IReadOnlyList<WeaponCategory> Categories = new List<WeaponCategory>
    {
        new("pistols", "Pistols", new[]
        {
            CsItem.Deagle, CsItem.Glock, CsItem.USPS, CsItem.HKP2000, CsItem.Elite, CsItem.Tec9, CsItem.P250,
            CsItem.CZ, CsItem.FiveSeven, CsItem.Revolver,
        }),
        new("smgs", "SMGs", new[]
        {
            CsItem.Mac10, CsItem.MP9, CsItem.MP7, CsItem.P90, CsItem.MP5SD, CsItem.Bizon, CsItem.UMP45,
        }),
        new("shotguns", "Shotguns", new[]
        {
            CsItem.XM1014, CsItem.Nova, CsItem.MAG7, CsItem.SawedOff,
        }),
        new("heavy", "Machine guns", new[]
        {
            CsItem.M249, CsItem.Negev,
        }),
        new("rifles", "Rifles", new[]
        {
            CsItem.AK47, CsItem.M4A1S, CsItem.M4A4, CsItem.GalilAR, CsItem.Famas, CsItem.SG556, CsItem.AUG,
        }),
        new("snipers", "Snipers", new[]
        {
            CsItem.AWP, CsItem.SSG08, CsItem.SCAR20, CsItem.G3SG1,
        }),
    };

    // CsItem has several names per value (Glock/Glock18, M4A1/M4A4, ...). Enum.ToString picks one
    // arbitrarily, so the names written to config.json are pinned here to the template's spelling.
    private static readonly Dictionary<CsItem, string> ConfigNames = new()
    {
        {CsItem.Deagle, "Deagle"}, {CsItem.Glock, "Glock"}, {CsItem.USPS, "USPS"}, {CsItem.HKP2000, "HKP2000"},
        {CsItem.Elite, "Elite"}, {CsItem.Tec9, "Tec9"}, {CsItem.P250, "P250"}, {CsItem.CZ, "CZ"},
        {CsItem.FiveSeven, "FiveSeven"}, {CsItem.Revolver, "Revolver"},
        {CsItem.Mac10, "Mac10"}, {CsItem.MP9, "MP9"}, {CsItem.MP7, "MP7"}, {CsItem.P90, "P90"},
        {CsItem.MP5SD, "MP5SD"}, {CsItem.Bizon, "Bizon"}, {CsItem.UMP45, "UMP45"},
        {CsItem.XM1014, "XM1014"}, {CsItem.Nova, "Nova"}, {CsItem.MAG7, "MAG7"}, {CsItem.SawedOff, "SawedOff"},
        {CsItem.M249, "M249"}, {CsItem.Negev, "Negev"},
        {CsItem.AK47, "AK47"}, {CsItem.M4A1S, "M4A1S"}, {CsItem.M4A4, "M4A1"}, {CsItem.GalilAR, "GalilAR"},
        {CsItem.Famas, "Famas"}, {CsItem.SG556, "SG556"}, {CsItem.AUG, "AUG"},
        {CsItem.AWP, "AWP"}, {CsItem.SSG08, "SSG08"}, {CsItem.SCAR20, "SCAR20"}, {CsItem.G3SG1, "G3SG1"},
    };

    private static readonly Dictionary<CsItem, string> DisplayNames = new()
    {
        {CsItem.Deagle, "Desert Eagle"}, {CsItem.Glock, "Glock-18"}, {CsItem.USPS, "USP-S"},
        {CsItem.HKP2000, "P2000"}, {CsItem.Elite, "Dual Berettas"}, {CsItem.Tec9, "Tec-9"},
        {CsItem.P250, "P250"}, {CsItem.CZ, "CZ75-Auto"}, {CsItem.FiveSeven, "Five-SeveN"},
        {CsItem.Revolver, "R8 Revolver"},
        {CsItem.Mac10, "MAC-10"}, {CsItem.MP9, "MP9"}, {CsItem.MP7, "MP7"}, {CsItem.P90, "P90"},
        {CsItem.MP5SD, "MP5-SD"}, {CsItem.Bizon, "PP-Bizon"}, {CsItem.UMP45, "UMP-45"},
        {CsItem.XM1014, "XM1014"}, {CsItem.Nova, "Nova"}, {CsItem.MAG7, "MAG-7"}, {CsItem.SawedOff, "Sawed-Off"},
        {CsItem.M249, "M249"}, {CsItem.Negev, "Negev"},
        {CsItem.AK47, "AK-47"}, {CsItem.M4A1S, "M4A1-S"}, {CsItem.M4A4, "M4A4"}, {CsItem.GalilAR, "Galil AR"},
        {CsItem.Famas, "FAMAS"}, {CsItem.SG556, "SG 553"}, {CsItem.AUG, "AUG"},
        {CsItem.AWP, "AWP"}, {CsItem.SSG08, "SSG 08"}, {CsItem.SCAR20, "SCAR-20"}, {CsItem.G3SG1, "G3SG1"},
    };

    public static IReadOnlyList<CsItem> AllCatalogWeapons { get; } =
        Categories.SelectMany(c => c.Weapons).ToList();

    private static readonly CsItem[] StandardWeapons =
    {
        // Pistols
        CsItem.Deagle, CsItem.Glock, CsItem.USPS, CsItem.HKP2000, CsItem.Tec9, CsItem.P250, CsItem.FiveSeven,
        // SMGs for half-buy rounds (one per team, so those rounds still get a primary)
        CsItem.Mac10, CsItem.MP9,
        // Rifles
        CsItem.AK47, CsItem.M4A1S, CsItem.M4A4, CsItem.GalilAR, CsItem.Famas,
        // Sniper
        CsItem.AWP,
    };

    public static readonly IReadOnlyList<WeaponPreset> Presets = new List<WeaponPreset>
    {
        new("standard",
            "AK-47, M4A4, M4A1-S, FAMAS, Galil, AWP, MAC-10/MP9 and the common pistols",
            StandardWeapons),
        new("nosnipers",
            "Everything except AWP, SSG 08 and the auto-snipers",
            AllCatalogWeapons.Where(w => !Categories.Single(c => c.Key == "snipers").Weapons.Contains(w)).ToList()),
        new("noauto",
            "Everything except the auto-snipers (SCAR-20, G3SG1)",
            AllCatalogWeapons.Where(w => w is not (CsItem.SCAR20 or CsItem.G3SG1)).ToList()),
        new("all",
            "Every weapon (the plugin default)",
            AllCatalogWeapons),
    };

    public static string GetConfigName(CsItem item) =>
        ConfigNames.TryGetValue(item, out var name) ? name : item.ToString();

    public static string GetDisplayName(CsItem item) =>
        DisplayNames.TryGetValue(item, out var name) ? name : item.ToString();

    public static WeaponCategory? FindCategory(string key) =>
        Categories.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.OrdinalIgnoreCase));

    public static WeaponPreset? FindPreset(string key) =>
        Presets.FirstOrDefault(p => string.Equals(p.Key, Normalize(key), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Matches a weapon typed by an admin: config name ("UMP45"), display name ("UMP-45"),
    /// any CsItem alias ("Scout", "M4A4"), case- and punctuation-insensitive. Returns null when the
    /// input is not exactly one catalog weapon.
    /// </summary>
    public static CsItem? ParseWeapon(string input)
    {
        var needle = Normalize(input);
        if (needle.Length == 0)
        {
            return null;
        }

        foreach (var weapon in AllCatalogWeapons)
        {
            if (Normalize(GetConfigName(weapon)) == needle || Normalize(GetDisplayName(weapon)) == needle)
            {
                return weapon;
            }
        }

        foreach (var name in Enum.GetNames<CsItem>())
        {
            if (Normalize(name) == needle)
            {
                var item = Enum.Parse<CsItem>(name);
                return AllCatalogWeapons.Contains(item) ? item : null;
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the weapons in catalog order (the order they are written to config.json),
    /// without duplicates, followed by any non-catalog weapons that were passed in.
    /// </summary>
    public static List<CsItem> SortForConfig(IEnumerable<CsItem> weapons)
    {
        var set = weapons.ToHashSet();
        var ordered = AllCatalogWeapons.Where(set.Contains).ToList();
        ordered.AddRange(set.Where(w => !AllCatalogWeapons.Contains(w)).OrderBy(w => (int)w));
        return ordered;
    }

    private static string Normalize(string s) =>
        new string(s.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
}
