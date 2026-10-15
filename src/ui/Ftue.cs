using System;
using Godot;
using SanguoBasketball.Core;
using SanguoBasketball.Systems;

namespace SanguoBasketball.UI;

/// <summary>
/// 新手引导（FTUE，01 文档 §10）：3 屏开场 → 发资源 → 教学局 → 免费十连 → 阵容。
/// 期间不出现任何需要阅读超过 2 行的弹窗。
/// </summary>
public static class Ftue
{
    public static int Step => GameState.D.Ftue.Step;

    public static void MaybeIntro(Action after)
    {
        if (Step > 0)
        {
            after?.Invoke();
            return;
        }
        ShowIntroPage(1, after);
    }

    private static void ShowIntroPage(int page, Action after)
    {
        string title, text, artKey;
        switch (page)
        {
            case 1:
                title = "群英穿越";
                text = "三国群英，一夜之间穿越到了篮球场。而你，是这支球队的领队。";
                artKey = "banner_market";
                break;
            case 2:
                title = "你的使命";
                text = "招募武将、锤炼阵容，让关羽投三分、吕布抢篮板、诸葛亮当教练。";
                artKey = "banner_barrack";
                break;
            default:
                title = "全军出击";
                text = "收下启程物资，去赢下第一场比赛吧！";
                artKey = "bg_main";
                break;
        }

        var v = UiKit.VBox(10);
        var tex = Art.Bg(artKey);
        if (tex != null)
        {
            var tr = new TextureRect
            {
                Texture = tex,
                CustomMinimumSize = new Vector2(0, 132),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                ClipContents = true,
            };
            v.AddChild(tr);
        }
        v.AddChild(UiKit.LWrap(text, 14, Palette.InkSoft));
        var btn = UiKit.BtnPrimary(page < 3 ? "继 续" : "领取物资，出发！", () =>
        {
            Popup.Current?.Close();
            if (page < 3)
            {
                Callable.From(() => ShowIntroPage(page + 1, after)).CallDeferred();
            }
            else
            {
                GrantStartResources();
                after?.Invoke();
                Callable.From(() => TipMainStart()).CallDeferred();
            }
        }, 48, 17);
        v.AddChild(btn);

        UiKit.ShowPopup("序章 · " + title, v, false, 320);
    }

    /// <summary>发放初始资源（12 绿卡、金币 500、铜虎符 5、干粮 3）。</summary>
    public static void GrantStartResources()
    {
        foreach (var def in ConfigDb.I.ByQuality("green"))
            GameState.AddHero(def.Id, 1);
        GameState.AddCoins(ConfigDb.I.C.Ext.FtueCoins);
        GameState.AddTali("copper", ConfigDb.I.C.Ext.FtueCopper);
        GameState.AddFood("dry", ConfigDb.I.C.Ext.FtueDry);
        LineupUtil.AutoFill();
        GameState.D.Ftue.Step = 1;
        SaveSystem.Save();
        UiKit.Toast("启程物资已到账：12 名将士 · 金币 500 · 铜虎符 5 · 干粮 3");
        // 刷新主页（阵容与资源即时可见）
        Callable.From(() =>
        {
            var tabs = TabsScreen.I;
            if (tabs != null && tabs.GetTab() is MainTab) tabs.SwitchTo("main");
        }).CallDeferred();
    }

    /// <summary>教学局保送：若失败则换种子重演，直至胜利（上限 12 次）。</summary>
    public static MatchResult SimulateTutorial(BattleTeam a, BattleTeam b, int seed)
    {
        for (int i = 0; i < 12; i++)
        {
            var res = MatchEngine.Simulate(a, b, seed + i, true);
            if (res.PlayerWon) return res;
        }
        return MatchEngine.Simulate(a, b, seed, true);
    }

    public static void TipMainStart()
    {
        if (Step != 1) return;
        var tab = TabsScreen.I;
        if (tab == null) return;
        var cta = tab.MainTabRef?.CtaButton;
        if (cta != null)
            FtueOverlay.Show(cta.GetGlobalRect(), "点击「开赛」，完成你的第一场比赛（对手很弱，稳赢）", null);
    }

    public static void OnFirstBattleWin()
    {
        if (Step == 1)
        {
            GameState.D.Ftue.Step = 2;
            SaveSystem.MarkDirty();
        }
    }

    public static void TipGacha()
    {
        if (Step != 2) return;
        var tab = TabsScreen.I;
        var circle = tab?.Nav?.CircleOf("gacha");
        if (circle != null)
            FtueOverlay.Show(circle.GetGlobalRect(), "去抽卡！市场池有一次免费十连", null);
    }

    public static void OnFreeTenUsed()
    {
        if (Step == 2)
        {
            GameState.D.Ftue.Step = 3;
            SaveSystem.MarkDirty();
        }
    }

    public static void TipLineup()
    {
        if (Step != 3) return;
        var tab = TabsScreen.I;
        var circle = tab?.Nav?.CircleOf("lineup");
        if (circle != null)
            FtueOverlay.Show(circle.GetGlobalRect(), "进入阵容，把最强的卡放上场上五席", null);
    }

    public static void OnLineupTouched()
    {
        if (Step == 3)
        {
            GameState.D.Ftue.Step = 4;
            SaveSystem.MarkDirty();
            UiKit.Toast("恭喜出师！自由发挥吧，主公");
        }
    }
}

/// <summary>引导手势浮层：暗化 + 高亮框 + 单行提示。</summary>
public partial class FtueOverlay : Control
{
    private Rect2 _target;
    private string _text;
    private Action _done;
    private bool _closing;

    public static FtueOverlay Show(Rect2 globalRect, string text, Action onDone)
    {
        var ov = new FtueOverlay { _target = globalRect, _text = text, _done = onDone };
        ov.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ov.MouseFilter = MouseFilterEnum.Stop;
        ScreenManager.I.AddChild(ov);
        return ov;
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0, 0, 0, 0.42f));
        var r = _target.Grow(8);
        DrawRect(r, new Color(0, 0, 0, 0), false, 3, false);
        DrawRect(new Rect2(r.Position, new Vector2(r.Size.X, 3)), Palette.Bamboo);
        DrawRect(new Rect2(new Vector2(r.Position.X, r.End.Y - 3), new Vector2(r.Size.X, 3)), Palette.Bamboo);
        DrawRect(new Rect2(r.Position, new Vector2(3, r.Size.Y)), Palette.Bamboo);
        DrawRect(new Rect2(new Vector2(r.End.X - 3, r.Position.Y), new Vector2(3, r.Size.Y)), Palette.Bamboo);
    }

    public override void _Ready()
    {
        var panel = UiKit.Card(12, new Color(Palette.Ink, 0.88f), null, 0, 12);
        var lbl = UiKit.LWrap(_text, 13, Colors.White);
        lbl.CustomMinimumSize = new Vector2(240, 0);
        panel.AddChild(lbl);
        AddChild(panel);
        Callable.From(() =>
        {
            float w = panel.Size.X;
            float y = _target.End.Y + 16;
            if (y > Size.Y - 140) y = Math.Max(60, _target.Position.Y - panel.Size.Y - 16);
            panel.Position = new Vector2(Mathf.Clamp((Size.X - w) / 2f, 12, Size.X - w - 12), y);
        }).CallDeferred();

        GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { Pressed: true }) Close();
            if (ev is InputEventScreenTouch { Pressed: true }) Close();
        };
    }

    private void Close()
    {
        if (_closing) return;
        _closing = true;
        var t = CreateTween();
        t.TweenProperty(this, "modulate:a", 0.0f, 0.15f);
        t.TweenCallback(Callable.From(() => { _done?.Invoke(); QueueFree(); }));
    }
}
