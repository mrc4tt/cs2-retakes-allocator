// Stolen from https://github.com/B3none/cs2-retakes/blob/014663222fa95bb9f506284814ae62205630416c/Modules/Translator.cs

using System.Globalization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Localization;

namespace RetakesAllocatorCore;

public class Translator
{
    private static Translator? _instance;

    public static Translator Initialize(IStringLocalizer localizer)
    {
        _instance = new(localizer);
        return _instance;
    }

    public static bool IsInitialized => _instance is not null;

    public static Translator Instance => _instance ?? throw new Exception("Translator is not initialized.");
    
    private IStringLocalizer _stringLocalizerImplementation;

    public Translator(IStringLocalizer localizer)
    {
        _stringLocalizerImplementation = localizer;
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        return _stringLocalizerImplementation.GetAllStrings(includeParentCultures);
    }

    public string this[string name] => Translate(name);

    /// <summary>Translate into the player's own language (server language for null, bots and HLTV).</summary>
    public string ForPlayer(CCSPlayerController? player, string name, params object[] arguments)
    {
        using (PlayerScope(player))
        {
            return Translate(name, arguments);
        }
    }

    /// <summary>
    /// Every translation made while the returned scope is alive (Translator and the plugin Localizer)
    /// uses the player's language. Null, bots and HLTV keep the server language. Game thread only.
    /// </summary>
    public static IDisposable PlayerScope(CCSPlayerController? player)
    {
        if (player is null || !player.IsValid || player.IsBot || player.IsHLTV)
        {
            return NoScope.Instance;
        }

        return new WithTemporaryCulture(player.GetLanguage());
    }

    /// <summary>Same as PlayerScope, for a culture captured earlier (safe off the game thread).</summary>
    public static IDisposable CultureScope(CultureInfo culture) => new WithTemporaryCulture(culture);

    /// <summary>Run an action for every human player (no bots, no HLTV) inside that player's language scope.</summary>
    public static void ForEachHuman(Action<CCSPlayerController> action)
    {
        foreach (var player in Utilities.GetPlayers())
        {
            if (!player.IsValid || player.IsBot || player.IsHLTV)
            {
                continue;
            }

            using (PlayerScope(player))
            {
                action(player);
            }
        }
    }

    private sealed class NoScope : IDisposable
    {
        public static readonly NoScope Instance = new();
        public void Dispose() { }
    }

    public string this[string name, params object[] arguments] => Translate(name, arguments);

    private string Translate(string key, params object[] arguments)
    {
        var isCenter = key.StartsWith("center.");
        key = key.Replace("center.", "");
        
        var localizedString = _stringLocalizerImplementation[key, arguments];

        if (localizedString == null || localizedString.ResourceNotFound)
        {
            return key;
        }

        return isCenter ? localizedString.Value : Color(localizedString.Value);
    }

    public static string Color(string text)
    {
        return text
            .Replace("[GREEN]", ChatColors.Green.ToString())
            .Replace("[RED]", ChatColors.Red.ToString())
            .Replace("[YELLOW]", ChatColors.Yellow.ToString())
            .Replace("[BLUE]", ChatColors.Blue.ToString())
            .Replace("[PURPLE]", ChatColors.Purple.ToString())
            .Replace("[ORANGE]", ChatColors.Orange.ToString())
            .Replace("[WHITE]", ChatColors.White.ToString())
            .Replace("[NORMAL]", ChatColors.White.ToString())
            .Replace("[GREY]", ChatColors.Grey.ToString())
            .Replace("[LIGHT_RED]", ChatColors.LightRed.ToString())
            .Replace("[LIGHT_BLUE]", ChatColors.LightBlue.ToString())
            .Replace("[LIGHT_PURPLE]", ChatColors.LightPurple.ToString())
            .Replace("[LIGHT_YELLOW]", ChatColors.LightYellow.ToString())
            .Replace("[DARK_RED]", ChatColors.DarkRed.ToString())
            .Replace("[DARK_BLUE]", ChatColors.DarkBlue.ToString())
            .Replace("[BLUE_GREY]", ChatColors.BlueGrey.ToString())
            .Replace("[OLIVE]", ChatColors.Olive.ToString())
            .Replace("[LIME]", ChatColors.Lime.ToString())
            .Replace("[GOLD]", ChatColors.Gold.ToString())
            .Replace("[SILVER]", ChatColors.Silver.ToString())
            .Replace("[MAGENTA]", ChatColors.Magenta.ToString());
    }
}
