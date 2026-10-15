using System;
using System.Linq;
using Godot;
using SanguoBasketball.Core;
using SanguoBasketball.Systems;

namespace SanguoBasketball.UI;

/// <summary>
/// 截图驱动（--shots）：依次打开各界面并保存 PNG 到 docs/screenshots/。
/// 开发校验与交付预览用。
/// </summary>
public static class ShotDriver
{
    public static void Start()
    {
        // 截图模式：跳过 FTUE 干扰，保证界面完整可读
        GameState.D.Ftue.Step = 4;
        GameState.D.Ftue.FreeTenUsed = true;
        LineupUtil.EnsureFieldFilled();
        var node = new ShotRunner();
        ScreenManager.I.AddChild(node);
    }

    /// <summary>立即保存当前视口截图到 docs/screenshots/。</summary>
    public static void CaptureNow(string name)
    {
        try
        {
            var vp = ScreenManager.I?.GetViewport();
            if (vp == null) return;
            var img = vp.GetTexture().GetImage();
            string dir = ProjectSettings.GlobalizePath("res://docs/screenshots");
            DirAccess.MakeDirRecursiveAbsolute(dir);
            img.SavePng(dir + "/" + name + ".png");
            GD.Print("[Shot] saved " + name);
        }
        catch (Exception e)
        {
            GD.PushWarning("[Shot] capture failed " + name + ": " + e.Message);
        }
    }
}

public partial class ShotRunner : Node
{
    private int _step;
    private double _t;
    private bool _actionPending = true;

    private static readonly (string name, Action action, double settle)[] Steps =
    {
        ("01_main", null, 1.2),
        ("02_lineup", () => TabsScreen.I.SwitchTo("lineup"), 1.2),
        ("03_city", () => TabsScreen.I.SwitchTo("gacha"), 1.2),
        ("04_gacha_rates", () =>
        {
            var tab = TabsScreen.I.GetTab() as GachaTab;
            tab?.DebugShowRates();
        }, 1.0),
        ("05_gacha_rates_close", () => Popup.Current?.Close(), 0.6),
        ("06_gacha_ten_reveal", () =>
        {
            var tab = TabsScreen.I.GetTab() as GachaTab;
            tab?.DebugTenDraw();
        }, 1.4),
        ("07_gacha_ten_mid", null, 1.4),
        ("08_bag", () =>
        {
            Popup.Current?.Close();
            TabsScreen.I.SwitchTo("bag");
        }, 1.2),
        ("09_bag_hero", () =>
        {
            var tab = TabsScreen.I.GetTab() as BagTab;
            tab?.DebugHeroTab();
        }, 1.0),
        ("09b_bag_batch", () =>
        {
            var tab = TabsScreen.I.GetTab() as BagTab;
            tab?.DebugBatch();
        }, 1.0),
        ("10_codex", () =>
        {
            TabsScreen.I.SwitchTo("codex");
        }, 1.2),
        ("11_hero_detail", () =>
        {
            var inst = GameState.HeroesOfDef("HERO_JIN_GUANYU").FirstOrDefault()
                       ?? GameState.D.Heroes.FirstOrDefault();
            if (inst != null) UiKit.PushScreen(new HeroDetailScreen(inst));
        }, 1.2),
        ("12_hero_materials", () =>
        {
            var top = ScreenManager.I.Top as HeroDetailScreen;
            top?.DebugShowMaterials();
        }, 1.0),
        ("12b_codex_detail", () =>
        {
            Popup.Current?.Close();
            UiKit.PopScreen();
            UiKit.PushScreen(new CodexHeroDetailScreen(ConfigDb.I.Hero("HERO_JIN_GUANYU")));
        }, 1.2),
        ("13_challenge", () =>
        {
            UiKit.PopScreen();
            UiKit.PushScreen(new ChallengeScreen());
        }, 1.2),
        ("14_dungeon_panel", () =>
        {
            var cs = ScreenManager.I.Top as ChallengeScreen;
            cs?.DebugShowDungeon(1);
        }, 1.2),
        ("15_battle_start", () =>
        {
            Popup.Current?.Close();
            UiKit.PopScreen();
            GameState.SelectedMode = "normal";
            GameState.Run = new NormalRun
            {
                Active = true,
                CurrentK = 9,
                CurrentOpponent = OpponentGen.Normal(9, 54321),
            };
            UiKit.PushScreen(new BattleScreen(BattleMode.Normal));
        }, 4.0),
        ("16_battle_mid", null, 5.0),
        ("16b_battle_info_attrs", () => BattleScreen.Current?.DebugFocus(0, "attrs"), 1.0),
        ("16c_battle_info_stats", () => BattleScreen.Current?.DebugFocus(0, "stats"), 1.0),
        ("17_battle_result", () =>
        {
            BattleScreen.Current?.DebugSkip();
        }, 2.0),
        ("18_altar", () =>
        {
            UiKit.PopScreen(); // 离开比赛
            UiKit.PushScreen(new AltarScreen());
        }, 1.4),
        ("19_run_summary", () =>
        {
            UiKit.PopScreen();
            UiKit.PushScreen(new RunSummaryScreen(new NormalRun
            {
                Streak = 12,
                CoinsEarned = 3456,
                BestMargin = 311,
            }));
        }, 1.2),
    };

    public override void _Process(double delta)
    {
        if (_step >= Steps.Length)
        {
            GD.Print("[Shot] all screens captured, quitting");
            ((SceneTree)Engine.GetMainLoop()).Quit(0);
            return;
        }

        var (name, action, settle) = Steps[_step];
        _t += delta;

        if (_actionPending)
        {
            _actionPending = false;
            _t = 0;
            try
            {
                action?.Invoke();
            }
            catch (Exception e)
            {
                GD.PushWarning("[Shot] step " + name + " action failed: " + e.Message);
            }
            return;
        }

        if (_t >= settle)
        {
            Shot(name);
            _step++;
            _actionPending = true;
            _t = 0;
        }
    }

    private void Shot(string name)
    {
        try
        {
            var img = GetViewport().GetTexture().GetImage();
            string dir = ProjectSettings.GlobalizePath("res://docs/screenshots");
            DirAccess.MakeDirRecursiveAbsolute(dir);
            string path = dir + "/" + name + ".png";
            img.SavePng(path);
            GD.Print("[Shot] saved " + name);
        }
        catch (Exception e)
        {
            GD.PushWarning("[Shot] save failed for " + name + ": " + e.Message);
        }
    }
}
