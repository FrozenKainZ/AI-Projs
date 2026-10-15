using System;
using System.Linq;
using Godot;
using SanguoBasketball.Core;
using SanguoBasketball.Systems;

namespace SanguoBasketball.UI;

/// <summary>SCR_MAIN · 主页（作战大厅）：3 秒内让玩家知道该干什么 —— 最大的朱砂按钮。</summary>
public partial class MainTab : Control
{
    public Button CtaButton { get; private set; }

    private VBoxContainer _root;
    private Label _staminaWarn;

    public override void _Ready()
    {
        Build();
    }

    private void Build()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var margin = UiKit.Pad(16, 6, 16, 4);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(margin);
        _root = UiKit.VBox(8);
        margin.AddChild(_root);

        // ---------- 场景演出区 ----------
        _root.AddChild(BuildSceneArea());

        // ---------- 队伍总评（场景框下方居中） ----------
        int teamOvr = HeroCalc.TeamOvr(GameState.D.Lineup);
        var ovrLabel = UiKit.L($"队伍总评 {teamOvr:N0}", 14, Palette.StoneBlue, true);
        ovrLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _root.AddChild(ovrLabel);

        // ---------- 状态胶囊 ----------
        int cleared = GameState.D.DungeonClearedCount;
        double rate = 1 + ConfigDb.I.C.CoinDungeonBonus * cleared;
        var capsule = UiKit.Card(18, Palette.CardBg, new Color(0, 0, 0, 0.12f), 1, 8);
        var capsuleLabel = UiKit.L(
            $"最高连胜 {GameState.D.Stats.MaxWinStreak} 场 · 金币倍率 ×{rate:0.0} · 已通关 {cleared} 副本",
            12, Palette.Ink);
        capsuleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        capsule.AddChild(capsuleLabel);
        _root.AddChild(capsule);

        // ---------- 模式卡 ----------
        _root.AddChild(BuildModeCards());

        // ---------- 体力提示 ----------
        _staminaWarn = UiKit.L("", 11, Palette.Gamboge, true);
        _staminaWarn.HorizontalAlignment = HorizontalAlignment.Center;
        _root.AddChild(_staminaWarn);
        RefreshStaminaWarn();

        // ---------- 开赛大按钮（60%–92% 拇指热区）----------
        var ctaLabel = Ftue.Step == 1 ? "开 赛 · 新手教学" : "开 赛";
        CtaButton = UiKit.BtnPrimary(ctaLabel, StartNormalRun, 58, 22);
        _root.AddChild(CtaButton);
        if (Ftue.Step == 1)
        {
            CtaButton.AddThemeFontSizeOverride("font_size", 20);
        }

        // 底部弹性占位
        var fill = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
        _root.AddChild(fill);
    }

    private Control BuildSceneArea()
    {
        var scene = new Panel { CustomMinimumSize = new Vector2(0, 296), ClipContents = true };
        scene.AddThemeStyleboxOverride("panel", UiKit.SB(Palette.CardBg, 16, Palette.Ink, 2));
        var tex = Art.Bg("bg_main");
        if (tex != null)
        {
            var tr = new TextureRect
            {
                Texture = tex,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = MouseFilterEnum.Ignore,
                Modulate = new Color(1, 1, 1, 0.9f),
            };
            tr.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            scene.AddChild(tr);
        }

        var col = UiKit.VBox(6);
        col.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        col.OffsetLeft = 12; col.OffsetRight = -12; col.OffsetTop = 10; col.OffsetBottom = -10;
        scene.AddChild(col);

        var head = UiKit.HBox(6);
        head.AddChild(UiKit.L("首发阵容（点击编辑）", 12, Palette.InkSoft));
        col.AddChild(head);

        // 5 张首发卡（呼吸浮动 FX-12）
        var cardRow = UiKit.HBox(10);
        cardRow.Alignment = BoxContainer.AlignmentMode.Center;
        int idx = 0;
        foreach (var slot in Slots.Field)
        {
            GameState.D.Lineup.TryGetValue(slot, out var uid);
            var inst = GameState.HeroByUid(uid);
            var holder = new Control { CustomMinimumSize = new Vector2(58, 84), MouseFilter = MouseFilterEnum.Stop };
            HeroCard card = null;
            if (inst != null)
            {
                card = new HeroCard(54) { ShowStaminaBar = true };
                card.Setup(inst, slot, true);
            }
            else
            {
                card = new HeroCard(54) { ShowStaminaBar = false };
                card.SetupRaw("__empty", "", "green", 0, slot);
            }
            holder.AddChild(card);
            card.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
            card.OffsetTop = 6;
            card.OffsetLeft = -27; card.OffsetRight = 27;
            card.MouseFilter = MouseFilterEnum.Ignore;
            var c = card;
            holder.GuiInput += ev =>
            {
                if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                    TabsScreen.I.SwitchTo("lineup");
            };
            cardRow.AddChild(holder);
            idx++;
        }
        col.AddChild(cardRow);

        var fill = new Control { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        col.AddChild(fill);

        // 入场动效：卡片依次飘入
        Callable.From(() =>
        {
            int i = 0;
            foreach (var child in cardRow.GetChildren())
            {
                if (child is Control cc)
                {
                    var tf = cc.GetChildCount() > 0 ? cc.GetChild<Control>(0) : null;
                    if (tf != null) Fx.CardEnter(tf, i * 0.12f);
                    i++;
                }
            }
        }).CallDeferred();

        return scene;
    }

    /// <summary>模式卡（v2：带水墨背景图）。</summary>
    private Control BuildModeCards()
    {
        var row = UiKit.HBox(10);

        // 普通模式
        var normal = MakeModeCard("bg_mode_normal", "普通模式",
            new[] { "无尽连胜 · 越打越强", "胜利得金币 · 输球收局" }, null,
            () => StartNormalRun());
        row.AddChild(normal);

        // 挑战模式
        int cleared = GameState.D.DungeonClearedCount;
        int next = Math.Min(cleared + 1, DungeonSystem.MaxDungeon);
        string prog = cleared >= DungeonSystem.MaxDungeon
            ? "全部通关！"
            : $"当前：{OpponentGen.DungeonName(next)} {DungeonSystem.BestDefeated(next)}/10 队";
        var chal = MakeModeCard("bg_mode_challenge", "挑战模式",
            new[] { "20 副本 · 每关 10 队" }, prog,
            () => UiKit.PushScreen(new ChallengeScreen()));
        row.AddChild(chal);

        return row;
    }

    private Control MakeModeCard(string bgKey, string title, string[] lines, string chipText, Action onTap)
    {
        var card = UiKit.Card(14, Palette.CardBg, Palette.Ink, 2, 0);
        card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        card.CustomMinimumSize = new Vector2(0, 96);
        card.ClipContents = true;
        card.MouseFilter = MouseFilterEnum.Stop;

        // 背景舞台（PanelContainer 只挂一层普通 Control）
        var stage = new Control { MouseFilter = MouseFilterEnum.Ignore };
        card.AddChild(stage);
        var tex = Art.Bg(bgKey);
        if (tex != null)
        {
            var tr = new TextureRect
            {
                Texture = tex,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            tr.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            stage.AddChild(tr);
            var veil = new ColorRect { Color = new Color(1, 1, 1, 0.52f), MouseFilter = MouseFilterEnum.Ignore };
            veil.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            stage.AddChild(veil);
        }

        var col = UiKit.VBox(3);
        col.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        col.OffsetLeft = 12; col.OffsetRight = -12; col.OffsetTop = 10; col.OffsetBottom = -10;
        col.MouseFilter = MouseFilterEnum.Ignore;
        stage.AddChild(col);
        col.AddChild(UiKit.L(title, 16, Palette.Ink, true));
        foreach (var line in lines)
            col.AddChild(UiKit.L(line, 11, Palette.InkSoft));
        if (chipText != null)
        {
            var chip = UiKit.Card(11, new Color(Palette.StoneBlue, 0.14f), null, 0, 4);
            var chipLabel = UiKit.L(chipText, 11, Palette.StoneBlue);
            chipLabel.HorizontalAlignment = HorizontalAlignment.Center;
            chip.AddChild(chipLabel);
            col.AddChild(chip);
        }

        card.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) onTap();
        };
        return card;
    }

    private void RefreshStaminaWarn()
    {
        var players = LineupUtil.FieldPlayers().Where(p => p != null).ToList();
        if (players.Count == 0) return;
        double avg = players.Average(p => p.Sta);
        if (avg < 40)
        {
            _staminaWarn.Text = $"注意：主力体力偏低（均值 {(int)avg}）点击去背包补充兵粮";
            _staminaWarn.Visible = true;
        }
        else
        {
            _staminaWarn.Visible = false;
        }
    }

    private void StartNormalRun()
    {
        if (LineupUtil.FieldPlayers().Any(x => x == null))
        {
            UiKit.Toast("场上 5 个位置必须放满");
            return;
        }
        GameState.SelectedMode = "normal";
        var run = new NormalRun
        {
            Active = true,
            Streak = 0,
            CoinsEarned = 0,
            ReviveUsed = false,
            CurrentK = 1,
        };
        bool tutorial = Ftue.Step == 1 && GameState.D.Ftue.Step < 2;
        run.CurrentOpponent = OpponentGen.Normal(1, GameRandom.NewSeed(), tutorial);
        GameState.Run = run;
        UiKit.PushScreen(new BattleScreen(BattleMode.Normal));
    }
}

/// <summary>占位 Tab（开发期临时）。</summary>
public partial class TmpTab : Control
{
    private readonly string _title;

    public TmpTab(string title)
    {
        _title = title;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var l = UiKit.L(_title + "（建设中）", 18, Palette.Gray, true);
        l.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        AddChild(l);
    }
}
