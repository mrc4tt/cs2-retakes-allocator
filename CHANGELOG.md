# Changelog

## 2.4.7
- Root admins can change the weapon pool (`UsableWeapons`) without FTP: `!allocator_weapons` opens a chat menu (per category, toggle a weapon or the whole category, presets), and `css_allocator_weapons` / `css_allocator_weapon <weapon> [on|off]` / `css_allocator_preset <name>` work from the console and RCON.
- Short aliases: `!gpool` / `!weaponpool` (menu), `!gpon <weapon...>` and `!gpoff <weapon...>` (several weapons in one go, e.g. `!gpoff ump ssg08 scar20 g3sg1`), `!gpt <weapon> [on|off]`, `!gpreset [name]`. All also work as `css_...` in the console and RCON.
- Presets: `standard` (AK-47, M4A4, M4A1-S, FAMAS, Galil AR, AWP, MAC-10, MP9, common pistols), `nosnipers`, `noauto`, `all`.
- Changes apply immediately and are saved to `config.json`. Only the `UsableWeapons` array is rewritten, so comments and other settings are kept; the previous file is saved as `config.json.bak`.
- The admin is warned when a round type has no weapon left for a team.
- Fix: with `Random` selection and every weapon of a type removed (for example all SMGs), players got Kevlar in that slot instead of falling back to the default weapon.
- `!reload_allocator_config` also refreshes the center gun menu's weapon lists and replies with the config path.

## 2.4.6
- `AutoUpdateSignatures` now only downloads `gamedata/RetakesAllocator.json` when it is missing. It used to replace the file from GitHub main whenever it differed, with no check of the game build, which undid the per-signature fixes CounterStrikeSharp's gamedata updater (mrc4tt fork, `PluginGamedataAutoUpdate`) applies at start-up.
- A gamedata copy left inside the plugin folder by releases before 2.4.3 (`plugins/RetakesAllocator/gamedata/RetakesAllocator_gamedata.json`) is moved to `addons/counterstrikesharp/gamedata/RetakesAllocator.json`, or deleted when that file exists. Nothing read it, and it showed up as broken signatures in CounterStrikeSharp's gamedata status.

## 2.4.5
- Gamedata updated for CS2 build 14192.

## 2.4.4
- The center gun menu sends its HTML only when it changes (or once a second to keep it up), instead of on every tick while open. Buttons still respond every tick.
- The bombsite center announcement (`EnableBombSiteAnnouncementCenter`) is sent once a second instead of every tick, without the per-tick entity scan.
- Both are lighter on players' FPS: each send makes the client re-layout the panel.

## 2.4.3
- First fshost release, based on yonilerner/cs2-retakes-allocator 2.4.3.
- Built against CounterStrikeSharp 1.0.407 (mrc4tt/CounterStrikeSharp).
- Works when loaded with `css_plugins load` (also mid-map): subscribes to the retakes plugin's events without needing `OnAllPluginsLoaded`, retries for 30 seconds if the retakes plugin is not loaded yet, and re-subscribes on map start after the retakes plugin is reloaded.
- Unloads cleanly even when the retakes plugin is already unloaded, and always removes its CanAcquire hook.
- Config lives in `addons/counterstrikesharp/configs/plugins/RetakesAllocator/config.json`, found correctly when the plugin sits in `plugins/disabled/`. Older locations (inside the plugin folder, or the stray `plugins/configs/...`) are migrated automatically.
- Custom gamedata ships as `addons/counterstrikesharp/gamedata/RetakesAllocator.json`; `AutoUpdateSignatures` refreshes that file.
- Ships a patched SQLite 3.50.2 native that loads on the game container's glibc.
- Gun menu no longer rebuilds every tick; weapon commands no longer block the game thread.
