using System;
using Godot;
using SanguoBasketball.Core;
using SanguoBasketball.Systems;

namespace SanguoBasketball.UI;

/// <summary>启动引导（BOOT → LOADING → MAIN_MENU）。</summary>
public static class Boot
{
    public static bool ShotsMode;

    public static void Run()
    {
        UiKit.Init();
        ConfigDb.Load();
        SaveSystem.Load();
        Ads.Detect();
        MockAdService.Player = MockAdOverlay.Play;

        var args = OS.GetCmdlineUserArgs();
        if (HasFlag(args, "--selftest"))
        {
            int fails = SelfTest.Run();
            GD.Print("SELFTEST_FAILS=" + fails);
            ((SceneTree)Engine.GetMainLoop()).Quit(fails);
            return;
        }

        ShotsMode = HasFlag(args, "--shots");
        if (HasFlag(args, "--freshtest"))
        {
            GameState.D = SaveSystem.CreateFresh();
        }
        if (HasFlag(args, "--rich"))
        {
            // 截图/演示用：发一笔资源（开发用途）
            GameState.AddCoins(120000);
            GameState.AddTali("copper", 60);
            GameState.AddTali("jade", 6);
            foreach (var d in ConfigDb.I.ByQuality("purple")) GameState.AddHero(d.Id, 3);
            foreach (var d in ConfigDb.I.ByQuality("gold")) GameState.AddHero(d.Id, 3);
            // 解锁全部槽位
            foreach (var slot in Slots.All)
                if (!GameState.IsSlotUnlocked(slot)) GameState.D.UnlockedSlots.Add(slot);
            GameState.AddMat("MAT_BRACER", "gold", 30);
            GameState.AddMat("MAT_PAD", "purple", 40);
            GameState.AddMat("MAT_BOOK", "purple", 20);
            GameState.AddMat("MAT_BOOK", "gold", 6);
            GameState.AddMat("MAT_SAND", "purple", 20);
            GameState.AddMat("MAT_SAND", "gold", 6);
            GameState.AddFood("forage", 20);
            GameState.AddFood("ration", 30);
            LineupUtil.AutoFill();
            SaveSystem.Save();
        }

        var root = Shell.Instance;
        root.Theme = UiKit.Theme;
        var mgr = new ScreenManager();
        root.AddChild(mgr);
        mgr.Push(new TabsScreen(), false);

        if (ShotsMode)
        {
            ShotDriver.Start();
        }
        else
        {
            Ftue.MaybeIntro(null);
        }

        // FTUE 引导截图（开发校验）：开场页 → 发资源 + 主界面引导提示
        if (HasFlag(args, "--ftueshot"))
        {
            var t1 = new Timer { WaitTime = 1.0, OneShot = true };
            t1.Timeout += () => ShotDriver.CaptureNow("00_ftue_intro");
            root.AddChild(t1);
            t1.Start();
            var t2 = new Timer { WaitTime = 2.0, OneShot = true };
            t2.Timeout += () =>
            {
                Popup.Current?.Close();
                if (Ftue.Step == 0) Ftue.GrantStartResources();
                Ftue.TipMainStart();
            };
            root.AddChild(t2);
            t2.Start();
            var t3 = new Timer { WaitTime = 3.4, OneShot = true };
            t3.Timeout += () =>
            {
                ShotDriver.CaptureNow("00_ftue_tip");
                ((SceneTree)Engine.GetMainLoop()).Quit(0);
            };
            root.AddChild(t3);
            t3.Start();
        }

        if (HasFlag(args, "--uidebug"))
        {
            GD.Print($"[UIDebug] win={DisplayServer.WindowGetSize()} screen={DisplayServer.ScreenGetSize()} screenScale={DisplayServer.ScreenGetScale()}");
            GD.Print($"[UIDebug] vpRect={root.GetViewportRect()} shellSize={root.Size} contentScale={root.GetWindow().ContentScaleFactor}");
            var t = new Timer { WaitTime = 1.0, OneShot = true };
            t.Timeout += () =>
            {
                var nav = TabsScreen.I.Nav;
                GD.Print($"[UIDebug] nav pos={nav.GlobalPosition} size={nav.Size} inTree={nav.IsVisibleInTree()} anchors={nav.AnchorLeft},{nav.AnchorTop},{nav.AnchorRight},{nav.AnchorBottom}");
                var tab = TabsScreen.I.GetTab();
                GD.Print($"[UIDebug] tab={tab?.GetType().Name} pos={tab?.Position} size={tab?.Size}");
                var c = TabsScreen.I.GetChild<Control>(1);
                GD.Print($"[UIDebug] content size={c.Size}");
                var firstItem = nav.GetChild(0).GetChild(0) as Control;
                GD.Print($"[UIDebug] navItem size={firstItem?.Size}");
                DumpTree(tab, 0, 4);
                var cs = new ChallengeScreen();
                UiKit.PushScreen(cs);
                var t2 = new Timer { WaitTime = 1.2, OneShot = true };
                t2.Timeout += () =>
                {
                    DumpTree(cs, 0, 5);
                    ((SceneTree)Engine.GetMainLoop()).Quit(0);
                };
                root.AddChild(t2);
                t2.Start();
            };
            root.AddChild(t);
            t.Start();
        }
    }

    private static bool HasFlag(string[] args, string flag) => Array.IndexOf(args, flag) >= 0;

    private static void DumpTree(Node n, int depth, int maxDepth)
    {
        if (depth > maxDepth) return;
        string indent = new string(' ', depth * 2);
        if (n is Control cc)
            GD.Print($"[UIDebug] {indent}{n.GetType().Name} pos={cc.Position} size={cc.Size} off=({cc.OffsetLeft},{cc.OffsetTop},{cc.OffsetRight},{cc.OffsetBottom}) anchor=({cc.AnchorLeft},{cc.AnchorTop},{cc.AnchorRight},{cc.AnchorBottom})");
        else
            GD.Print($"[UIDebug] {indent}{n.GetType().Name}");
        foreach (var child in n.GetChildren())
            DumpTree(child, depth + 1, maxDepth);
    }
}
