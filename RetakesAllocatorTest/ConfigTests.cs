using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using RetakesAllocatorCore;
using RetakesAllocatorCore.Config;

namespace RetakesAllocatorTest;

public class ConfigTests : BaseTestFixture
{
    [Test]
    public void TestDefaultWeaponsValidation()
    {
        var usableWeapons = WeaponHelpers.AllWeapons;
        usableWeapons.Remove(CsItem.Glock);
        var warnings = Configs.OverrideConfigDataForTests(
            new ConfigData()
            {
                UsableWeapons = usableWeapons,
            }
        ).Validate();
        Assert.That(warnings[0],
            Is.EqualTo(
                "Glock18 in the DefaultWeapons.Terrorist.PistolRound " +
                "config is not in the UsableWeapons list."));

        var defaults =
            new Dictionary<CsTeam, Dictionary<WeaponAllocationType, CsItem>>(Configs.GetConfigData().DefaultWeapons);
        defaults[CsTeam.Terrorist] = new Dictionary<WeaponAllocationType, CsItem>(defaults[CsTeam.Terrorist]);
        defaults[CsTeam.Terrorist].Remove(WeaponAllocationType.FullBuyPrimary);
        warnings = Configs.OverrideConfigDataForTests(
            new ConfigData()
            {
                DefaultWeapons = defaults
            }
        ).Validate();
        Assert.That(warnings[0], Is.EqualTo("Missing FullBuyPrimary in DefaultWeapons.Terrorist config."));

        defaults.Remove(CsTeam.CounterTerrorist);
        warnings = Configs.OverrideConfigDataForTests(
            new ConfigData()
            {
                DefaultWeapons = defaults
            }
        ).Validate();
        Assert.That(warnings[0], Is.EqualTo("Missing FullBuyPrimary in DefaultWeapons.Terrorist config."));
        Assert.That(warnings[1], Is.EqualTo("Missing CounterTerrorist in DefaultWeapons config."));

        defaults[CsTeam.Terrorist][WeaponAllocationType.FullBuyPrimary] = CsItem.Kevlar;
        var error = Assert.Catch(() =>
        {
            Configs.OverrideConfigDataForTests(
                new ConfigData()
                {
                    DefaultWeapons = defaults
                }
            );
        });
        Assert.That(error?.Message,
            Is.EqualTo("Kevlar is not a valid weapon in config DefaultWeapons.Terrorist.FullBuyPrimary."));

        defaults =
            new Dictionary<CsTeam, Dictionary<WeaponAllocationType, CsItem>>(Configs.GetConfigData().DefaultWeapons);
        defaults[CsTeam.Terrorist][WeaponAllocationType.Preferred] = CsItem.AWP;
        error = Assert.Catch(() =>
        {
            Configs.OverrideConfigDataForTests(
                new ConfigData()
                {
                    DefaultWeapons = defaults
                }
            );
        });
        Assert.That(error?.Message, Is.EqualTo(
            "Preferred is not a valid default weapon allocation type for config DefaultWeapons.Terrorist."
        ));
    }

    [Test]
    public void TestShippedTemplateIsValidJsonc()
    {
        // The commented template written on first run must parse with the real
        // deserializer (comments + trailing commas) and pass validation.
        var configData = Configs.ParseConfigDataForTests(Configs.DefaultConfigTemplate);
        Assert.That(configData, Is.Not.Null);
        Assert.That(configData!.Validate(), Is.Empty);
    }

    [Test]
    public void TestLegacyConfigMigratesToNewLocation()
    {
        // Build a self-contained fake install tree so the ../../ hop stays inside temp.
        var root = Path.Combine(Path.GetTempPath(), "ra_migrate_" + Guid.NewGuid().ToString("N"));
        var moduleDir = Path.Combine(root, "addons", "counterstrikesharp", "plugins", "RetakesAllocator");
        var legacyDir = Path.Combine(moduleDir, "config");
        Directory.CreateDirectory(legacyDir);
        var legacyFile = Path.Combine(legacyDir, "config.json");
        var newFile = Path.Combine(
            root, "addons", "counterstrikesharp", "configs", "plugins", "RetakesAllocator", "config.json");

        // A tuned legacy config (with comments) we expect to survive the move untouched.
        var legacyContents = Configs.DefaultConfigTemplate.Replace("\"Retakes\"", "\"MIGRATED_MARKER\"");
        File.WriteAllText(legacyFile, legacyContents);

        try
        {
            var data = Configs.Load(moduleDir, saveAfterLoad: true);

            Assert.That(File.Exists(newFile), Is.True, "config should be moved to the new location");
            Assert.That(File.Exists(legacyFile), Is.False, "legacy file should be removed after the move");
            Assert.That(data.ChatMessagePluginName, Is.EqualTo("MIGRATED_MARKER"),
                "migrated values should be preserved");
            Assert.That(File.ReadAllText(newFile), Does.Contain("COMMON SETUPS"),
                "comments should be preserved verbatim by the move");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void TestStrayPluginsConfigsTreeMigratesAndIsRemoved()
    {
        // An earlier build with a fixed "../.." hop wrote the config to
        // plugins/configs/plugins/RetakesAllocator when the plugin was nested under
        // plugins/disabled/. That config must move to configs/plugins/ and the stray
        // plugins/configs/ tree must be cleaned up behind it.
        var root = Path.Combine(Path.GetTempPath(), "ra_stray_" + Guid.NewGuid().ToString("N"));
        var cssDir = Path.Combine(root, "addons", "counterstrikesharp");
        var moduleDir = Path.Combine(cssDir, "plugins", "disabled", "RetakesAllocator");
        var strayRoot = Path.Combine(cssDir, "plugins", "configs");
        var strayDir = Path.Combine(strayRoot, "plugins", "RetakesAllocator");
        Directory.CreateDirectory(moduleDir);
        Directory.CreateDirectory(strayDir);

        File.WriteAllText(Path.Combine(strayDir, "config.json"),
            Configs.DefaultConfigTemplate.Replace("\"Retakes\"", "\"STRAY_MARKER\""));

        try
        {
            var data = Configs.Load(moduleDir, saveAfterLoad: true);

            Assert.That(data.ChatMessagePluginName, Is.EqualTo("STRAY_MARKER"),
                "the stray config's values should be preserved by the migration");
            Assert.That(File.Exists(Path.Combine(cssDir, "configs", "plugins", "RetakesAllocator", "config.json")), Is.True,
                "config should end up in the CSS-standard configs/plugins location");
            Assert.That(Directory.Exists(strayRoot), Is.False,
                "the whole stray plugins/configs tree should be removed");
            Assert.That(Directory.Exists(Path.Combine(cssDir, "plugins")), Is.True,
                "the real plugins directory must survive pruning");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void TestPruningStopsAtSharedCssDirectories()
    {
        // Migrating out of the plugin folder removes the emptied config/ directory, but must
        // not touch the plugin folder itself or any other plugin's config.
        var root = Path.Combine(Path.GetTempPath(), "ra_prune_" + Guid.NewGuid().ToString("N"));
        var cssDir = Path.Combine(root, "addons", "counterstrikesharp");
        var moduleDir = Path.Combine(cssDir, "plugins", "disabled", "RetakesAllocator");
        var cssConfigsPlugins = Path.Combine(cssDir, "configs", "plugins");
        var otherPluginDir = Path.Combine(cssConfigsPlugins, "SomeOtherPlugin");
        var ourLegacyDir = Path.Combine(moduleDir, "config");
        Directory.CreateDirectory(moduleDir);
        Directory.CreateDirectory(otherPluginDir);
        Directory.CreateDirectory(ourLegacyDir);

        File.WriteAllText(Path.Combine(otherPluginDir, "SomeOtherPlugin.json"), "{}");
        File.WriteAllText(Path.Combine(ourLegacyDir, "config.json"), Configs.DefaultConfigTemplate);

        try
        {
            Configs.Load(moduleDir, saveAfterLoad: true);

            Assert.That(Directory.Exists(ourLegacyDir), Is.False,
                "our own now-empty legacy directory should be removed");
            Assert.That(Directory.Exists(moduleDir), Is.True,
                "the plugin folder itself must survive pruning");
            Assert.That(File.Exists(Path.Combine(cssConfigsPlugins, "RetakesAllocator", "config.json")), Is.True,
                "config should be moved to configs/plugins/RetakesAllocator");
            Assert.That(Directory.Exists(cssConfigsPlugins), Is.True,
                "configs/plugins still holds another plugin and must survive");
            Assert.That(File.Exists(Path.Combine(otherPluginDir, "SomeOtherPlugin.json")), Is.True,
                "another plugin's config must not be touched");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void TestOnlyDefaultSelectionWarns()
    {
        var warnings = Configs.OverrideConfigDataForTests(
            new ConfigData()
            {
                AllowedWeaponSelectionTypes = new() { WeaponSelectionType.Default },
            }
        ).Validate();
        Assert.That(warnings, Has.Some.Contains("only contains 'Default'"));

        warnings = Configs.OverrideConfigDataForTests(
            new ConfigData()
            {
                AllowedWeaponSelectionTypes = new(),
            }
        ).Validate();
        Assert.That(warnings, Has.Some.Contains("AllowedWeaponSelectionTypes is empty"));
    }
}
