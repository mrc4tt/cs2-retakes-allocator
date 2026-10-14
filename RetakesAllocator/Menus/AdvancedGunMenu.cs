using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Utils;
using RetakesAllocatorCore;
using CounterStrikeSharp.API;
using System.Text;
using RetakesAllocator.Menus;
using RetakesAllocatorCore.Config;
using static RetakesAllocatorCore.PluginInfo;
using CounterStrikeSharp.API.Modules.Events;

namespace RetakesAllocator.AdvancedMenus;

public class AdvancedGunMenu
{
    public Dictionary<ulong, bool> menuon = new Dictionary<ulong, bool>();
    public Dictionary<ulong, int> mainmenu = new Dictionary<ulong, int>();
    public Dictionary<ulong, int> currentIndexDict = new Dictionary<ulong, int>();
    public Dictionary<ulong, bool> buttonPressed = new Dictionary<ulong, bool>();

    // SteamID -> player slot, recorded when the menu is opened. OnTick used to run a full
    // FindAllEntitiesByDesignerName("cs_player_controller") scan every tick to turn the
    // menuon keys back into controllers; the slot is already known at open time, so the
    // scan was pure waste. GetPlayerFromSlot is an O(1) index lookup.
    private readonly Dictionary<ulong, int> _slotById = new();

    // Reused across ticks so the draw loop allocates nothing steady-state.
    private readonly List<ulong> _tickIds = new();
    private readonly StringBuilder _builder = new();

    // Cached weapon-name lists. They depend only on the allocation config, so they are
    // identical for every player and every tick — building them 8x per player per tick
    // (the old behaviour) is what caused the multi-hundred-ms frame spikes. Built once on
    // first use; call InvalidateWeaponCache() if the allocation config is reloaded at runtime.
    private string[]? _tFullBuy, _tSecondary, _tHalfBuy, _tPistolRound;
    private string[]? _ctFullBuy, _ctSecondary, _ctHalfBuy, _ctPistolRound;

    private static string[] BuildWeaponList(WeaponAllocationType type, CsTeam team)
    {
        var list = new List<string>();
        foreach (var weapon in WeaponHelpers.GetPossibleWeaponsForAllocationType(type, team))
        {
            list.Add(weapon.GetName());
        }
        return list.ToArray();
    }

    private void EnsureWeaponCache()
    {
        if (_tFullBuy != null) return;
        _tFullBuy      = BuildWeaponList(WeaponAllocationType.FullBuyPrimary, CsTeam.Terrorist);
        _tSecondary    = BuildWeaponList(WeaponAllocationType.Secondary, CsTeam.Terrorist);
        _tHalfBuy      = BuildWeaponList(WeaponAllocationType.HalfBuyPrimary, CsTeam.Terrorist);
        _tPistolRound  = BuildWeaponList(WeaponAllocationType.PistolRound, CsTeam.Terrorist);
        _ctFullBuy     = BuildWeaponList(WeaponAllocationType.FullBuyPrimary, CsTeam.CounterTerrorist);
        _ctSecondary   = BuildWeaponList(WeaponAllocationType.Secondary, CsTeam.CounterTerrorist);
        _ctHalfBuy     = BuildWeaponList(WeaponAllocationType.HalfBuyPrimary, CsTeam.CounterTerrorist);
        _ctPistolRound = BuildWeaponList(WeaponAllocationType.PistolRound, CsTeam.CounterTerrorist);
    }

    // Cached menu chrome. Translator.Instance[...] takes no player argument, so every one of
    // these is server-global and identical on every tick for every player. The old code did
    // ~30 dictionary lookups and allocated 4 string[] per player per tick; at 64 tick with a
    // handful of menus open that is thousands of gen0 allocations a second, which is what
    // turned into GC pauses and choke. Built once; InvalidateWeaponCache() drops them too so
    // a runtime config/translation reload still takes effect.
    private string? _imageLeft, _imageRight, _bottomMenu, _bottomMenuOnPistol;
    private string[]? _main, _tLoadout, _ctLoadout, _awp;

    private static string Text(string key, string fallback = "")
    {
        var value = Translator.Instance[key];
        return string.IsNullOrEmpty(value) ? fallback : value;
    }

    private void EnsureTextCache()
    {
        if (_main != null) return;
        _imageLeft          = Text("menu.left.image");
        _imageRight         = Text("menu.right.image");
        _bottomMenu         = Text("menu.bottom.text");
        _bottomMenuOnPistol = Text("menu.bottom.text.pistol");

        _main = new[]
        {
            Text("menu.main.tloadout", "█░ T Loadout ░█"),
            Text("menu.main.ctloadout", "█░ CT Loadout ░█"),
            Text("menu.main.awp", "█░ AWP ░█"),
        };

        _tLoadout = new[]
        {
            Text("menu.tprimary", "█ T Primary █"),
            Text("menu.tsecondary", "█ T Secondary █"),
            Text("menu.tPistol", "█ T Pistol Round █"),
            Text("menu.tHalfbuy", "█ T Half Buy █"),
        };

        _ctLoadout = new[]
        {
            Text("menu.ctprimary", "█ CT Primary █"),
            Text("menu.ctsecondary", "█ CT Secondary █"),
            Text("menu.ctPistol", "█ CT Pistol Round █"),
            Text("menu.ctHalfbuy", "█ CT Half Buy █"),
        };

        _awp = new[]
        {
            Text("menu.awp.always", "Always"),
            Text("menu.awp.never", "Never"),
        };
    }

    public void InvalidateWeaponCache()
    {
        _tFullBuy = null;
        _main = null;
    }

    // The menu HTML was sent on every tick (64 times a second) while open, even when nothing changed. Each
    // send makes the client's Panorama re-layout the panel, which costs client FPS. Buttons are still read
    // every tick; the HTML is only sent when it changed or as a 1s keepalive (it stays up 5s per send).
    private readonly Dictionary<ulong, (string Html, float SentAt)> _lastMenuHtml = new();
    private const float MenuKeepAliveSeconds = 1.0f;

    private void SendMenuHtml(CCSPlayerController player, ulong playerid, string html)
    {
        var now = Server.CurrentTime;
        if (_lastMenuHtml.TryGetValue(playerid, out var last)
            && last.Html == html
            && now >= last.SentAt
            && now - last.SentAt < MenuKeepAliveSeconds)
        {
            return;
        }

        player.PrintToCenterHtml(html);
        _lastMenuHtml[playerid] = (html, now);
    }

    private void ForgetPlayer(ulong playerid)
    {
        _lastMenuHtml.Remove(playerid);
        menuon.Remove(playerid);
        mainmenu.Remove(playerid);
        currentIndexDict.Remove(playerid);
        buttonPressed.Remove(playerid);
        _slotById.Remove(playerid);
    }

    private static void Print(CCSPlayerController player, string message)
    {
        Helpers.WriteNewlineDelimited(message, player.PrintToChat);
    }
    public HookResult OnEventPlayerChat(EventPlayerChat @event, GameEventInfo info)
    {
        if(@event == null)return HookResult.Continue;
        var eventplayer = @event.Userid;
        var eventmessage = @event.Text;
        var player = Utilities.GetPlayerFromUserid(eventplayer);
        
        if (player == null || !player.IsValid)return HookResult.Continue;
        var playerid = player.SteamID;

        if (string.IsNullOrWhiteSpace(eventmessage)) return HookResult.Continue;
        string trimmedMessageStart = eventmessage.TrimStart();
        string message = trimmedMessageStart.TrimEnd();
        string[] CenterMenuCommands = Configs.GetConfigData().InGameGunMenuCenterCommands.Split(',');

        if (CenterMenuCommands.Any(cmd => cmd.Equals(message, StringComparison.OrdinalIgnoreCase)))
        {
            if (!menuon.ContainsKey(playerid))
            {
                menuon.Add(playerid, true);
            }
            if (!mainmenu.ContainsKey(playerid))
            {
                mainmenu.Add(playerid, 0);
            }
            if (!currentIndexDict.ContainsKey(playerid))
            {
                currentIndexDict.Add(playerid, 0);
            }
            if (!buttonPressed.ContainsKey(playerid))
            {
                buttonPressed.Add(playerid, false);
            }
            _slotById[playerid] = player.Slot;
        }
        return HookResult.Continue;
    }

    public void OnTick()
    {
        // Nobody has the menu open -> nothing to draw. Bail before the (per-tick) entity
        // scan so the menu costs zero when unused.
        if (menuon.Count == 0) return;

        EnsureTextCache();
        EnsureWeaponCache();

        string Imageleft = _imageLeft!;
        string ImageRight = _imageRight!;
        string BottomMenu = _bottomMenu!;
        string BottomMenuOnpistol = _bottomMenuOnPistol!;
        string[] Main = _main!;
        string[] Tloadout = _tLoadout!;
        string[] CTloadout = _ctLoadout!;
        string[] AWP = _awp!;

        // Snapshot the ids: the draw loop below closes menus (and so mutates menuon) while
        // it runs, which would invalidate a live enumerator.
        _tickIds.Clear();
        foreach (var id in menuon.Keys) _tickIds.Add(id);

        foreach (var playerid in _tickIds)
        {
            if (!_slotById.TryGetValue(playerid, out var slot)) continue;

            var player = Utilities.GetPlayerFromSlot(slot);
            if (player == null || !player.IsValid || player.SteamID != playerid)
            {
                // Slot recycled by a different player (or now empty) -> stale entry.
                ForgetPlayer(playerid);
                continue;
            }
            if (!player.PawnIsAlive || player.IsBot || player.IsHLTV) continue;

            if (menuon.ContainsKey(playerid))
            {
                string[] TFullBuy = _tFullBuy!;
                string[] TSecondary = _tSecondary!;
                string[] THalfBuy = _tHalfBuy!;
                string[] TPistolRound = _tPistolRound!;

                string[] CTFullBuy = _ctFullBuy!;
                string[] CTSecondary = _ctSecondary!;
                string[] CTHalfBuy = _ctHalfBuy!;
                string[] CTPistolRound = _ctPistolRound!;

                if (player.Buttons == 0)
                {
                    buttonPressed[playerid] = false;
                }
                else if (player.Buttons == PlayerButtons.Back && !buttonPressed[playerid])
                {
                    if (mainmenu[playerid] == 0)//main
                    {
                        currentIndexDict[playerid] = (currentIndexDict[playerid] == Main.Length - 1) ? 0 : currentIndexDict[playerid] + 1;
                    }

                    if (mainmenu[playerid] == 1)//T loadout
                    {
                        currentIndexDict[playerid] = (currentIndexDict[playerid] == Tloadout.Length - 1) ? 0 : currentIndexDict[playerid] + 1;
                    }
                    if (mainmenu[playerid] == 2)//T Primary
                    {
                        currentIndexDict[playerid] = (currentIndexDict[playerid] == TFullBuy.Length - 1) ? 0 : currentIndexDict[playerid] + 1;
                    }
                    if (mainmenu[playerid] == 3)//T Secondary
                    {
                        currentIndexDict[playerid] = (currentIndexDict[playerid] == TSecondary.Length - 1) ? 0 : currentIndexDict[playerid] + 1;
                    }
                    if (mainmenu[playerid] == 4)//T Pistol Round
                    {
                        currentIndexDict[playerid] = (currentIndexDict[playerid] == TPistolRound.Length - 1) ? 0 : currentIndexDict[playerid] + 1;
                    }
                    if (mainmenu[playerid] == 10)//T Half Buy
                    {
                        currentIndexDict[playerid] = (currentIndexDict[playerid] == THalfBuy.Length - 1) ? 0 : currentIndexDict[playerid] + 1;
                    }

                    if (mainmenu[playerid] == 5)//CT loadout
                    {
                        currentIndexDict[playerid] = (currentIndexDict[playerid] == CTloadout.Length - 1) ? 0 : currentIndexDict[playerid] + 1;
                    }
                    if (mainmenu[playerid] == 6)//CT Primary
                    {
                        currentIndexDict[playerid] = (currentIndexDict[playerid] == CTFullBuy.Length - 1) ? 0 : currentIndexDict[playerid] + 1;
                    }
                    if (mainmenu[playerid] == 7)//CT Secondary
                    {
                        currentIndexDict[playerid] = (currentIndexDict[playerid] == CTSecondary.Length - 1) ? 0 : currentIndexDict[playerid] + 1;
                    }
                    if (mainmenu[playerid] == 8)//CT Pistol Round
                    {
                        currentIndexDict[playerid] = (currentIndexDict[playerid] == CTPistolRound.Length - 1) ? 0 : currentIndexDict[playerid] + 1;
                    }

                    if (mainmenu[playerid] == 9)//AWP loadout
                    {
                        currentIndexDict[playerid] = (currentIndexDict[playerid] == AWP.Length - 1) ? 0 : currentIndexDict[playerid] + 1;
                    }
                    if (mainmenu[playerid] == 11)//CT Half Buy
                    {
                        currentIndexDict[playerid] = (currentIndexDict[playerid] == CTHalfBuy.Length - 1) ? 0 : currentIndexDict[playerid] + 1;
                    }
                    
                    buttonPressed[playerid] = true;
                    player.ExecuteClientCommand("play sounds/ui/csgo_ui_contract_type4.vsnd_c");
                }
                else if (player.Buttons == PlayerButtons.Forward && !buttonPressed[playerid])
                {
                    if (mainmenu[playerid] == 0)//main
                    {
                        currentIndexDict[playerid] = (currentIndexDict[playerid] == 0) ? Main.Length - 1 : currentIndexDict[playerid] - 1;
                    }

                    if (mainmenu[playerid] == 1)//T loadout
                    {
                        currentIndexDict[playerid] = (currentIndexDict[playerid] == 0) ? Tloadout.Length - 1 : currentIndexDict[playerid] - 1;
                    }
                    if (mainmenu[playerid] == 2)//T Primary
                    {
                        currentIndexDict[playerid] = (currentIndexDict[playerid] == 0) ? TFullBuy.Length - 1 : currentIndexDict[playerid] - 1;
                    }
                    if (mainmenu[playerid] == 3)//T Secondary
                    {
                        currentIndexDict[playerid] = (currentIndexDict[playerid] == 0) ? TSecondary.Length - 1 : currentIndexDict[playerid] - 1;
                    }
                    if (mainmenu[playerid] == 4)//T Pistol Round
                    {
                        currentIndexDict[playerid] = (currentIndexDict[playerid] == 0) ? TPistolRound.Length - 1 : currentIndexDict[playerid] - 1;
                    }
                    if (mainmenu[playerid] == 10)//T Half Buy
                    {
                        currentIndexDict[playerid] = (currentIndexDict[playerid] == 0) ? THalfBuy.Length - 1 : currentIndexDict[playerid] - 1;
                    }

                    if (mainmenu[playerid] == 5)//CT loadout
                    {
                        currentIndexDict[playerid] = (currentIndexDict[playerid] == 0) ? CTloadout.Length - 1 : currentIndexDict[playerid] - 1;
                    }
                    if (mainmenu[playerid] == 6)//CT Primary
                    {
                        currentIndexDict[playerid] = (currentIndexDict[playerid] == 0) ? CTFullBuy.Length - 1 : currentIndexDict[playerid] - 1;
                    }
                    if (mainmenu[playerid] == 7)//CT Secondary
                    {
                        currentIndexDict[playerid] = (currentIndexDict[playerid] == 0) ? CTSecondary.Length - 1 : currentIndexDict[playerid] - 1;
                    }
                    if (mainmenu[playerid] == 8)//CT Pistol Round
                    {
                        currentIndexDict[playerid] = (currentIndexDict[playerid] == 0) ? CTPistolRound.Length - 1 : currentIndexDict[playerid] - 1;
                    }

                    if (mainmenu[playerid] == 9)//AWP loadout
                    {
                        currentIndexDict[playerid] = (currentIndexDict[playerid] == 0) ? AWP.Length - 1 : currentIndexDict[playerid] - 1;
                    }
                    if (mainmenu[playerid] == 11)//CT Half Buy
                    {
                        currentIndexDict[playerid] = (currentIndexDict[playerid] == 0) ? CTHalfBuy.Length - 1 : currentIndexDict[playerid] - 1;
                    }
                    
                    buttonPressed[playerid] = true;
                    player.ExecuteClientCommand("play sounds/ui/csgo_ui_contract_type4.vsnd_c");
                }else if ((player.Buttons == PlayerButtons.Moveleft || player.Buttons == PlayerButtons.Moveright) && !buttonPressed[playerid])
                {
                    int currentLineIndex = currentIndexDict[playerid];

                    if (mainmenu[playerid] == 2)
                    {
                        string currentLineTFullBuy = TFullBuy[currentLineIndex];
                        GunsMenu.HandlePreferenceSelection(player, CsTeam.Terrorist, currentLineTFullBuy);
                        Print(player, Translator.Instance["guns_menu.weapon_selected",currentLineTFullBuy, Utils.TeamString(CsTeam.Terrorist),Translator.Instance["weapon_type.primary"]]);
                    }
                    if (mainmenu[playerid] == 3)
                    {
                        string currentLineTSec = TSecondary[currentLineIndex];
                        GunsMenu.HandlePreferenceSelection(player, CsTeam.Terrorist, currentLineTSec, RoundType.FullBuy);
                        Print(player, Translator.Instance["guns_menu.weapon_selected",currentLineTSec, Utils.TeamString(CsTeam.Terrorist),Translator.Instance["weapon_type.secondary"]]);
                    }
                    if (mainmenu[playerid] == 4)
                    {
                        string currentLineTPR = TPistolRound[currentLineIndex];
                        GunsMenu.HandlePreferenceSelection(player, CsTeam.Terrorist, currentLineTPR);
                        Print(player, Translator.Instance["guns_menu.weapon_selected",currentLineTPR, Utils.TeamString(CsTeam.Terrorist),Translator.Instance["roundtype.Pistol"]]);
                    }
                    if (mainmenu[playerid] == 10)
                    {
                        string currentLineTHalf = THalfBuy[currentLineIndex];
                        GunsMenu.HandlePreferenceSelection(player, CsTeam.Terrorist, currentLineTHalf);
                        Print(player, Translator.Instance["guns_menu.weapon_selected",currentLineTHalf, Utils.TeamString(CsTeam.Terrorist),Translator.Instance["roundtype.HalfBuy"]]);
                    }
                    if (mainmenu[playerid] == 11)
                    {
                        string currentLineCTHalf = CTHalfBuy[currentLineIndex];
                        GunsMenu.HandlePreferenceSelection(player, CsTeam.CounterTerrorist, currentLineCTHalf);
                        Print(player, Translator.Instance["guns_menu.weapon_selected",currentLineCTHalf, Utils.TeamString(CsTeam.CounterTerrorist),Translator.Instance["roundtype.HalfBuy"]]);
                    }

                    if (mainmenu[playerid] == 6)
                    {
                        string currentLineCTFullBuy = CTFullBuy[currentLineIndex];
                        GunsMenu.HandlePreferenceSelection(player, CsTeam.CounterTerrorist, currentLineCTFullBuy);
                        Print(player, Translator.Instance["guns_menu.weapon_selected",currentLineCTFullBuy, Utils.TeamString(CsTeam.CounterTerrorist),Translator.Instance["weapon_type.primary"]]);
                    }
                    if (mainmenu[playerid] == 7)
                    {
                        string currentLineCTSec = CTSecondary[currentLineIndex];
                        GunsMenu.HandlePreferenceSelection(player, CsTeam.CounterTerrorist, currentLineCTSec, RoundType.FullBuy);
                        Print(player, Translator.Instance["guns_menu.weapon_selected",currentLineCTSec, Utils.TeamString(CsTeam.CounterTerrorist),Translator.Instance["weapon_type.secondary"]]);
                    }
                    if (mainmenu[playerid] == 8)
                    {
                        string currentLineCTPR = CTPistolRound[currentLineIndex];
                        GunsMenu.HandlePreferenceSelection(player, CsTeam.CounterTerrorist, currentLineCTPR);
                        Print(player, Translator.Instance["guns_menu.weapon_selected",currentLineCTPR, Utils.TeamString(CsTeam.CounterTerrorist),Translator.Instance["roundtype.Pistol"]]);
                    }

                    if (mainmenu[playerid] == 9)
                    {
                        string currentLineName = AWP[currentLineIndex];
                        if (currentLineName == AWP[0])
                        {
                            GunsMenu.HandlePreferenceSelection(player, CsTeam.Terrorist, CsItem.AWP.ToString(), remove: false);
                            Print(player, Translator.Instance["guns_menu.awp_preference_selected",currentLineName]);
                        }
                        if (currentLineName == AWP[1])
                        {
                            GunsMenu.HandlePreferenceSelection(player, CsTeam.Terrorist, CsItem.AWP.ToString(), remove: true);
                            Print(player, Translator.Instance["guns_menu.awp_preference_selected",currentLineName]);
                        }
                    }

                    if (mainmenu[playerid] == 5)
                    {
                        string currentLineName = CTloadout[currentLineIndex];
                        if (currentLineName == CTloadout[0])
                        {
                            mainmenu[playerid] = 6;
                            currentIndexDict[playerid] = 0;
                        }
                        if (currentLineName == CTloadout[1])
                        {
                            mainmenu[playerid] = 7;
                            currentIndexDict[playerid] = 0;
                        }
                        if (currentLineName == CTloadout[2])
                        {
                            mainmenu[playerid] = 8;
                            currentIndexDict[playerid] = 0;
                        }
                        if (CTHalfBuy.Length > 0 && currentLineName == CTloadout[3])
                        {
                            mainmenu[playerid] = 11;
                            currentIndexDict[playerid] = 0;
                        }
                    }
                    if (mainmenu[playerid] == 1)
                    {
                        string currentLineName = Tloadout[currentLineIndex];
                        if (currentLineName == Tloadout[0])
                        {
                            mainmenu[playerid] = 2;
                            currentIndexDict[playerid] = 0;
                        }
                        if (currentLineName == Tloadout[1])
                        {
                            mainmenu[playerid] = 3;
                            currentIndexDict[playerid] = 0;
                        }
                        if (currentLineName == Tloadout[2])
                        {
                            mainmenu[playerid] = 4;
                            currentIndexDict[playerid] = 0;
                        }
                        if (THalfBuy.Length > 0 && currentLineName == Tloadout[3])
                        {
                            mainmenu[playerid] = 10;
                            currentIndexDict[playerid] = 0;
                        }
                    }
                    if (mainmenu[playerid] == 0)
                    {
                        string currentLineName = Main[currentLineIndex];
                        if (currentLineName == Main[0])
                        {
                            mainmenu[playerid] = 1;
                            currentIndexDict[playerid] = 0;
                        }
                        if (currentLineName == Main[1])
                        {
                            mainmenu[playerid] = 5;
                            currentIndexDict[playerid] = 0;
                        }
                        if (currentLineName == Main[2])
                        {
                            mainmenu[playerid] = 9;
                            currentIndexDict[playerid] = 0;
                        }
                    }
                    buttonPressed[playerid] = true;
                    player.ExecuteClientCommand("play sounds/ui/item_sticker_select.vsnd_c");
                }
                else if ((long)player.Buttons == 8589934592 && !buttonPressed[playerid])
                {
                    if (mainmenu[playerid] == 0)
                    {
                        ForgetPlayer(playerid);
                    }

                    if(mainmenu.ContainsKey(playerid))
                    {
                        if (mainmenu[playerid] == 1)
                        {
                            mainmenu[playerid] = 0;
                            currentIndexDict[playerid] = 0;
                        }
                        if (mainmenu[playerid] == 5)
                        {
                            mainmenu[playerid] = 0;
                            currentIndexDict[playerid] = 0;
                        }

                        if (mainmenu[playerid] == 2)
                        {
                            mainmenu[playerid] = 1;
                            currentIndexDict[playerid] = 0;
                        }
                        if (mainmenu[playerid] == 3)
                        {
                            mainmenu[playerid] = 1;
                            currentIndexDict[playerid] = 0;
                        }
                        if (mainmenu[playerid] == 4)
                        {
                            mainmenu[playerid] = 1;
                            currentIndexDict[playerid] = 0;
                        }
                        if (mainmenu[playerid] == 10)
                        {
                            mainmenu[playerid] = 1;
                            currentIndexDict[playerid] = 0;
                        }
                        if (mainmenu[playerid] == 11)
                        {
                            mainmenu[playerid] = 1;
                            currentIndexDict[playerid] = 0;
                        }

                        if (mainmenu[playerid] == 6)
                        {
                            mainmenu[playerid] = 5;
                            currentIndexDict[playerid] = 0;
                        }
                        if (mainmenu[playerid] == 7)
                        {
                            mainmenu[playerid] = 5;
                            currentIndexDict[playerid] = 0;
                        }
                        if (mainmenu[playerid] == 8)
                        {
                            mainmenu[playerid] = 5;
                            currentIndexDict[playerid] = 0;
                        }

                        if (mainmenu[playerid] == 9)
                        {
                            mainmenu[playerid] = 0;
                            currentIndexDict[playerid] = 0;
                        }
                    }
                    buttonPressed[playerid] = true;
                    player.ExecuteClientCommand("play sounds/ui/menu_focus.vsnd_c");
                }

                // Reused instance: a fresh StringBuilder per player per tick was one of the
                // larger allocation sources here.
                StringBuilder builder = _builder;
                builder.Clear();
                if (mainmenu.ContainsKey(playerid))
                {
                    if(mainmenu[playerid] == 0)
                    {
                        for (int i = 0; i < Main.Length; i++)
                        {
                            if (i == currentIndexDict[playerid])
                            {
                                string lineHtml = $"<font color='orange'>{Imageleft} {Main[i]} {ImageRight}</font><br>";
                                builder.AppendLine(lineHtml);
                            }
                            else
                            {
                                builder.AppendLine($"<font color='white'>{Main[i]}</font><br>");
                            }
                        }
                        builder.AppendLine(BottomMenu);
                    }

                    if(mainmenu[playerid] == 1)
                    {
                        for (int i = 0; i < Tloadout.Length; i++)
                        {
                            if (i == currentIndexDict[playerid]) 
                            {
                                string lineHtml = $"<font color='orange'>{Imageleft} {Tloadout[i]} {ImageRight}</font><br>";
                                builder.AppendLine(lineHtml);
                            }
                            else
                            {
                                builder.AppendLine($"<font color='white'>{Tloadout[i]}</font><br>");
                            }
                        }
                        builder.AppendLine(BottomMenu);
                    }
                    if(mainmenu[playerid] == 5)
                    {
                        for (int i = 0; i < CTloadout.Length; i++)
                        {
                            if (i == currentIndexDict[playerid]) 
                            {
                                string lineHtml = $"<font color='orange'>{Imageleft} {CTloadout[i]} {ImageRight}</font><br>";
                                builder.AppendLine(lineHtml);
                            }
                            else
                            {
                                builder.AppendLine($"<font color='white'>{CTloadout[i]}</font><br>");
                            }
                        }
                        builder.AppendLine(BottomMenu);
                    }
                    if(mainmenu[playerid] == 9)
                    {
                        for (int i = 0; i < AWP.Length; i++)
                        {
                            if (i == currentIndexDict[playerid]) 
                            {
                                string lineHtml = $"<font color='orange'>{Imageleft} {AWP[i]} {ImageRight}</font><br>";
                                builder.AppendLine(lineHtml);
                            }
                            else
                            {
                                builder.AppendLine($"<font color='white'>{AWP[i]}</font><br>");
                            }
                        }
                        builder.AppendLine(BottomMenu);
                    }

                    if(mainmenu[playerid] == 2)
                    {
                        for (int i = 0; i < TFullBuy.Length; i++)
                        {
                            if (i == currentIndexDict[playerid]) 
                            {
                                string lineHtml = $"<font color='orange'>{Imageleft} {TFullBuy[i]} {ImageRight}</font><br>";
                                builder.AppendLine(lineHtml);
                            }
                            else
                            {
                                builder.AppendLine($"<font color='white'>{TFullBuy[i]}</font><br>");
                            }
                        }
                        builder.AppendLine(BottomMenu);
                    }
                    if(mainmenu[playerid] == 3)
                    {
                        for (int i = 0; i < TSecondary.Length; i++)
                        {
                            if (i == currentIndexDict[playerid]) 
                            {
                                string lineHtml = $"<font color='orange'>{Imageleft} {TSecondary[i]} {ImageRight}</font><br>";
                                builder.AppendLine(lineHtml);
                            }
                            else
                            {
                                builder.AppendLine($"<font color='white'>{TSecondary[i]}</font><br>");
                            }
                        }
                        builder.AppendLine(BottomMenuOnpistol);
                    }
                    if(mainmenu[playerid] == 4)
                    {
                        for (int i = 0; i < TPistolRound.Length; i++)
                        {
                            if (i == currentIndexDict[playerid]) 
                            {
                                string lineHtml = $"<font color='orange'>{Imageleft} {TPistolRound[i]} {ImageRight}</font><br>";
                                builder.AppendLine(lineHtml);
                            }
                            else
                            {
                                builder.AppendLine($"<font color='white'>{TPistolRound[i]}</font><br>");
                            }
                        }
                        builder.AppendLine(BottomMenuOnpistol);
                    }
                    if(mainmenu[playerid] == 10)
                    {
                        for (int i = 0; i < THalfBuy.Length; i++)
                        {
                            if (i == currentIndexDict[playerid]) 
                            {
                                string lineHtml = $"<font color='orange'>{Imageleft} {THalfBuy[i]} {ImageRight}</font><br>";
                                builder.AppendLine(lineHtml);
                            }
                            else
                            {
                                builder.AppendLine($"<font color='white'>{THalfBuy[i]}</font><br>");
                            }
                        }
                        builder.AppendLine(BottomMenu);
                    }
                    if(mainmenu[playerid] == 11)
                    {
                        for (int i = 0; i < CTHalfBuy.Length; i++)
                        {
                            if (i == currentIndexDict[playerid]) 
                            {
                                string lineHtml = $"<font color='orange'>{Imageleft} {CTHalfBuy[i]} {ImageRight}</font><br>";
                                builder.AppendLine(lineHtml);
                            }
                            else
                            {
                                builder.AppendLine($"<font color='white'>{CTHalfBuy[i]}</font><br>");
                            }
                        }
                        builder.AppendLine(BottomMenu);
                    }

                    if(mainmenu[playerid] == 6)
                    {
                        for (int i = 0; i < CTFullBuy.Length; i++)
                        {
                            if (i == currentIndexDict[playerid]) 
                            {
                                string lineHtml = $"<font color='orange'>{Imageleft} {CTFullBuy[i]} {ImageRight}</font><br>";
                                builder.AppendLine(lineHtml);
                            }
                            else
                            {
                                builder.AppendLine($"<font color='white'>{CTFullBuy[i]}</font><br>");
                            }
                        }
                        builder.AppendLine(BottomMenu);
                    }
                    if(mainmenu[playerid] == 7)
                    {
                        for (int i = 0; i < CTSecondary.Length; i++)
                        {
                            if (i == currentIndexDict[playerid]) 
                            {
                                string lineHtml = $"<font color='orange'>{Imageleft} {CTSecondary[i]} {ImageRight}</font><br>";
                                builder.AppendLine(lineHtml);
                            }
                            else
                            {
                                builder.AppendLine($"<font color='white'>{CTSecondary[i]}</font><br>");
                            }
                        }
                        builder.AppendLine(BottomMenuOnpistol);
                    }
                    if(mainmenu[playerid] == 8)
                    {
                        for (int i = 0; i < CTPistolRound.Length; i++)
                        {
                            if (i == currentIndexDict[playerid]) 
                            {
                                string lineHtml = $"<font color='orange'>{Imageleft} {CTPistolRound[i]} {ImageRight}</font><br>";
                                builder.AppendLine(lineHtml);
                            }
                            else
                            {
                                builder.AppendLine($"<font color='white'>{CTPistolRound[i]}</font><br>");
                            }
                        }
                        builder.AppendLine(BottomMenuOnpistol);
                    }
                    
                }
                builder.AppendLine("</div>");
                var centerhtml = builder.ToString();
                SendMenuHtml(player, playerid, centerhtml);
            }
        }
    }
    public HookResult OnEventPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (@event == null) return HookResult.Continue;
        var player = @event.Userid;

        if (player == null || !player.IsValid || player.IsBot || player.IsHLTV)return HookResult.Continue;
        ForgetPlayer(player.SteamID);

        return HookResult.Continue;
    }
}
