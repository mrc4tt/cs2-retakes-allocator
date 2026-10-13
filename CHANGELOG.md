# Changelog

## 2.4.3
- First fshost release, based on yonilerner/cs2-retakes-allocator 2.4.3.
- Built against CounterStrikeSharp 1.0.407 (mrc4tt/CounterStrikeSharp).
- Works when loaded with `css_plugins load` (also mid-map): subscribes to the retakes plugin's events without needing `OnAllPluginsLoaded`, retries for 30 seconds if the retakes plugin is not loaded yet, and re-subscribes on map start after the retakes plugin is reloaded.
- Unloads cleanly even when the retakes plugin is already unloaded, and always removes its CanAcquire hook.
- Config lives in `addons/counterstrikesharp/configs/plugins/RetakesAllocator/config.json`, found correctly when the plugin sits in `plugins/disabled/`. Older locations (inside the plugin folder, or the stray `plugins/configs/...`) are migrated automatically.
- Custom gamedata ships as `addons/counterstrikesharp/gamedata/RetakesAllocator.json`; `AutoUpdateSignatures` refreshes that file.
- Ships a patched SQLite 3.50.2 native that loads on the game container's glibc.
- Gun menu no longer rebuilds every tick; weapon commands no longer block the game thread.
