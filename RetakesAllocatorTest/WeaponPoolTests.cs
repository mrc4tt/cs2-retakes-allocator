using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using RetakesAllocatorCore;
using RetakesAllocatorCore.Config;

namespace RetakesAllocatorTest;

public class WeaponPoolTests : BaseTestFixture
{
    [Test]
    public void CatalogCoversEveryWeapon()
    {
        Assert.That(WeaponCatalog.AllCatalogWeapons, Is.EquivalentTo(WeaponHelpers.AllWeapons));
        Assert.That(WeaponCatalog.AllCatalogWeapons, Is.Unique);
    }

    [Test]
    public void TemplateUsableWeaponsMatchCatalogOrder()
    {
        var template = Configs.ParseConfigDataForTests(Configs.DefaultConfigTemplate)!;
        Assert.That(template.UsableWeapons, Is.EqualTo(WeaponCatalog.AllCatalogWeapons));
    }

    [TestCase("UMP45", CsItem.UMP45)]
    [TestCase("ump-45", CsItem.UMP45)]
    [TestCase("ump", CsItem.UMP45)]
    [TestCase("SSG 08", CsItem.SSG08)]
    [TestCase("scout", CsItem.SSG08)]
    [TestCase("M4A4", CsItem.M4A4)]
    [TestCase("M4A1", CsItem.M4A4)]
    [TestCase("m4a1-s", CsItem.M4A1S)]
    [TestCase("ak-47", CsItem.AK47)]
    [TestCase("kevlar", null)]
    [TestCase("nope", null)]
    [TestCase("", null)]
    public void ParseWeapon(string input, CsItem? expected)
    {
        Assert.That(WeaponCatalog.ParseWeapon(input), Is.EqualTo(expected));
    }

    [Test]
    public void RewriteTemplateKeepsCommentsAndOtherSettings()
    {
        var pool = new[] {CsItem.AK47, CsItem.M4A1S, CsItem.AWP, CsItem.Glock, CsItem.USPS};
        var result = UsableWeaponsWriter.Rewrite(Configs.DefaultConfigTemplate, pool);

        var parsed = Configs.ParseConfigDataForTests(result)!;
        Assert.That(parsed.UsableWeapons, Is.EquivalentTo(pool));

        // Everything outside the array is untouched.
        var original = Configs.ParseConfigDataForTests(Configs.DefaultConfigTemplate)!;
        Assert.That(parsed.DefaultWeapons, Is.EqualTo(original.DefaultWeapons));
        Assert.That(parsed.AllowedWeaponSelectionTypes, Is.EqualTo(original.AllowedWeaponSelectionTypes));
        Assert.That(result, Does.Contain("RETAKES ALLOCATOR CONFIG"));
        Assert.That(result, Does.Contain("// │ 2) AllowedWeaponSelectionTypes"));

        var before = Configs.DefaultConfigTemplate.Split("\"UsableWeapons\": [")[0];
        Assert.That(result, Does.StartWith(before));
        Assert.That(result, Does.Contain("\"UsableWeapons\": [\n    // Pistols\n    \"Glock\", \"USPS\","));
        Assert.That(result, Does.Contain("    // Snipers\n    \"AWP\"\n  ],"));
    }

    [Test]
    public void RewriteFullPoolIsIdempotent()
    {
        var once = UsableWeaponsWriter.Rewrite(Configs.DefaultConfigTemplate, WeaponCatalog.AllCatalogWeapons);
        var twice = UsableWeaponsWriter.Rewrite(once, WeaponCatalog.AllCatalogWeapons);
        Assert.That(twice, Is.EqualTo(once));
        Assert.That(Configs.ParseConfigDataForTests(once)!.UsableWeapons,
            Is.EqualTo(WeaponCatalog.AllCatalogWeapons));
    }

    [Test]
    public void RewriteSkipsNestedPropertyWithSameName()
    {
        const string json = """
                            {
                              // comment with "UsableWeapons": [ inside ]
                              "Nested": { "UsableWeapons": ["AWP"] },
                              "UsableWeapons": /* c */ ["AK47"],
                              "Tail": 1,
                            }
                            """;
        var result = UsableWeaponsWriter.Rewrite(json, new[] {CsItem.M4A1S});
        Assert.That(result, Does.Contain("\"Nested\": { \"UsableWeapons\": [\"AWP\"] },"));
        Assert.That(result, Does.Contain("// comment with \"UsableWeapons\": [ inside ]"));
        Assert.That(result, Does.Contain("/* c */ [\n"));
        Assert.That(result, Does.Contain("\"M4A1S\""));
        Assert.That(result, Does.Not.Contain("\"AK47\""));
        Assert.That(result, Does.Contain("\"Tail\": 1,"));
    }

    [Test]
    public void RewriteAddsMissingPropertyAndKeepsCrlfAndBom()
    {
        var json = "﻿{\r\n  \"ChatMessagePluginName\": \"X\"\r\n}\r\n";
        var result = UsableWeaponsWriter.Rewrite(json, new[] {CsItem.AK47});
        Assert.That(result, Does.StartWith("﻿{\r\n  \"UsableWeapons\": [\r\n    // Rifles\r\n    \"AK47\"\r\n  ],"));
        var parsed = Configs.ParseConfigDataForTests(result.TrimStart('﻿'))!;
        Assert.That(parsed.UsableWeapons, Is.EqualTo(new[] {CsItem.AK47}));
        Assert.That(parsed.ChatMessagePluginName, Is.EqualTo("X"));
    }

    [Test]
    public void RewriteEmptyPool()
    {
        var result = UsableWeaponsWriter.Rewrite(Configs.DefaultConfigTemplate, Array.Empty<CsItem>());
        Assert.That(Configs.ParseConfigDataForTests(result)!.UsableWeapons, Is.Empty);
    }

    [Test]
    public void SaveUsableWeaponsWritesFileAndAppliesLive()
    {
        var path = Configs.ConfigFilePath!;
        var original = File.Exists(path) ? File.ReadAllText(path) : null;
        try
        {
            var preset = WeaponCatalog.FindPreset("standard")!;
            Configs.SaveUsableWeapons(preset.Weapons);

            Assert.That(Configs.GetConfigData().UsableWeapons, Is.EquivalentTo(preset.Weapons));
            // Only the UsableWeapons array changed; the rest of the file is byte-for-byte the same.
            Assert.That(File.ReadAllText(path),
                Is.EqualTo(UsableWeaponsWriter.Rewrite(original ?? Configs.DefaultConfigTemplate, preset.Weapons)));
            Assert.That(File.ReadAllText(path), Does.Not.Contain("\"UMP45\""));
            Assert.That(File.Exists(path + ".bak"), Is.True);
            Assert.That(File.Exists(path + ".tmp"), Is.False);

            Configs.Load(".");
            Assert.That(Configs.GetConfigData().UsableWeapons, Is.EquivalentTo(preset.Weapons));
        }
        finally
        {
            if (original is not null)
            {
                File.WriteAllText(path, original);
            }
            else
            {
                File.Delete(path);
            }

            File.Delete(path + ".bak");
            Configs.Load(".");
        }
    }

    [TestCase("standard")]
    [TestCase("nosnipers")]
    [TestCase("noauto")]
    [TestCase("all")]
    public void PresetsLeaveNoEmptyRoundSlot(string presetKey)
    {
        var preset = WeaponCatalog.FindPreset(presetKey)!;
        Configs.OverrideConfigDataForTests(new ConfigData {UsableWeapons = preset.Weapons.ToList()});
        foreach (var team in new[] {CsTeam.Terrorist, CsTeam.CounterTerrorist})
        {
            foreach (var slot in new[]
                     {
                         WeaponAllocationType.PistolRound, WeaponAllocationType.Secondary,
                         WeaponAllocationType.HalfBuyPrimary, WeaponAllocationType.FullBuyPrimary,
                     })
            {
                Assert.That(WeaponHelpers.GetPossibleWeaponsForAllocationType(slot, team), Is.Not.Empty,
                    $"{presetKey}: {team} {slot}");
            }
        }
    }

    [Test]
    public void StandardPresetExcludesRequestedWeapons()
    {
        var standard = WeaponCatalog.FindPreset("Standard")!.Weapons;
        Assert.That(standard, Does.Contain(CsItem.AK47).And.Contain(CsItem.M4A1S).And.Contain(CsItem.AWP));
        Assert.That(standard, Does.Not.Contain(CsItem.UMP45).And.Not.Contain(CsItem.SSG08));
    }

    [Test]
    public void RandomWithEmptyCategoryFallsBackInsteadOfKevlar()
    {
        // Only rifles + pistols allowed: half-buy has no SMG for Random to pick.
        Configs.OverrideConfigDataForTests(new ConfigData
        {
            UsableWeapons = new List<CsItem> {CsItem.AK47, CsItem.M4A1S, CsItem.Glock, CsItem.USPS, CsItem.Deagle},
            AllowedWeaponSelectionTypes = new List<WeaponSelectionType> {WeaponSelectionType.Random},
        });

        for (var i = 0; i < 20; i++)
        {
            var weapons = WeaponHelpers.GetWeaponsForRoundType(RoundType.HalfBuy, CsTeam.Terrorist, null, false);
            Assert.That(weapons, Does.Not.Contain(CsItem.Kevlar));
            Assert.That(weapons.All(WeaponHelpers.IsWeapon), Is.True);
        }
    }
}
