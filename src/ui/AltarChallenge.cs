using System;
using System.Linq;
using Godot;
using SanguoBasketball.Core;
using SanguoBasketball.Systems;

namespace SanguoBasketball.UI;

/// <summary>SCR_ALTAR · 七星坛：每日 7 次广告领奖（02 文档 §11）。</summary>
public partial class AltarScreen : Control
{
    private Label _countLabel;
    private Control _adHolder;
    private Control _dotsHolder;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var bg = new ColorRect { Color = new Color("#141B2A"), MouseFilter = MouseFilterEnum.Ignore };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);
        var tex = Art.Bg("bg_altar");
        if (tex != null)
        {
            var tr = new TextureRect { Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0.85f) };
            tr.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(tr);
        }

        var margin = UiKit.Pad(16, 48, 16, 16);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(margin);
        var root = UiKit.VBox(10);
        margin.AddChild(root);

        var head = UiKit.HBox(8);
        head.AddChild(UiKit.BtnSecondary("← 返回", () => UiKit.PopScreen(), 36, 13));
        var sp = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        head.AddChild(sp);
        head.AddChild(UiKit.L("七 星 坛", 17, Colors.White, true));
        root.AddChild(head);

        // 次数灯
        _dotsHolder = new Control { CustomMinimumSize = new Vector2(0, 34), MouseFilter = MouseFilterEnum.Ignore };
        root.AddChild(_dotsHolder);
        _countLabel = UiKit.L("", 13, Palette.Gamboge, true);
        _countLabel.HorizontalAlignment = HorizontalAlignment.Center;
        root.AddChild(_countLabel);

        var fill = new Control { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        root.AddChild(fill);

        var tip = UiKit.L("坛体光柱汇聚 · 双奖池：金币 / 虎符", 12, new Color(1, 1, 1, 0.55f));
        tip.HorizontalAlignment = HorizontalAlignment.Center;
        root.AddChild(tip);

        _adHolder = new Control { CustomMinimumSize = new Vector2(0, 52), MouseFilter = MouseFilterEnum.Ignore };
        root.AddChild(_adHolder);

        var rule = UiKit.L("规则：每日 7 次 · 04:00 重置 · 广告失败将返还次数", 11, new Color(1, 1, 1, 0.45f));
        rule.HorizontalAlignment = HorizontalAlignment.Center;
        root.AddChild(rule);

        Refresh();
    }

    private void Refresh()
    {
        GameState.EnsureAltarDay();
        int used = GameState.D.Altar.Used;
        int limit = ConfigDb.I.C.AltarDailyLimit;
        int remain = AltarSystem.Remaining();
        _countLabel.Text = $"今日次数 {used}/{limit}";

        _dotsHolder.ClearChildren();
        var dots = new DotsRow(limit, used);
        dots.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
        dots.CustomMinimumSize = new Vector2(limit * 26, 22);
        dots.OffsetTop = 6;
        _dotsHolder.AddChild(dots);

        _adHolder.ClearChildren();
        if (remain > 0)
        {
            var adBtn = UiKit.AdButton("观看视频 领取祈福", Fx.Nested("ad_ring_seconds", 15f), Play);
            adBtn.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _adHolder.AddChild(adBtn);
        }
        else
        {
            var b = UiKit.BtnSecondary("明日再来", null, 52, 16);
            b.Disabled = true;
            b.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _adHolder.AddChild(b);
        }
    }

    private void Play()
    {
        if (!AltarSystem.CanPlay())
        {
            UiKit.Toast("今日次数已用完，明日再来");
            return;
        }
        Ads.ShowAltar(ok =>
        {
            if (!ok)
            {
                UiKit.Toast("广告暂不可用，次数未消耗，请稍后重试");
                return;
            }
            var rng = new GameRandom(GameRandom.NewSeed());
            var item = AltarSystem.Roll(rng);
            string msg = AltarSystem.Grant(item);
            SaveSystem.Save();
            Refresh();
            UiKit.Toast(msg);
            PlayRewardAnim(item, msg);
        });
    }

    private void PlayRewardAnim(DrawItem item, string msg)
    {
        // 光柱汇聚 → 奖池开启（简化演出）
        var flash = new ColorRect { Color = new Color(1, 0.92f, 0.6f, 0), MouseFilter = MouseFilterEnum.Ignore };
        flash.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(flash);
        var t = flash.CreateTween();
        t.TweenProperty(flash, "color:a", 0.55f, 0.2f);
        t.TweenProperty(flash, "color:a", 0f, 0.5f);
        t.TweenCallback(Callable.From(() => flash.QueueFree()));

        var v = UiKit.VBox(8);
        var iconBox = new Control { CustomMinimumSize = new Vector2(0, 96), MouseFilter = MouseFilterEnum.Ignore };
        var tex = Art.DrawIcon(item);
        if (tex != null)
        {
            var tr = new TextureRect { Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore };
            tr.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
            tr.CustomMinimumSize = new Vector2(88, 88);
            tr.Position = new Vector2(-44, 0);
            iconBox.AddChild(tr);
        }
        v.AddChild(iconBox);
        var label = UiKit.L(msg, 16, Palette.Cinnabar, true);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(label);
        UiKit.ShowPopup("祈 福 结 果", v, true, 300);
    }
}

/// <summary>七灯次数点。</summary>
public partial class DotsRow : Control
{
    private readonly int _total, _used;

    public DotsRow(int total, int used)
    {
        _total = total;
        _used = used;
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(total * 26, 22);
    }

    public override void _Draw()
    {
        for (int i = 0; i < _total; i++)
        {
            var c = i < _used ? new Color(1, 1, 1, 0.25f) : Palette.Gamboge;
            var center = new Vector2(13 + i * 26, Size.Y / 2);
            DrawCircle(center, 6, c);
            if (i >= _used) DrawCircle(center, 3, new Color(1, 1, 1, 0.85f));
        }
    }
}

/// <summary>SCR_CHALLENGE · 挑战模式：20 副本纵向地图（02 文档 §10）。</summary>
public partial class ChallengeScreen : Control
{
    private VBoxContainer _list;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var bg = new ColorRect { Color = Palette.Paper, MouseFilter = MouseFilterEnum.Ignore };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var margin = UiKit.Pad(16, 48, 16, 8);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(margin);
        var root = UiKit.VBox(8);
        margin.AddChild(root);

        var head = UiKit.HBox(8);
        head.AddChild(UiKit.BtnSecondary("← 返回", () => UiKit.PopScreen(), 36, 13));
        var sp = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        head.AddChild(sp);
        head.AddChild(UiKit.L("挑战模式", 17, Palette.Ink, true));
        head.AddChild(UiKit.BtnGhost("奖励预览", () => UiKit.Toast("点选副本节点查看通关奖励"), 12, Palette.StoneBlue));
        root.AddChild(head);

        var sub = UiKit.L("20 副本 · 每关 10 队 · 通关解锁下一关（失败不退费）", 11, Palette.Gray);
        root.AddChild(sub);

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        root.AddChild(scroll);
        _list = UiKit.VBox(6);
        _list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_list);
        BuildList();
    }

    /// <summary>刷新地图（进度变化后调用）。</summary>
    public void Rebuild()
    {
        if (_list == null) return;
        _list.ClearChildren();
        BuildList();
    }

    private void BuildList()
    {
        var mapTex = Art.Bg("bg_challenge");
        int i = 0;
        for (int n = 1; n <= DungeonSystem.MaxDungeon; n++)
        {
            _list.AddChild(BuildNode(n, mapTex, i));
            i++;
        }
    }

    private Control BuildNode(int n, Texture2D mapTex, int idx)
    {
        bool cleared = DungeonSystem.IsCleared(n);
        bool unlocked = DungeonSystem.IsUnlocked(n);
        int best = DungeonSystem.BestDefeated(n);
        string name = OpponentGen.DungeonName(n);
        long fee = DungeonSystem.Fee(n);
        int ovrFrom = DungeonSystem.OppOvr(n, 1);
        int ovrTo = DungeonSystem.OppOvr(n, 10);

        Color border = cleared ? Palette.Cinnabar : unlocked ? Palette.Ink : new Color(0, 0, 0, 0.18f);
        var card = UiKit.Card(12, cleared ? new Color("#FBEAE6") : Palette.CardBg, border, 2, 10);
        if (!unlocked) card.Modulate = new Color(1, 1, 1, 0.62f);

        var h = UiKit.HBox(10);
        card.AddChild(h);

        var noBox = UiKit.Card(10, cleared ? Palette.Cinnabar : unlocked ? Palette.StoneBlue : Palette.Gray, null, 0, 0);
        noBox.CustomMinimumSize = new Vector2(38, 38);
        var noLabel = UiKit.L(cleared ? "✓" : n.ToString(), 15, Colors.White, true);
        noLabel.HorizontalAlignment = HorizontalAlignment.Center;
        noLabel.VerticalAlignment = VerticalAlignment.Center;
        noBox.AddChild(noLabel);
        h.AddChild(noBox);

        var col = UiKit.VBox(2);
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        col.AddChild(UiKit.L($"第 {n} 关 · {name}" + (cleared ? "（已通关）" : unlocked ? "" : "（未解锁）"), 14, Palette.Ink, true));
        col.AddChild(UiKit.L($"对手 OVR {ovrFrom} → {ovrTo} · 10 队 · 进度 {best}/10", 11, Palette.Gray));
        col.AddChild(UiKit.L(unlocked ? $"进场费 {fee:N0} 金币" : $"通关「{OpponentGen.DungeonName(n - 1)}」后解锁", 11, unlocked ? Palette.Gamboge : Palette.Gray));
        h.AddChild(col);

        if (unlocked)
        {
            card.MouseFilter = MouseFilterEnum.Stop;
            int dn = n;
            card.GuiInput += ev =>
            {
                if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                    ShowDungeonPanel(dn);
            };
            if (!cleared)
            {
                // 当前可打节点脉冲光
                var pulse = new Panel { MouseFilter = MouseFilterEnum.Ignore };
                pulse.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                pulse.AddThemeStyleboxOverride("panel", UiKit.SB(new Color(0, 0, 0, 0), 12, Palette.Bamboo, 2));
                card.AddChild(pulse);
                var t = pulse.CreateTween();
                t.SetLoops();
                t.TweenProperty(pulse, "modulate:a", 0.25f, 0.8f);
                t.TweenProperty(pulse, "modulate:a", 1.0f, 0.8f);
            }
        }

        Fx.CardEnter(card, Math.Min(idx, 8) * 0.03f);
        return card;
    }

    // ---------------- 调试入口（截图/自测） ----------------
    public void DebugShowDungeon(int n) => ShowDungeonPanel(n);

    private void ShowDungeonPanel(int n)
    {
        bool cleared = DungeonSystem.IsCleared(n);
        int best = DungeonSystem.BestDefeated(n);
        long fee = DungeonSystem.Fee(n);
        int ovrFrom = DungeonSystem.OppOvr(n, 1);
        int ovrTo = DungeonSystem.OppOvr(n, 10);

        var v = UiKit.VBox(8);
        v.AddChild(UiKit.L($"第 {n} 关 · {OpponentGen.DungeonName(n)}" + (cleared ? " ✓ 已通关" : ""), 16, Palette.Ink, true));
        v.AddChild(UiKit.L($"对手 OVR {ovrFrom} → {ovrTo} · 共 10 队", 12, Palette.InkSoft));
        v.AddChild(UiKit.L($"进度：已击败 {best}/10（重进从第 1 队重打，已击败队名有 ✓）", 11, Palette.Gray));

        // 通关奖励预览
        v.AddChild(UiKit.L("通关奖励（首次通关发放）：", 12, Palette.InkSoft, true));
        var rewards = DungeonSystem.BuildRewards(n);
        var rGrid = new GridContainer { Columns = 6 };
        rGrid.AddThemeConstantOverride("h_separation", 4);
        rGrid.AddThemeConstantOverride("v_separation", 4);
        foreach (var r in rewards)
        {
            var cell = ItemUi.DrawCell(r, 48);
            rGrid.AddChild(cell);
        }
        var rScroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, rewards.Count > 24 ? 192 : 120), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        rScroll.AddChild(rGrid);
        v.AddChild(rScroll);

        var btns = UiKit.HBox(8);
        var enterBtn = UiKit.BtnPrimary($"进入 {fee:N0} 金币", () =>
        {
            Popup.Current?.Close();
            ConfirmEnter(n, fee);
        }, 46, 15);
        enterBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        btns.AddChild(enterBtn);
        var listBtn = UiKit.BtnSecondary("查看队伍列表", () => ShowTeamList(n), 46, 14);
        listBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        btns.AddChild(listBtn);
        v.AddChild(btns);

        UiKit.ShowPopup("副本详情", v, true, 348);
    }

    private void ConfirmEnter(int n, long fee)
    {
        if (GameState.D.Coins < fee)
        {
            UiKit.Toast($"金币不足（还差 {fee - GameState.D.Coins:N0}），去打普通模式");
            return;
        }
        UiKit.Confirm($"进入第 {n} 关 · {OpponentGen.DungeonName(n)}",
            $"支付进场费 {fee:N0} 金币。\n注意：中途失败不退费，可从第 1 队重来。",
            $"支付 {fee:N0} 金币 进入", () =>
            {
                if (!GameState.TrySpendCoins(fee)) return;
                var run = new ChallengeRun
                {
                    Active = true,
                    DungeonN = n,
                    Index = 1,
                    Fee = fee,
                    CurrentOpponent = OpponentGen.Dungeon(n, 1, OpponentGen.DungeonSeed(n, 1)),
                };
                GameState.Challenge = run;
                SaveSystem.Save();
                UiKit.PushScreen(new BattleScreen(BattleMode.Challenge));
            }, "取消", true);
    }

    private void ShowTeamList(int n)
    {
        var v = UiKit.VBox(4);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 360), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var list = UiKit.VBox(4);
        list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(list);
        v.AddChild(scroll);

        int best = DungeonSystem.BestDefeated(n);
        for (int m = 1; m <= 10; m++)
        {
            var team = OpponentGen.Dungeon(n, m, OpponentGen.DungeonSeed(n, m));
            var h = UiKit.HBox(6);
            bool beaten = m <= best;
            var badge = UiKit.L(beaten ? "✓" : m.ToString(), 13, beaten ? Palette.Cinnabar : Palette.InkSoft, true);
            badge.CustomMinimumSize = new Vector2(22, 0);
            h.AddChild(badge);
            var col = UiKit.VBox(1);
            col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            col.AddChild(UiKit.L($"{team.Name} · OVR {team.AvgCourtOvr()}", 13, Palette.Ink, true));
            string comp = string.Join(" ", team.Court.Select(p => Quality.Cn(p.Quality) + p.Name));
            col.AddChild(UiKit.L(comp, 10, Palette.Gray));
            h.AddChild(col);
            var card = UiKit.Card(10, Palette.CardBg, beaten ? Palette.Cinnabar : new Color(0, 0, 0, 0.1f), beaten ? 2 : 1, 6);
            card.AddChild(h);
            list.AddChild(card);
        }
        UiKit.ShowPopup($"第 {n} 关 · 队伍侦查", v, true, 344);
    }
}
