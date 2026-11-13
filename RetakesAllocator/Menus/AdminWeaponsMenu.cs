using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
using RetakesAllocatorCore;
using RetakesAllocatorCore.Config;

namespace RetakesAllocator.Menus;

/// <summary>
/// Chat menu for admins to turn weapons on/off for the whole server (UsableWeapons). Every change
/// is saved to config.json right away, so no FTP access is needed. Pages: category list ->
/// weapons in a category (toggle one, or all at once) / presets.
/// </summary>
public class AdminWeaponsMenu
{
    private readonly WeaponPoolAdmin _admin;

    public AdminWeaponsMenu(WeaponPoolAdmin admin)
    {
        _admin = admin;
    }

    public void OpenMain(CCSPlayerController player)
    {
        var pool = WeaponPoolAdmin.CurrentPool();
        var total = WeaponCatalog.AllCatalogWeapons.Count;
        var enabled = WeaponCatalog.AllCatalogWeapons.Count(pool.Contains);

        var menu = new ChatMenu(Translator.Instance["admin_weapons.menu_title", enabled, total]);
        foreach (var category in WeaponCatalog.Categories)
        {
            var on = category.Weapons.Count(pool.Contains);
            menu.AddMenuOption($"{category.DisplayName} ({on}/{category.Weapons.Count})",
                (p, _) => OpenCategory(p, category));
        }

        menu.AddMenuOption(Translator.Instance["admin_weapons.presets"], (p, _) => OpenPresets(p));
        MenuManager.OpenChatMenu(player, menu);
    }

    private void OpenCategory(CCSPlayerController player, WeaponCatalog.WeaponCategory category)
    {
        var menu = new ChatMenu(Translator.Instance["admin_weapons.category_title", category.DisplayName]);
        foreach (var weapon in category.Weapons)
        {
            var state = WeaponPoolAdmin.IsEnabled(weapon)
                ? Translator.Instance["admin_weapons.on"]
                : Translator.Instance["admin_weapons.off"];
            menu.AddMenuOption($"{state} {WeaponCatalog.GetDisplayName(weapon)}", (p, _) =>
            {
                _admin.SetWeapon(weapon, null, p.PrintToChat);
                OpenCategory(p, category);
            });
        }

        menu.AddMenuOption(Translator.Instance["admin_weapons.enable_all", category.DisplayName], (p, _) =>
        {
            _admin.SetCategory(category, true, p.PrintToChat);
            OpenCategory(p, category);
        });
        menu.AddMenuOption(Translator.Instance["admin_weapons.disable_all", category.DisplayName], (p, _) =>
        {
            _admin.SetCategory(category, false, p.PrintToChat);
            OpenCategory(p, category);
        });
        menu.AddMenuOption(Translator.Instance["admin_weapons.back"], (p, _) => OpenMain(p));

        MenuManager.OpenChatMenu(player, menu);
    }

    private void OpenPresets(CCSPlayerController player)
    {
        var menu = new ChatMenu(Translator.Instance["admin_weapons.presets_title"]);
        foreach (var preset in WeaponCatalog.Presets)
        {
            menu.AddMenuOption($"{preset.Key} - {preset.Description}", (p, _) =>
            {
                _admin.ApplyPreset(preset, p.PrintToChat);
                OpenMain(p);
            });
        }

        menu.AddMenuOption(Translator.Instance["admin_weapons.back"], (p, _) => OpenMain(p));
        MenuManager.OpenChatMenu(player, menu);
    }
}
