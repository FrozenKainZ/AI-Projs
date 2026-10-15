using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using SanguoBasketball.Core;
using SanguoBasketball.Systems;

namespace SanguoBasketball.UI;

public enum BattleMode
{
    Normal,
    Challenge,
}

/// <summary>
/// SCR_BATTLE · 比赛直播（SYS-05/06/07/08 的 UI 载体）。
/// 数据先算后播；1x/2x/4x/跳过；六犯换人条（3s 可改选，重演保持一致）；托管两类。
/// </summary>
public partial class BattleScreen : Control
{
    public static BattleScreen Current;

    private readonly BattleMode _mode;

    // 数据
    private BattleTeam _playerTeam;
    private BattleTeam _oppTeam;
    private OpponentTeam _oppGen;
    private MatchResult _result;
    private int _seed;
    private readonly Dictionary<int, string> _overrides = new();
    private readonly HashSet<string> _enteredBench = new();
    private readonly Dictionary<string, int> _fouls = new();

    // 播放
    private int _eventIdx = -1;
    private double _timer;
    private float _speed = 1f;
    private bool _finished;
    private bool _skipArmed;
    private double _skipArmedAt;

    // 托管（SYS-08）
    private bool _autopilot;
    private bool _autopilotFood;
    private bool _autoContinuePending;
    private double _autoContinueAt;

    // UI
    private Label _nameA, _nameB, _scoreA, _scoreB, _quarterLabel, _timeLabel;
    private Control _progressFill;
    private Control _progressTrack;
    private VBoxContainer _feed;
    private ScrollContainer _feedScroll;
    private HBoxContainer _myRow;
    private HBoxContainer _oppRow;
    private HBoxContainer _coachRow;
    private Dictionary<string, HeroCard> _myCards = new();
    private Button _autoBtn;
    private Label _autoCapsule;
    private Button _b1, _b2, _b4, _skipQBtn, _skipBtn, _gearBtn;
    private Control _subBarHolder;
    private Label _marginStamp;
    private int _shownA, _shownB;

    // v2：信息面板（球员属性 / 数据统计）+ 聚焦球员 + 实时数据
    private Control _infoContent;
    private string _infoTab = "stats";
    private BattlePlayer _focused;
    private Button _attrTabBtn, _statsTabBtn;
    private readonly Dictionary<string, LiveStat> _liveStats = new();

    private sealed class LiveStat
    {
        public int Points, Threes, Blocks, Steals, Fouls;
    }

    public BattleScreen(BattleMode mode)
    {
        _mode = mode;
    }

    public override void _Ready()
    {
        Current = this;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildChrome();
        StartMatch();
        if (Boot.ShotsMode) { _speed = 4f; SetSpeedVisual(); }
        SetProcess(true);
    }

    public override void _ExitTree()
    {
        if (Current == this) Current = null;
    }

    /// <summary>调试入口：聚焦我方某位置球员并切换信息面板（截图/自测用）。</summary>
    public void DebugFocus(int slotIdx, string tab)
    {
        _infoTab = tab;
        if (slotIdx >= 0 && slotIdx < 5 && _playerTeam != null && _playerTeam.Court[slotIdx] != null)
            _focused = _playerTeam.Court[slotIdx];
        RefreshInfoPanel();
    }

    /// <summary>调试入口：直接跳到终场（截图/自测用）。</summary>
    public void DebugSkip()
    {
        OnSkipPressed();
    }

    // ================= 界面骨架 =================
    private void BuildChrome()
    {
        var bg = new ColorRect { Color = Palette.Paper, MouseFilter = MouseFilterEnum.Ignore };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);
        var tex = Art.Bg("bg_battle");
        if (tex != null)
        {
            var tr = new TextureRect { Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0.35f) };
            tr.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(tr);
        }

        var margin = UiKit.Pad(12, 44, 12, 8);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(margin);
        var root = UiKit.VBox(8);
        margin.AddChild(root);

        // ---- 比分牌 ----
        var board = UiKit.Card(14, Palette.DarkBoard, null, 0, 10);
        var bv = UiKit.VBox(6);
        board.AddChild(bv);
        var names = UiKit.HBox(0);
        _nameA = UiKit.L("", 12, new Color("#B9C2CF"));
        _nameA.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _nameB = UiKit.L("", 12, new Color("#E2E6EE"));
        _nameB.HorizontalAlignment = HorizontalAlignment.Right;
        _nameB.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        names.AddChild(_nameA);
        names.AddChild(_nameB);
        bv.AddChild(names);

        var scoreRow = UiKit.HBox(0);
        _scoreA = UiKit.L("0", 44, Colors.White, true);
        _scoreA.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _scoreA.HorizontalAlignment = HorizontalAlignment.Center;
        var colon = UiKit.L(":", 22, Palette.Gray, true);
        colon.VerticalAlignment = VerticalAlignment.Center;
        _scoreB = UiKit.L("0", 44, Palette.Gamboge, true);
        _scoreB.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _scoreB.HorizontalAlignment = HorizontalAlignment.Center;
        scoreRow.AddChild(_scoreA);
        scoreRow.AddChild(colon);
        scoreRow.AddChild(_scoreB);
        bv.AddChild(scoreRow);

        var qRow = UiKit.HBox(8);
        _quarterLabel = UiKit.L("Q1", 12, new Color("#B9C2CF"));
        qRow.AddChild(_quarterLabel);
        _progressTrack = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 8), MouseFilter = MouseFilterEnum.Ignore };
        _progressFill = new Control { MouseFilter = MouseFilterEnum.Ignore };
        _progressFill.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _progressFill.AnchorRight = 0;
        _progressTrack.AddChild(_progressFill);
        qRow.AddChild(_progressTrack);
        _timeLabel = UiKit.L("剩 12'00\"", 12, new Color("#B9C2CF"));
        qRow.AddChild(_timeLabel);
        bv.AddChild(qRow);

        _marginStamp = UiKit.L("碾 压 局", 12, Palette.Cinnabar, true);
        _marginStamp.HorizontalAlignment = HorizontalAlignment.Center;
        _marginStamp.Visible = false;
        bv.AddChild(_marginStamp);
        root.AddChild(board);

        // ---- 对手席（背影剪影）----
        var oppPanel = UiKit.Card(12, new Color(1, 1, 1, 0.72f), new Color(0, 0, 0, 0.10f), 1, 6);
        var ov = UiKit.VBox(4);
        oppPanel.AddChild(ov);
        ov.AddChild(UiKit.L("对手席", 10, Palette.Gray));
        var oppRow = UiKit.HBox(6);
        ov.AddChild(oppRow);
        _oppRow = oppRow;
        root.AddChild(oppPanel);

        // ---- 直播文字流 ----
        var feedPanel = UiKit.Card(12, new Color(1, 1, 1, 0.82f), Palette.Ink, 2, 6);
        feedPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        var fv = UiKit.VBox(4);
        feedPanel.AddChild(fv);
        fv.AddChild(UiKit.L("直播文字流（自动上滚）", 10, Palette.Gray));
        _feedScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _feed = UiKit.VBox(4);
        _feed.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _feedScroll.AddChild(_feed);
        fv.AddChild(_feedScroll);
        root.AddChild(feedPanel);

        // ---- 我方席 ----
        var myPanel = UiKit.Card(12, new Color(1, 1, 1, 0.82f), Palette.Ink, 2, 6);
        var mv = UiKit.VBox(4);
        myPanel.AddChild(mv);
        var myHead = UiKit.HBox(6);
        myHead.AddChild(UiKit.L("我方席（体力/犯规实时）", 10, Palette.Gray));
        var sp = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        myHead.AddChild(sp);
        _autoCapsule = UiKit.L("", 10, Palette.Bamboo, true);
        myHead.AddChild(_autoCapsule);
        mv.AddChild(myHead);
        _myRow = UiKit.HBox(6);
        mv.AddChild(_myRow);
        // v2：教练席（原替补席位置显示教练）
        _coachRow = UiKit.HBox(4);
        mv.AddChild(_coachRow);
        root.AddChild(myPanel);

        // ---- v2：信息面板（球员属性 / 数据统计 + 替补席） ----
        var infoPanel = UiKit.Card(12, new Color(1, 1, 1, 0.82f), new Color(0, 0, 0, 0.12f), 1, 6);
        var iv = UiKit.VBox(4);
        infoPanel.AddChild(iv);
        var tabRow = UiKit.HBox(5);
        _attrTabBtn = UiKit.BtnSecondary("球员属性", () => { _infoTab = "attrs"; RefreshInfoPanel(); }, 26, 11);
        _attrTabBtn.CustomMinimumSize = new Vector2(64, 26);
        _statsTabBtn = UiKit.BtnSecondary("数据统计", () => { _infoTab = "stats"; RefreshInfoPanel(); }, 26, 11);
        _statsTabBtn.CustomMinimumSize = new Vector2(64, 26);
        tabRow.AddChild(_attrTabBtn);
        tabRow.AddChild(_statsTabBtn);
        var sp2 = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        tabRow.AddChild(sp2);
        var benchBtn = UiKit.BtnSecondary("替补席", ShowBenchPopup, 26, 11);
        benchBtn.CustomMinimumSize = new Vector2(58, 26);
        tabRow.AddChild(benchBtn);
        iv.AddChild(tabRow);
        _infoContent = new Control { CustomMinimumSize = new Vector2(0, 44), MouseFilter = MouseFilterEnum.Ignore };
        iv.AddChild(_infoContent);
        root.AddChild(infoPanel);

        // ---- 控制排（拇指区）----
        var ctrl = UiKit.HBox(5);
        _b1 = UiKit.BtnRound("1x", () => SetSpeed(1f), 42, 13);
        _b2 = UiKit.BtnRound("2x", () => SetSpeed(2f), 42, 13);
        _b4 = UiKit.BtnRound("4x", () => SetSpeed(4f), 42, 13);
        _skipQBtn = UiKit.BtnRound("跳节", () => OnSkipQuarter(), 42, 12);
        _skipQBtn.CustomMinimumSize = new Vector2(52, 42);
        _skipBtn = UiKit.BtnRound("跳过", () => OnSkipPressed(), 42, 12);
        _skipBtn.CustomMinimumSize = new Vector2(52, 42);
        ctrl.AddChild(_b1);
        ctrl.AddChild(_b2);
        ctrl.AddChild(_b4);
        ctrl.AddChild(_skipQBtn);
        ctrl.AddChild(_skipBtn);
        var fill = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        ctrl.AddChild(fill);
        _autoBtn = UiKit.BtnSecondary("托管：关", ToggleAutopilot, 42, 12);
        // 长按弹出模式选择
        var hold = new Timer { WaitTime = 0.6, OneShot = true };
        AddChild(hold);
        hold.Timeout += ShowAutoModePopup;
        _autoBtn.ButtonDown += () => hold.Start();
        _autoBtn.ButtonUp += () => hold.Stop();
        ctrl.AddChild(_autoBtn);
        _gearBtn = UiKit.BtnRound("⚙", ShowAutoModePopup, 42, 14);
        ctrl.AddChild(_gearBtn);
        root.AddChild(ctrl);
        var ctrlHint = UiKit.L("跳节=快进一节 · ⚙/长按托管=选择是否耗粮", 10, Palette.Gray);
        ctrlHint.HorizontalAlignment = HorizontalAlignment.Center;
        root.AddChild(ctrlHint);

        // ---- 换人条容器（叠加）----
        _subBarHolder = new Control { MouseFilter = MouseFilterEnum.Ignore };
        _subBarHolder.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_subBarHolder);

        SetSpeedVisual();
    }

    // ================= 比赛启动 =================
    private void StartMatch()
    {
        _playerTeam = MatchEngine.BuildPlayerTeam();
        _oppGen = _mode == BattleMode.Normal ? GameState.Run.CurrentOpponent : GameState.Challenge.CurrentOpponent;
        _oppTeam = MatchEngine.BuildOpponentTeam(_oppGen);
        _seed = GameRandom.NewSeed();
        _overrides.Clear();
        _enteredBench.Clear();
        _fouls.Clear();
        _liveStats.Clear();
        _focused = null;
        _eventIdx = -1;
        _timer = 0.6;
        _finished = false;
        _shownA = 0;
        _shownB = 0;

        bool tutorial = _mode == BattleMode.Normal && Ftue.Step == 1;
        _result = tutorial
            ? Ftue.SimulateTutorial(_playerTeam, _oppTeam, _seed)
            : MatchEngine.Simulate(_playerTeam, _oppTeam, _seed, true, _overrides);

        _nameA.Text = _playerTeam.Name;
        _nameB.Text = _oppTeam.Name;
        _scoreA.Text = "0";
        _scoreB.Text = "0";
        _quarterLabel.Text = "Q1";
        _marginStamp.Visible = false;

        // 对手席（v2：正常立绘 + 教练席）
        _oppRow.AddThemeConstantOverride("separation", 5);
        _oppRow.ClearChildren();
        foreach (var p in _oppGen.Court.Take(5))
        {
            var holder = new Control { CustomMinimumSize = new Vector2(42, 66), MouseFilter = MouseFilterEnum.Ignore };
            var card = new HeroCard(40);
            card.SetupRaw(p.DefId, p.Name, p.Quality, p.Star, null);
            card.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
            card.OffsetLeft = -20; card.OffsetRight = 20;
            card.MouseFilter = MouseFilterEnum.Ignore;
            holder.AddChild(card);
            _oppRow.AddChild(holder);
        }
        foreach (var co in _oppGen.Coaches.Take(3))
        {
            var holder = new Control { CustomMinimumSize = new Vector2(34, 66), MouseFilter = MouseFilterEnum.Ignore };
            var card = new HeroCard(32);
            card.SetupRaw(co.DefId, co.Name, co.Quality, co.Star, "教");
            card.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
            card.OffsetLeft = -16; card.OffsetRight = 16;
            card.MouseFilter = MouseFilterEnum.Ignore;
            holder.AddChild(card);
            _oppRow.AddChild(holder);
        }

        // 我方席
        _myCards.Clear();
        RefreshMyRow();
        RefreshCoachRow();
        RefreshInfoPanel();

        // 文字流
        _feed.ClearChildren();
        UpdateProgress(0, 0);

        // 开赛提示
        AddFeedLine("第 " + (_mode == BattleMode.Normal ? GameState.Run.CurrentK : GameState.Challenge.Index) + " 场开打！双方亮阵完毕。", "opening");
    }

    /// <summary>我方教练席（v2：顶替原替补席展示位）。</summary>
    private void RefreshCoachRow()
    {
        _coachRow.ClearChildren();
        int totalAura = 0;
        foreach (var slot in Slots.Coach)
        {
            var inst = GameState.HeroByUid(GetLineupUid(slot));
            var holder = new Control { CustomMinimumSize = new Vector2(34, 60), MouseFilter = MouseFilterEnum.Ignore };
            if (inst != null)
            {
                var card = new HeroCard(32);
                card.Setup(inst, "教", false);
                card.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
                card.OffsetLeft = -16; card.OffsetRight = 16;
                card.MouseFilter = MouseFilterEnum.Ignore;
                holder.AddChild(card);
                totalAura += HeroCalc.CoachAura(new List<HeroInst> { inst });
            }
            else if (GameState.IsSlotUnlocked(slot))
            {
                var dashed = new Panel { CustomMinimumSize = new Vector2(30, 42), MouseFilter = MouseFilterEnum.Ignore };
                dashed.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
                dashed.OffsetLeft = -15; dashed.OffsetRight = 15; dashed.OffsetTop = 4; dashed.OffsetBottom = 46;
                dashed.AddThemeStyleboxOverride("panel", UiKit.SB(new Color(1, 1, 1, 0.3f), 8, Palette.Gray, 1));
                holder.AddChild(dashed);
            }
            else
            {
                var lockLabel = UiKit.L("🔒", 11, Palette.Gray);
                lockLabel.HorizontalAlignment = HorizontalAlignment.Center;
                lockLabel.VerticalAlignment = VerticalAlignment.Center;
                lockLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                lockLabel.MouseFilter = MouseFilterEnum.Ignore;
                holder.AddChild(lockLabel);
            }
            _coachRow.AddChild(holder);
        }
        var auraLabel = UiKit.L($"教练光环 +{totalAura}", 10, totalAura > 0 ? Palette.Gamboge : Palette.Gray, true);
        auraLabel.VerticalAlignment = VerticalAlignment.Center;
        _coachRow.AddChild(auraLabel);
    }

    private void RefreshMyRow()
    {
        _myRow.AddThemeConstantOverride("separation", 4);
        _myRow.ClearChildren();
        foreach (var slot in Slots.Field)
        {
            var holder = new Control { CustomMinimumSize = new Vector2(44, 76), MouseFilter = MouseFilterEnum.Stop };
            var card = new HeroCard(42) { ShowStaminaBar = true };
            var inst = GameState.HeroByUid(GetLineupUid(slot));
            if (inst != null)
            {
                card.Setup(inst, slot, true);
                _myCards[inst.Uid] = card;
            }
            else
            {
                card.SetupRaw("__empty", "—", "green", 0, slot);
            }
            card.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
            card.OffsetLeft = -21; card.OffsetRight = 21;
            card.MouseFilter = MouseFilterEnum.Ignore;
            holder.AddChild(card);
            string uid = inst?.Uid;
            holder.GuiInput += ev =>
            {
                if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                    FocusPlayer(uid);
            };
            _myRow.AddChild(holder);
        }
    }

    /// <summary>聚焦场上球员（点击我方席卡片），信息面板显示其数据/属性。</summary>
    private void FocusPlayer(string uid)
    {
        if (string.IsNullOrEmpty(uid)) return;
        if (_focused != null && _focused.Uid == uid) uid = null; // 再次点击取消聚焦
        _focused = null;
        if (uid != null)
        {
            foreach (var p in _playerTeam.Court)
                if (p != null && p.Uid == uid) { _focused = p; break; }
        }
        RefreshInfoPanel();
    }

    /// <summary>信息面板：球员属性 / 数据统计（无聚焦显示 -）。</summary>
    private void RefreshInfoPanel()
    {
        if (_infoContent == null) return;

        // Tab 选中态
        void Style(Button b, bool active)
        {
            b.AddThemeColorOverride("font_color", active ? Colors.White : Palette.Ink);
            b.AddThemeColorOverride("font_hover_color", active ? Colors.White : Palette.Ink);
            b.AddThemeStyleboxOverride("normal", UiKit.SB(active ? Palette.Ink : Palette.CardBg, 10, Palette.Ink, 2));
            b.AddThemeStyleboxOverride("hover", UiKit.SB(active ? Palette.Ink : Palette.Paper, 10, Palette.Ink, 2));
            b.AddThemeStyleboxOverride("pressed", UiKit.SB(Palette.Ink, 10, Palette.Ink, 2));
        }
        Style(_attrTabBtn, _infoTab == "attrs");
        Style(_statsTabBtn, _infoTab == "stats");

        _infoContent.ClearChildren();
        var row = UiKit.HBox(10);
        row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _infoContent.AddChild(row);

        if (_focused == null)
        {
            var hint = UiKit.L("点击场上球员查看" + (_infoTab == "attrs" ? "属性" : "数据") + " —", 12, Palette.Gray);
            hint.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(hint);
            return;
        }

        var nameCol = UiKit.VBox(1);
        nameCol.CustomMinimumSize = new Vector2(64, 0);
        nameCol.AddChild(UiKit.L(_focused.Name, 13, Palette.Ink, true));
        nameCol.AddChild(UiKit.L(_focused.SlotPos, 10, Palette.Gray));
        row.AddChild(nameCol);

        if (_infoTab == "stats")
        {
            _liveStats.TryGetValue(_focused.Uid, out var st);
            st ??= new LiveStat();
            void Stat(string k, string v)
            {
                var col = UiKit.VBox(0);
                col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                var kl = UiKit.L(k, 9, Palette.Gray);
                kl.HorizontalAlignment = HorizontalAlignment.Center;
                var vl = UiKit.L(v, 13, Palette.Ink, true);
                vl.HorizontalAlignment = HorizontalAlignment.Center;
                col.AddChild(kl);
                col.AddChild(vl);
                row.AddChild(col);
            }
            Stat("得分", st.Points.ToString());
            Stat("三分", st.Threes.ToString());
            Stat("盖帽", st.Blocks.ToString());
            Stat("抢断", st.Steals.ToString());
            Stat("犯规", st.Fouls.ToString());
            Stat("篮板", "—");
        }
        else
        {
            // 比赛属性近似：核心×位置×体力 + 光环
            var inst = _focused.Inst;
            if (inst == null)
            {
                row.AddChild(UiKit.L("对手球员属性暂不可查", 11, Palette.Gray));
                return;
            }
            var core = HeroCalc.CoreAttrs(inst);
            double pos = Slots.IsField(_focused.SlotPos) && inst.Def.PrefMatches(_focused.SlotPos)
                ? ConfigDb.I.C.PosBonusMatch : ConfigDb.I.C.PosBonusOther;
            float sta = HeroCalc.StaminaMult(inst.Sta);
            float aura = _playerTeam.Aura;
            var grid = new GridContainer { Columns = 5 };
            grid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            grid.AddThemeConstantOverride("h_separation", 6);
            for (int i = 0; i < Attr.COUNT; i++)
            {
                int v = (int)MathF.Round(core[i] * (float)pos * sta + aura);
                var cell = UiKit.VBox(0);
                var kl = UiKit.L(Attr.Shorts[i], 9, Palette.Gray);
                kl.HorizontalAlignment = HorizontalAlignment.Center;
                var vl = UiKit.L(v.ToString(), 11, Palette.Ink, true);
                vl.HorizontalAlignment = HorizontalAlignment.Center;
                cell.AddChild(kl);
                cell.AddChild(vl);
                grid.AddChild(cell);
            }
            row.AddChild(grid);
        }
    }

    /// <summary>替补席弹窗（v2：点击替补席按钮查看）。</summary>
    private void ShowBenchPopup()
    {
        var v = UiKit.VBox(6);
        v.AddChild(UiKit.L("替补席（比赛不耗体力 · 六犯顶替登场）", 11, Palette.Gray));
        var bench = LineupUtil.AvailableBench();
        if (bench.Count == 0)
        {
            bool anyLocked = Slots.Bench.Any(s => !GameState.IsSlotUnlocked(s));
            v.AddChild(UiKit.L(anyLocked ? "替补席尚未解锁（阵容页可用金币解锁）" : "替补席没有球员", 12, Palette.Gray));
        }
        foreach (var h in bench)
        {
            var card = UiKit.Card(10, Palette.CardBg, Quality.Color(h.Def.QualityKey), 2, 6);
            var hr = UiKit.HBox(8);
            card.AddChild(hr);
            var hc = new HeroCard(36);
            hc.Setup(h);
            hc.MouseFilter = MouseFilterEnum.Ignore;
            hr.AddChild(hc);
            var col = UiKit.VBox(1);
            col.AddChild(UiKit.L(h.Def.Name, 13, Palette.Ink, true));
            col.AddChild(UiKit.L($"OVR {HeroCalc.Ovr(h)} · 体力 {h.Sta}/{HeroCalc.StaCapTotal(h)} · 偏好 {h.Def.PrefText}", 10, Palette.Gray));
            hr.AddChild(col);
            v.AddChild(card);
        }
        UiKit.ShowPopup("替补席", v, true, 320);
    }

    private static string GetLineupUid(string slot)
        => GameState.D.Lineup.TryGetValue(slot, out var uid) ? uid : null;

    // ================= 播放循环 =================
    public override void _Process(double delta)
    {
        if (_finished)
        {
            // 托管自动续战
            if (_autoContinuePending && Time.GetTicksMsec() / 1000.0 >= _autoContinueAt)
            {
                _autoContinuePending = false;
                ContinueNextMatch();
            }
            return;
        }

        if (_skipArmed && Time.GetTicksMsec() / 1000.0 - _skipArmedAt > 2.5)
        {
            _skipArmed = false;
        }

        _timer -= delta;
        if (_timer > 0) return;
        AdvanceOneEvent();
        _timer = 1.7 / _speed;
    }

    private LiveStat StatFor(string uid)
    {
        if (!_liveStats.TryGetValue(uid, out var s))
        {
            s = new LiveStat();
            _liveStats[uid] = s;
        }
        return s;
    }

    private void AdvanceOneEvent()
    {
        if (_eventIdx + 1 >= _result.Events.Count)
        {
            FinishMatch();
            return;
        }
        _eventIdx++;
        var e = _result.Events[_eventIdx];

        // 比分跳变（FX-09）
        if (e.ScoreA != _shownA || e.ScoreB != _shownB)
        {
            int dA = e.ScoreA - _shownA;
            int dB = e.ScoreB - _shownB;
            _shownA = e.ScoreA;
            _shownB = e.ScoreB;
            if (dA > 0) ScorePop(_scoreA, e.ScoreA, dA, Palette.Gamboge);
            if (dB > 0) ScorePop(_scoreB, e.ScoreB, dB, Colors.White);
        }

        // 节次与进度
        int q = Math.Clamp(e.Quarter, 1, 4);
        _quarterLabel.Text = e.Quarter > 4 ? "加时" : "Q" + q;
        UpdateProgress(e.Quarter, e.At);

        // 文字流（FX-15）
        AddFeedLine(e.Text, e.Type);

        // v2：实时数据归集（供信息面板）
        if (e.DeltaA > 0 && !string.IsNullOrEmpty(e.ActorUid))
        {
            var st = StatFor(e.ActorUid);
            st.Points += e.DeltaA;
            if (e.Type == "three") st.Threes++;
        }
        if (e.ActorIsA && !string.IsNullOrEmpty(e.ActorUid))
        {
            if (e.Type == "block") StatFor(e.ActorUid).Blocks++;
            if (e.Type == "steal") StatFor(e.ActorUid).Steals++;
            if (e.Type == "foul" && e.FoulsNow > 0) StatFor(e.ActorUid).Fouls = e.FoulsNow;
        }
        RefreshInfoPanel();

        // 犯规/换人 UI
        if (!string.IsNullOrEmpty(e.ActorUid))
        {
            if (e.Type == "foul" && e.FoulsNow > 0)
            {
                _fouls[e.ActorUid] = e.FoulsNow;
                if (_myCards.TryGetValue(e.ActorUid, out var card))
                    card.SetFouls(e.FoulsNow, e.FoulsNow >= 5);
            }
            else if (e.Type == "foulout")
            {
                if (_myCards.TryGetValue(e.ActorUid, out var card))
                    card.SetFouls(ConfigDb.I.C.FoulOutCount, false);
                // 替补上场：更新席位
                if (_myCards.TryGetValue(e.ActorUid, out var old) && e.SubInName != "无人可换")
                {
                    var subInst = FindBenchInstByName(e.SubInName);
                    if (subInst != null) _enteredBench.Add(subInst.Uid);
                }
                if (e.SubPending) ShowSubBar(e);
            }
        }

        // 碾压局印章
        if (Math.Abs(e.ScoreA - e.ScoreB) >= 100) _marginStamp.Visible = true;
    }

    private HeroInst FindBenchInstByName(string name)
    {
        foreach (var slot in Slots.Bench)
        {
            var inst = GameState.HeroByUid(GetLineupUid(slot));
            if (inst != null && inst.Def.Name == name) return inst;
        }
        return null;
    }

    private void ScorePop(Label label, int value, int delta, Color color)
    {
        label.Text = value.ToString();
        label.PivotOffset = label.Size / 2;
        var t = label.CreateTween();
        t.TweenProperty(label, "scale", new Vector2(Fx.Nested("score_pop.scale", 1.3f), Fx.Nested("score_pop.scale", 1.3f)), Fx.Nested("score_pop.dur", 0.2f) * 0.5f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        t.TweenProperty(label, "scale", Vector2.One, Fx.Nested("score_pop.dur", 0.2f) * 0.5f);

        // 飘分
        var floatLabel = UiKit.L("+" + delta, 14, color, true);
        floatLabel.Position = new Vector2(label.GlobalPosition.X - 8, label.GlobalPosition.Y + 8);
        floatLabel.ZIndex = 50;
        AddChild(floatLabel);
        var ft = floatLabel.CreateTween();
        ft.SetParallel(true);
        ft.TweenProperty(floatLabel, "position:y", floatLabel.Position.Y - 26, 0.6f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        ft.TweenProperty(floatLabel, "modulate:a", 0f, 0.6f);
        ft.Chain().TweenCallback(Callable.From(() => floatLabel.QueueFree()));
    }

    private void UpdateProgress(int quarter, float at)
    {
        float overall = (Math.Clamp(quarter, 1, 4) - 1 + Mathf.Clamp(at, 0f, 1f)) / 4f;
        _progressFill.AnchorRight = overall;
        int remainSec = (int)((1 - at) * 12 * 60);
        _timeLabel.Text = $"剩 {remainSec / 60}'{remainSec % 60:00}\"";
    }

    private static Color FeedColor(string type) => type switch
    {
        "dunk" => Palette.Cinnabar,
        "block" => Palette.StoneBlue,
        "steal" => Palette.StoneBlue,
        "three" => Palette.Gamboge,
        "three_rain" => Palette.Gamboge,
        "combo" => Palette.Cinnabar,
        "foulout" => Palette.Cinnabar,
        "clutch" => Palette.Cinnabar,
        "foul" => Palette.Gray,
        "opening" => Palette.StoneBlue,
        "qend" => Palette.Gray,
        _ => Palette.InkSoft
    };

    private void AddFeedLine(string text, string type)
    {
        var row = UiKit.HBox(6);
        var bar = new ColorRect { Color = FeedColor(type), CustomMinimumSize = new Vector2(4, 16), MouseFilter = MouseFilterEnum.Ignore };
        row.AddChild(bar);
        var bold = type is "dunk" or "block" or "foulout" or "combo" or "three_rain" or "three";
        var lbl = UiKit.LWrap(text, 12, type is "opening" or "qend" ? Palette.Gray : Palette.Ink);
        lbl.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        if (bold) UiKit.SetFont(lbl, true);
        row.AddChild(lbl);
        _feed.AddChild(row);

        if (_feed.GetChildCount() > 60)
            _feed.GetChild(0).QueueFree();

        Fx.CardEnter(row, 0f);
        Callable.From(() =>
        {
            var vs = _feedScroll.GetVScrollBar();
            _feedScroll.ScrollVertical = (int)vs.MaxValue;
        }).CallDeferred();
    }

    // ================= 换人条（§8.4）=================
    private void ShowSubBar(MatchEvent e)
    {
        _subBarHolder.ClearChildren();
        var panel = UiKit.Card(14, new Color(Palette.CardBg, 0.98f), Palette.Cinnabar, 2, 8);
        panel.AnchorLeft = 0; panel.AnchorRight = 1; panel.AnchorTop = 1; panel.AnchorBottom = 1;
        panel.OffsetLeft = 12; panel.OffsetRight = -12; panel.OffsetTop = -150; panel.OffsetBottom = -96;
        panel.MouseFilter = MouseFilterEnum.Stop;
        _subBarHolder.AddChild(panel);

        var v = UiKit.VBox(6);
        panel.AddChild(v);
        var head = UiKit.HBox(6);
        head.AddChild(UiKit.L($"六犯离场！{e.SubInName} 顶替登场（3 秒内可改选）", 12, Palette.Cinnabar, true));
        var sp = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        head.AddChild(sp);
        var countdown = UiKit.L("3", 14, Palette.Gray, true);
        head.AddChild(countdown);
        v.AddChild(head);

        var row = UiKit.HBox(6);
        v.AddChild(row);
        var benchInsts = new List<HeroInst>();
        foreach (var slot in Slots.Bench)
        {
            var inst = GameState.HeroByUid(GetLineupUid(slot));
            if (inst != null && !_enteredBench.Contains(inst.Uid)) benchInsts.Add(inst);
        }
        if (benchInsts.Count == 0)
            row.AddChild(UiKit.L("替补席无人可用", 11, Palette.Gray));

        int subIdx = e.SubIndex;
        bool picked = false;
        foreach (var inst in benchInsts.Take(3))
        {
            var b = UiKit.BtnSecondary(inst.Def.Name, () =>
            {
                if (picked || subIdx < 0) return;
                picked = true;
                _overrides[subIdx] = inst.Uid;
                _result = MatchEngine.Simulate(_playerTeam, _oppTeam, _seed, true, _overrides);
                _subBarHolder.ClearChildren();
                UiKit.Toast($"{inst.Def.Name} 替补登场（已重演后续赛况）");
                RefreshMyRow();
            }, 40, 13);
            row.AddChild(b);
        }

        // 3 秒倒计时（超时自动 = 引擎自动选择）
        var holder = _subBarHolder;
        var tw = countdown.CreateTween();
        tw.TweenMethod(Callable.From((float v) =>
        {
            if (!IsInstanceValid(countdown)) return;
            countdown.Text = Math.Max(0, (int)Math.Ceiling(v)).ToString();
        }), 3f, 0f, 3f).SetTrans(Tween.TransitionType.Linear);
        tw.TweenCallback(Callable.From(() =>
        {
            if (IsInstanceValid(holder)) holder.ClearChildren();
        }));

        Fx.RiseIn(panel, 20f);
    }

    // ================= 控制 =================
    private void SetSpeed(float s)
    {
        _speed = s;
        SetSpeedVisual();
    }

    private void SetSpeedVisual()
    {
        void Style(Button b, bool active)
        {
            b.AddThemeColorOverride("font_color", active ? Colors.White : Palette.Ink);
            b.AddThemeColorOverride("font_hover_color", active ? Colors.White : Palette.Ink);
            b.AddThemeStyleboxOverride("normal", UiKit.SB(active ? Palette.Ink : Palette.CardBg, 22, active ? Palette.Ink : Palette.Ink, 2));
            b.AddThemeStyleboxOverride("hover", UiKit.SB(active ? Palette.Ink : Palette.Paper, 22, Palette.Ink, 2));
            b.AddThemeStyleboxOverride("pressed", UiKit.SB(Palette.Ink, 22, Palette.Ink, 2));
        }
        Style(_b1, Mathf.IsEqualApprox(_speed, 1f));
        Style(_b2, Mathf.IsEqualApprox(_speed, 2f));
        Style(_b4, Mathf.IsEqualApprox(_speed, 4f));
    }

    /// <summary>跳过（v2：单击直接结算整场比赛）。</summary>
    private void OnSkipPressed()
    {
        while (_eventIdx + 1 < _result.Events.Count)
        {
            _eventIdx++;
            var e = _result.Events[_eventIdx];
            _shownA = e.ScoreA;
            _shownB = e.ScoreB;
        }
        _scoreA.Text = _shownA.ToString();
        _scoreB.Text = _shownB.ToString();
        UpdateProgress(4, 1f);
        AddFeedLine("比赛直接跳到终场。", "qend");
        FinishMatch();
    }

    /// <summary>跳节（v2）：快进一节比赛（走到下一节开头结算）。</summary>
    private void OnSkipQuarter()
    {
        if (_finished) return;
        int curQ = _eventIdx >= 0 ? _result.Events[_eventIdx].Quarter : 1;
        int guard = 0;
        while (_eventIdx + 1 < _result.Events.Count && _eventIdx + 1 < _result.Events.Count
               && guard++ < 2000)
        {
            var next = _result.Events[_eventIdx + 1];
            if (next.Quarter > curQ) break;
            _eventIdx++;
            var e = _result.Events[_eventIdx];
            _shownA = e.ScoreA;
            _shownB = e.ScoreB;
        }
        _scoreA.Text = _shownA.ToString();
        _scoreB.Text = _shownB.ToString();
        var last = _eventIdx >= 0 ? _result.Events[_eventIdx] : null;
        int q = last != null ? Math.Clamp(last.Quarter, 1, 4) : 1;
        _quarterLabel.Text = q > 4 ? "加时" : "Q" + q;
        UpdateProgress(q, 1f);
        AddFeedLine($"（已快进至第 {q} 节结束）", "qend");
        // 同步犯规显示
        foreach (var kv in _fouls)
            if (_myCards.TryGetValue(kv.Key, out var card))
                card.SetFouls(kv.Value, kv.Value >= 5);
        RefreshInfoPanel();
        if (_eventIdx + 1 >= _result.Events.Count) FinishMatch();
    }

    private void ToggleAutopilot()
    {
        _autopilot = !_autopilot;
        RefreshAutoVisual();
    }

    private void ShowAutoModePopup()
    {
        var v = UiKit.VBox(8);
        v.AddChild(UiKit.L("托管模式：自动连战，输球时自动暂停", 12, Palette.InkSoft));
        var b1 = UiKit.BtnPrimary("不耗粮托管（体力 0 也继续）", () =>
        {
            _autopilot = true;
            _autopilotFood = false;
            RefreshAutoVisual();
            Popup.Current?.Close();
        }, 46, 14);
        var b2 = UiKit.BtnSecondary("耗粮托管（赛前自动补至 ≥90）", () =>
        {
            _autopilot = true;
            _autopilotFood = true;
            RefreshAutoVisual();
            Popup.Current?.Close();
        }, 46, 14);
        v.AddChild(b1);
        v.AddChild(b2);
        UiKit.ShowPopup("托管设置", v, true, 330);
    }

    private void RefreshAutoVisual()
    {
        _autoBtn.Text = _autopilot ? "托管中 ⇄" : "托管：关";
        _autoCapsule.Text = _autopilot ? (_autopilotFood ? "托管中 · 自动开赛（耗粮）" : "托管中 · 自动开赛") : "";
        _autoCapsule.AddThemeColorOverride("font_color", _autopilot ? Palette.Bamboo : Palette.Gray);
    }

    // ================= 结算 =================
    private void FinishMatch()
    {
        if (_finished) return;
        _finished = true;

        bool won = _result.PlayerWon;
        int myScore = _result.PlayerScore;
        int oppScore = _result.OppScore;
        int margin = _result.Margin;

        // 体力结算（§3.4：上场过 −12）
        ConsumeStamina();
        GameState.D.Stats.TotalMatches++;
        if (margin > GameState.D.Stats.BestMargin) GameState.D.Stats.BestMargin = margin;
        SaveSystem.MarkDirty();

        if (_mode == BattleMode.Normal)
        {
            if (won)
            {
                GameState.D.Stats.TotalWins++;
                var run = GameState.Run;
                run.Streak++;
                run.CurrentK++;
                long coins = WinCoins(run.Streak);
                run.CoinsEarned += coins;
                GameState.AddCoins(coins);
                if (run.Streak > GameState.D.Stats.MaxWinStreak) GameState.D.Stats.MaxWinStreak = run.Streak;
                if (Ftue.Step == 1) Ftue.OnFirstBattleWin();
                SaveSystem.Save();
                ShowResultOverlay(true, myScore, oppScore, coins, run.Streak);
            }
            else
            {
                ShowRevive();
            }
        }
        else
        {
            if (won)
            {
                GameState.D.Stats.TotalWins++;
                var run = GameState.Challenge;
                DungeonSystem.RecordProgress(run.DungeonN, run.Index);
                if (run.Index >= DungeonSystem.TeamsPerDungeon)
                {
                    var reward = DungeonSystem.OnClear(run.DungeonN);
                    SaveSystem.Save();
                    ShowDungeonClear(reward);
                }
                else
                {
                    run.Index++;
                    run.CurrentOpponent = OpponentGen.Dungeon(run.DungeonN, run.Index, OpponentGen.DungeonSeed(run.DungeonN, run.Index));
                    SaveSystem.Save();
                    ShowResultOverlay(true, myScore, oppScore, 0, 0);
                }
            }
            else
            {
                ShowResultOverlay(false, myScore, oppScore, 0, 0);
            }
        }
    }

    private static long WinCoins(int streak)
    {
        var c = ConfigDb.I.C;
        double v = (c.WinCoinBase + c.WinCoinGrowth * (streak - 1)) * (1 + c.CoinDungeonBonus * GameState.D.DungeonClearedCount);
        return (long)Math.Round(v);
    }

    private void ConsumeStamina()
    {
        int cost = ConfigDb.I.C.StaminaCostPerMatch;
        var seen = new HashSet<string>();
        foreach (var slot in Slots.Field.Concat(Slots.Bench))
        {
            var inst = GameState.HeroByUid(GetLineupUid(slot));
            if (inst == null) continue;
            bool played = Slots.IsField(slot) || _enteredBench.Contains(inst.Uid);
            if (played && seen.Add(inst.Uid))
                inst.Sta = Math.Max(0, inst.Sta - cost);
        }
        SaveSystem.MarkDirty();
    }

    private void ShowResultOverlay(bool won, int my, int opp, long coins, int streak)
    {
        var panel = UiKit.Card(16, new Color(Palette.CardBg, 0.98f), won ? Palette.Gamboge : Palette.Gray, 2, 14);
        panel.AnchorLeft = 0.5f; panel.AnchorRight = 0.5f; panel.AnchorTop = 0.5f; panel.AnchorBottom = 0.5f;
        panel.OffsetLeft = -160; panel.OffsetRight = 160; panel.OffsetTop = -170; panel.OffsetBottom = 170;
        panel.MouseFilter = MouseFilterEnum.Stop;
        AddChild(panel);
        Fx.PopIn(panel);

        var v = UiKit.VBox(8);
        panel.AddChild(v);

        var title = UiKit.L(won ? "胜 利" : "惜 败", 30, won ? Palette.Cinnabar : Palette.Gray, true);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(title);

        var scoreLine = UiKit.L($"{my} : {opp}", 26, Palette.Ink, true);
        scoreLine.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(scoreLine);

        if (won && _mode == BattleMode.Normal)
        {
            v.AddChild(UiKit.L($"连胜 {streak} 场 · 金币 +{coins:N0}", 14, Palette.Gamboge, true));
        }
        if (won && _mode == BattleMode.Challenge)
        {
            v.AddChild(UiKit.L($"已击败 {GameState.Challenge.Index - 1}/10 队", 13, Palette.StoneBlue, true));
        }

        // MVP 与数据
        var mvp = _result.BoxA.FirstOrDefault();
        if (mvp != null)
        {
            v.AddChild(UiKit.L($"MVP：{mvp.Name} {mvp.Points} 分 · {mvp.Blocks} 帽 · {mvp.Steals} 断", 12, Palette.InkSoft));
        }
        v.AddChild(UiKit.L($"终场 {_result.FinalA}:{_result.FinalB} · 分差 {_result.Margin}", 11, Palette.Gray));

        var btnRow = UiKit.HBox(6);
        if (won)
        {
            string nextText = _mode == BattleMode.Normal ? "下一场" : "下一队";
            var next = UiKit.BtnPrimary(nextText, () =>
            {
                panel.QueueFree();
                ContinueNextMatch();
            }, 50, 17);
            next.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            btnRow.AddChild(next);

            // v2：结算界面可直接更换球员（替补席换上）
            var swap = UiKit.BtnSecondary("更换球员", () => ShowSwapPopup(), 50, 13);
            swap.CustomMinimumSize = new Vector2(96, 50);
            btnRow.AddChild(swap);

            var exit = UiKit.BtnSecondary(_mode == BattleMode.Normal ? "结束本局" : "退出挑战", () =>
            {
                panel.QueueFree();
                EndRun(false);
            }, 50, 13);
            exit.CustomMinimumSize = new Vector2(88, 50);
            btnRow.AddChild(exit);
        }
        else
        {
            var back = UiKit.BtnPrimary(_mode == BattleMode.Normal ? "结束本局" : "返回挑战地图", () =>
            {
                panel.QueueFree();
                EndRun(false);
            }, 50, 17);
            back.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            btnRow.AddChild(back);
        }
        v.AddChild(btnRow);

        if (_autopilot && won)
        {
            var tip = UiKit.L("托管中 · 1.5 秒后自动开赛（点此处取消）", 10, Palette.Bamboo);
            tip.HorizontalAlignment = HorizontalAlignment.Center;
            v.AddChild(tip);
            tip.GuiInput += ev =>
            {
                if (ev is InputEventMouseButton { Pressed: true })
                {
                    _autopilot = false;
                    RefreshAutoVisual();
                    tip.Text = "已取消自动续战";
                }
            };
            _autoContinuePending = true;
            _autoContinueAt = Time.GetTicksMsec() / 1000.0 + 1.5;
        }
    }

    // ================= 复活（ADP_REVIVE）=================
    private void ShowRevive()
    {
        var run = GameState.Run;
        var v = UiKit.VBox(8);
        v.AddChild(UiKit.LWrap(
            $"本轮已连胜 {run.Streak} 场，累计 {run.CoinsEarned:N0} 金币。\n看一段视频，全队体力回满并再战一次！",
            13, Palette.InkSoft));

        bool reviveLeft = !run.ReviveUsed;
        if (reviveLeft)
        {
            var adWrap = UiKit.AdButton("看视频 · 全队体力回满 + 再战 1 次", Fx.Nested("ad_ring_seconds", 15f), () =>
            {
                Ads.ShowRevive(ok =>
                {
                    if (!ok)
                    {
                        UiKit.Toast("广告暂不可用，可直接放弃本局");
                        return;
                    }
                    Popup.Current?.Close();
                    ReviveAndRematch();
                });
            });
            v.AddChild(adWrap);
        }
        else
        {
            v.AddChild(UiKit.L("本局复活机会已用完", 12, Palette.Gray));
        }

        var giveUp = UiKit.BtnSecondary("结束本局", () =>
        {
            Popup.Current?.Close();
            EndRun(false);
        }, 46, 15);
        v.AddChild(giveUp);

        UiKit.ShowPopup("复活一次？", v, false, 336);
    }

    private void ReviveAndRematch()
    {
        // 全队体力回满（v2：上限为各自体力上限）
        foreach (var p in LineupUtil.FieldPlayers())
            if (p != null) p.Sta = HeroCalc.StaCapTotal(p);
        foreach (var b in LineupUtil.BenchPlayers())
            if (b != null) b.Sta = HeroCalc.StaCapTotal(b);
        SaveSystem.Save();
        UiKit.Toast("全队体力已回满！再战同一对手");
        StartMatch(); // 重新开一场（同对手，新种子）
    }

    // ================= 续战 / 结束 =================
    private void ContinueNextMatch()
    {
        if (_mode == BattleMode.Normal)
        {
            var run = GameState.Run;
            // 耗粮托管：赛前补体力
            if (_autopilot && _autopilotFood) RefillWithFood();
            run.CurrentOpponent = OpponentGen.Normal(run.CurrentK, GameRandom.NewSeed());
            SaveSystem.MarkDirty();
            StartMatch();
        }
        else
        {
            var run = GameState.Challenge;
            if (run.CurrentOpponent == null)
            {
                run.CurrentOpponent = OpponentGen.Dungeon(run.DungeonN, run.Index, OpponentGen.DungeonSeed(run.DungeonN, run.Index));
            }
            StartMatch();
        }
    }

    private void RefillWithFood()
    {
        var field = LineupUtil.FieldPlayers().Where(p => p != null).ToList();
        bool Need() => field.Any(p => p.Sta < 90);
        // 顺序：粮草（全队） → 军粮 → 干粮（上限为各自体力上限）
        while (Need() && GameState.GetFood("forage") > 0)
        {
            GameState.UseFood("forage", 1);
            foreach (var p in field) p.Sta = Math.Min(HeroCalc.StaCapTotal(p), p.Sta + 35);
        }
        while (Need() && GameState.GetFood("ration") > 0)
        {
            GameState.UseFood("ration", 1);
            var lowest = field.OrderBy(p => p.Sta).First();
            lowest.Sta = Math.Min(HeroCalc.StaCapTotal(lowest), lowest.Sta + 50);
        }
        while (Need() && GameState.GetFood("dry") > 0)
        {
            GameState.UseFood("dry", 1);
            var lowest = field.OrderBy(p => p.Sta).First();
            lowest.Sta = Math.Min(HeroCalc.StaCapTotal(lowest), lowest.Sta + 25);
        }
        if (Need())
        {
            _autopilotFood = false;
            RefreshAutoVisual();
            UiKit.Toast("兵粮不足，已自动降级为「不耗粮」托管");
        }
        SaveSystem.MarkDirty();
    }

    /// <summary>结算界面 · 更换球员（v2）：替补席换上，下一场生效。</summary>
    private void ShowSwapPopup(string benchUid = null)
    {
        var v = UiKit.VBox(6);
        if (string.IsNullOrEmpty(benchUid))
        {
            v.AddChild(UiKit.L("先选择要换上场的替补球员", 12, Palette.Gray));
            var bench = LineupUtil.AvailableBench();
            if (bench.Count == 0)
            {
                v.AddChild(UiKit.L("替补席没有球员（可在阵容页解锁并指派）", 12, Palette.Gray));
            }
            foreach (var h in bench)
            {
                string uid = h.Uid;
                var row = ItemUi.HeroRow(h, "换上", () =>
                {
                    Popup.Current?.Close();
                    ShowSwapPopup(uid);
                }, true);
                v.AddChild(row);
            }
        }
        else
        {
            v.AddChild(UiKit.L("再选择被换下的场上球员", 12, Palette.Gray));
            foreach (var slot in Slots.Field)
            {
                var inst = GameState.HeroByUid(GetLineupUid(slot));
                if (inst == null) continue;
                string s = slot;
                var row = ItemUi.HeroRow(inst, "换下（" + Slots.Cn(s) + "）", () => SwapLineup(benchUid, s), true);
                v.AddChild(row);
            }
        }
        UiKit.ShowPopup("更换球员", v, true, 330);
    }

    private void SwapLineup(string benchUid, string fieldSlot)
    {
        var lineup = GameState.D.Lineup;
        string benchSlot = Slots.Bench.FirstOrDefault(s => lineup.TryGetValue(s, out var u) && u == benchUid);
        if (benchSlot == null)
        {
            UiKit.Toast("该球员不在替补席");
            return;
        }
        lineup.TryGetValue(fieldSlot, out var oldUid);
        if (string.IsNullOrEmpty(oldUid)) lineup.Remove(benchSlot);
        else lineup[benchSlot] = oldUid;
        lineup[fieldSlot] = benchUid;
        SaveSystem.Save();
        GameState.TouchBestTeamOvr();
        Popup.Current?.Close();
        UiKit.Toast("已更换球员，下一场生效");
    }

    private void EndRun(bool viaSummaryOnly)
    {
        _autoContinuePending = false;
        // v2：体力消耗只在一局内呈现 —— 局末全队回满
        GameState.RestoreAllStamina();
        GameState.TouchBestTeamOvr();
        if (_mode == BattleMode.Normal)
        {
            var run = GameState.Run;
            GameState.D.Stats.TotalRuns++;
            run.Active = false;
            SaveSystem.Save();
            UiKit.PopScreen();
            UiKit.PushScreen(new RunSummaryScreen(run));
        }
        else
        {
            GameState.Challenge.Active = false;
            SaveSystem.Save();
            UiKit.PopScreen();
            Callable.From(() =>
            {
                if (ScreenManager.I.Top is ChallengeScreen cs) cs.Rebuild();
            }).CallDeferred();
        }
    }

    private void ShowDungeonClear(List<DrawItem> reward)
    {
        var v = UiKit.VBox(8);
        v.AddChild(UiKit.L("大 捷！本副本 10 队全部击破", 15, Palette.Cinnabar, true));
        v.AddChild(UiKit.L("解锁下一副本，通关奖励已入包：", 12, Palette.InkSoft));
        var grid = new GridContainer { Columns = 6 };
        foreach (var r in reward) grid.AddChild(ItemUi.DrawCell(r, 48));
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, reward.Count > 18 ? 190 : 100) };
        scroll.AddChild(grid);
        v.AddChild(scroll);
        var back = UiKit.BtnPrimary("返回挑战地图", () =>
        {
            Popup.Current?.Close();
            UiKit.PopScreen();
            Callable.From(() =>
            {
                if (ScreenManager.I.Top is ChallengeScreen cs) cs.Rebuild();
            }).CallDeferred();
        }, 48, 16);
        v.AddChild(back);
        UiKit.ShowPopup("通关结算", v, false, 344);
    }
}

/// <summary>本局总结页（普通模式）。</summary>
public partial class RunSummaryScreen : Control
{
    private readonly NormalRun _run;

    public RunSummaryScreen(NormalRun run)
    {
        _run = run;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var bg = new ColorRect { Color = Palette.Paper, MouseFilter = MouseFilterEnum.Ignore };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var margin = UiKit.Pad(24, 60, 24, 16);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(margin);
        var v = UiKit.VBox(10);
        margin.AddChild(v);

        var title = UiKit.L("本 局 总 结", 22, Palette.Ink, true);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(title);

        var card = UiKit.Card(14, Palette.CardBg, Palette.Ink, 2, 14);
        var cv = UiKit.VBox(8);
        card.AddChild(cv);
        void Stat(string k, string val)
        {
            var h = UiKit.HBox(6);
            h.AddChild(UiKit.L(k, 13, Palette.Gray));
            var sp = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            h.AddChild(sp);
            h.AddChild(UiKit.L(val, 15, Palette.Ink, true));
            cv.AddChild(h);
        }
        Stat("连胜场次", $"{_run.Streak} 场");
        Stat("累计金币", $"+{_run.CoinsEarned:N0}");
        Stat("最高分差", $"{_run.BestMargin}");
        Stat("历史最高连胜", $"{GameState.D.Stats.MaxWinStreak} 场");
        v.AddChild(card);

        var tip = UiKit.L("输是新的开始 —— 下一局从第 1 场弱队重新出发！", 12, Palette.Gray);
        tip.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(tip);

        var fill = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
        v.AddChild(fill);

        var back = UiKit.BtnPrimary("回 主 页", () =>
        {
            UiKit.PopScreen();
            Callable.From(() =>
            {
                if (TabsScreen.I != null) TabsScreen.I.SwitchTo("main");
                if (Ftue.Step == 2) Ftue.TipGacha();
            }).CallDeferred();
        }, 56, 20);
        v.AddChild(back);
    }
}
