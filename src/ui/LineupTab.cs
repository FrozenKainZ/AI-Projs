using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using SanguoBasketball.Core;
using SanguoBasketball.Systems;

namespace SanguoBasketball.UI;

/// <summary>SCR_LINEUP · 阵容编辑：11 槽位管理（5 场上 + 3 教练 + 3 替补）。</summary>
public partial class LineupTab : Control
{
    private string _selectedSlot = "PG";
    private Dictionary<string, string> _undoSnapshot;
    private double _undoAt = -999;

    private HBoxContainer _fieldRow, _coachRow, _benchRow;
    private VBoxContainer _listBox;
    private NumberLabel _teamOvr;
    private Button _undoBtn;
    private Label _hint;

    public override void _Ready()
    {
        Build();
        Refresh();
    }

    private void Build()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var margin = UiKit.Pad(16, 48, 16, 4);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(margin);
        var root = UiKit.VBox(8);
        margin.AddChild(root);

        // 头部
        var head = UiKit.HBox(8);
        head.AddChild(UiKit.L("阵容编辑", 18, Palette.Ink, true));
        var sp = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        head.AddChild(sp);
        head.AddChild(UiKit.BtnSecondary("一键最优", () =>
        {
            SnapshotUndo();
            LineupUtil.AutoFill();
            Refresh();
            UiKit.Toast("已按 OVR + 位置偏好自动填充");
        }, 36, 13));
        root.AddChild(head);

        // 场上五人
        root.AddChild(SectionLabel("场上五人（位置偏好 ×1.15）"));
        _fieldRow = UiKit.HBox(8);
        root.AddChild(_fieldRow);

        // 教练席
        root.AddChild(SectionLabel("教练席（不耗体力 · 提供光环）"));
        _coachRow = UiKit.HBox(8);
        root.AddChild(_coachRow);

        // 替补席
        root.AddChild(SectionLabel("替补席（比赛不耗体力 · 六犯顶替）"));
        _benchRow = UiKit.HBox(8);
        root.AddChild(_benchRow);

        // 总评 + 撤销
        var ovrRow = UiKit.HBox(8);
        ovrRow.AddChild(UiKit.L("队伍总评", 13, Palette.Gray));
        _teamOvr = new NumberLabel(HeroCalc.TeamOvr(GameState.D.Lineup), 20, Palette.StoneBlue);
        ovrRow.AddChild(_teamOvr);
        var sp2 = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        ovrRow.AddChild(sp2);
        _undoBtn = UiKit.BtnSecondary("撤销", DoUndo, 32, 12);
        ovrRow.AddChild(_undoBtn);
        root.AddChild(ovrRow);

        _hint = UiKit.L("点选槽位后，在下方列表里点卡牌指派；再次点击同一槽位可取消选择", 11, Palette.Gray);
        root.AddChild(_hint);

        // 候选列表
        root.AddChild(ItemUi.HRule());
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        root.AddChild(scroll);
        _listBox = UiKit.VBox(6);
        _listBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_listBox);
    }

    private Control SectionLabel(string text)
    {
        var l = UiKit.L(text, 12, Palette.InkSoft, true);
        return l;
    }

    public override void _Process(double delta)
    {
        if (_undoBtn != null)
            _undoBtn.Disabled = _undoSnapshot == null || Time.GetTicksMsec() / 1000.0 - _undoAt > 30;
    }

    private void SnapshotUndo()
    {
        _undoSnapshot = new Dictionary<string, string>(GameState.D.Lineup);
        _undoAt = Time.GetTicksMsec() / 1000.0;
    }

    private void DoUndo()
    {
        if (_undoSnapshot == null) return;
        GameState.D.Lineup = new Dictionary<string, string>(_undoSnapshot);
        SaveSystem.MarkDirty();
        _undoSnapshot = null;
        Refresh();
        UiKit.Toast("已撤销上一次阵容变化");
    }

    private void Refresh()
    {
        // 场上（恒解锁）
        _fieldRow.ClearChildren();
        foreach (var slot in Slots.Field)
            _fieldRow.AddChild(BuildSlot(slot, false));
        // 教练（v2：需解锁）
        _coachRow.ClearChildren();
        int aura = HeroCalc.CoachAura(LineupUtil.CoachPlayers());
        foreach (var slot in Slots.Coach)
            _coachRow.AddChild(GameState.IsSlotUnlocked(slot)
                ? BuildSlot(slot, true, aura)
                : BuildLockedSlot(slot));
        // 替补（v2：需解锁）
        _benchRow.ClearChildren();
        foreach (var slot in Slots.Bench)
            _benchRow.AddChild(GameState.IsSlotUnlocked(slot)
                ? BuildSlot(slot, false, 0, true)
                : BuildLockedSlot(slot));

        _teamOvr?.SetValue(HeroCalc.TeamOvr(GameState.D.Lineup), true);
        RefreshList();
    }

    /// <summary>未解锁槽位（金币解锁）。</summary>
    private Control BuildLockedSlot(string slot)
    {
        var col = UiKit.VBox(2);
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var badge = UiKit.L(Slots.Cn(slot), 10, Palette.Gray, true);
        badge.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(badge);

        var holder = new Control { CustomMinimumSize = new Vector2(0, 76), MouseFilter = MouseFilterEnum.Stop };
        col.AddChild(holder);
        var panel = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
        panel.OffsetLeft = -26; panel.OffsetRight = 26; panel.OffsetTop = 2; panel.OffsetBottom = 72;
        panel.AddThemeStyleboxOverride("panel", UiKit.SB(new Color(0, 0, 0, 0.04f), 10, Palette.Gray, 2));
        holder.AddChild(panel);
        var lockLabel = UiKit.L("🔒", 15, Palette.Gray);
        lockLabel.HorizontalAlignment = HorizontalAlignment.Center;
        lockLabel.VerticalAlignment = VerticalAlignment.Center;
        lockLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        lockLabel.MouseFilter = MouseFilterEnum.Ignore;
        panel.AddChild(lockLabel);

        long cost = GameState.SlotUnlockCost(slot);
        var costLabel = UiKit.L($"{cost:N0} 金币", 9, Palette.Gamboge, true);
        costLabel.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(costLabel);

        holder.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                UiKit.Confirm($"解锁{Slots.Cn(slot)}",
                    $"花费 {cost:N0} 金币解锁该槽位。\n解锁后可在此席位指派武将。",
                    $"解锁（{cost:N0} 金币）", () =>
                    {
                        if (GameState.TryUnlockSlot(slot))
                        {
                            UiKit.Toast($"{Slots.Cn(slot)} 已解锁！");
                            Refresh();
                        }
                        else
                        {
                            UiKit.Toast("金币不足");
                        }
                    }, "取消", false);
            }
        };
        return col;
    }

    private Control BuildSlot(string slot, bool coach, int aura = 0, bool bench = false)
    {
        GameState.D.Lineup.TryGetValue(slot, out var uid);
        var inst = GameState.HeroByUid(uid);
        bool selected = _selectedSlot == slot;

        var col = UiKit.VBox(2);
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var badge = UiKit.L(Slots.Cn(slot), 10, selected ? Palette.Cinnabar : Palette.Gray, true);
        badge.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(badge);

        var holder = new Control { CustomMinimumSize = new Vector2(0, 76), MouseFilter = MouseFilterEnum.Stop };
        col.AddChild(holder);

        if (inst != null)
        {
            var card = new HeroCard(52);
            card.Setup(inst, null, !coach);
            card.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
            card.OffsetLeft = -26; card.OffsetRight = 26;
            card.MouseFilter = MouseFilterEnum.Ignore;
            holder.AddChild(card);
        }
        else
        {
            var empty = new Panel { MouseFilter = MouseFilterEnum.Ignore };
            empty.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
            empty.OffsetLeft = -26; empty.OffsetRight = 26; empty.OffsetTop = 2; empty.OffsetBottom = 72;
            empty.AddThemeStyleboxOverride("panel", UiKit.SB(new Color(0, 0, 0, 0.03f), 10,
                bench ? Palette.Gray : new Color(0, 0, 0, 0.20f), 2));
            holder.AddChild(empty);
            var plus = UiKit.L("+", 22, Palette.Gray, true);
            plus.HorizontalAlignment = HorizontalAlignment.Center;
            plus.VerticalAlignment = VerticalAlignment.Center;
            plus.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            plus.MouseFilter = MouseFilterEnum.Ignore;
            empty.AddChild(plus);
        }

        if (coach)
        {
            var al = UiKit.L(inst != null ? $"+{AuraOf(inst, slot, aura)}" : "光环 0", 10, Palette.Gamboge, true);
            al.HorizontalAlignment = HorizontalAlignment.Center;
            col.AddChild(al);
        }

        if (selected)
        {
            var ring = new Panel { MouseFilter = MouseFilterEnum.Ignore };
            ring.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            ring.AddThemeStyleboxOverride("panel", UiKit.SB(new Color(0, 0, 0, 0), 10, Palette.Bamboo, 2));
            holder.AddChild(ring);
        }

        holder.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                _selectedSlot = selected ? null : slot;
                Refresh();
            }
        };
        return col;
    }

    private static int AuraOf(HeroInst coach, string slot, int total)
    {
        int ovr = HeroCalc.Ovr(coach);
        double rate = slot == "HC" ? ConfigDb.I.C.AuraHcRate : ConfigDb.I.C.AuraAcRate;
        return (int)MathF.Round(ovr * (float)rate);
    }

    private void RefreshList()
    {
        _listBox.ClearChildren();
        if (string.IsNullOrEmpty(_selectedSlot))
        {
            _listBox.AddChild(UiKit.L("点选上方任意槽位以查看候选", 13, Palette.Gray));
            return;
        }

        var slot = _selectedSlot;
        GameState.D.Lineup.TryGetValue(slot, out var curUid);
        var cur = GameState.HeroByUid(curUid);
        string title = $"「{Slots.Cn(slot)}」候选（偏好匹配优先）";
        _listBox.AddChild(UiKit.L(title, 12, Palette.InkSoft, true));

        var candidates = LineupUtil.CandidatesFor(slot);
        int shown = 0;
        foreach (var h in candidates)
        {
            if (shown >= 60) break;
            shown++;
            int score = HeroCalc.SlotOvr(h, slot);
            int baseScore = cur != null ? HeroCalc.SlotOvr(cur, slot) : 0;
            int delta = score - baseScore;
            bool pref = h.Def.Pref == slot;
            string deltaText = cur == null ? "空位" + (pref ? " ★适配" : "")
                : (delta > 0 ? $"↑{delta}" : delta < 0 ? $"↓{-delta}" : "—") + (pref ? " ★适配" : "");
            bool isCurrent = h.Uid == curUid;

            var right = UiKit.L($"{score}" + (isCurrent ? "（当前）" : ""), 13, isCurrent ? Palette.StoneBlue : Palette.Ink);
            var dr = UiKit.L(deltaText, 11, delta >= 0 ? Palette.Bamboo : Palette.Cinnabar);
            var dv = UiKit.VBox(0);
            dv.AddChild(right);
            dv.AddChild(dr);

            var detailBtn = UiKit.BtnGhost("详", () => UiKit.PushScreen(new HeroDetailScreen(h)), 12, Palette.StoneBlue);
            bool inLineup = LineupUtil.IsInLineup(h) && !isCurrent;
            var row = ItemUi.Row(Art.Hero(h.DefId), h.Def.FullName, $"{h.Star}★ · OVR {HeroCalc.Ovr(h)} · 体力 {h.Sta}/{HeroCalc.StaCapTotal(h)}", "", () =>
            {
                if (isCurrent)
                {
                    _selectedSlot = null;
                    Refresh();
                    return;
                }
                AssignToSlot(h);
            }, true, Quality.Color(h.Def.QualityKey));

            // 在行尾追加 已出战标签 + delta + 详情按钮
            var box = (HBoxContainer)row.GetChild(0);
            if (inLineup)
            {
                var chip = UiKit.Card(4, Palette.Bamboo, null, 0, 1);
                var chipLabel = UiKit.L("已出战", 9, Colors.White, true);
                chipLabel.MouseFilter = MouseFilterEnum.Ignore;
                chip.AddChild(chipLabel);
                chip.MouseFilter = MouseFilterEnum.Ignore;
                box.AddChild(chip);
            }
            box.AddChild(dv);
            box.AddChild(detailBtn);
            _listBox.AddChild(row);
        }
    }

    private void AssignToSlot(HeroInst inst)
    {
        SnapshotUndo();
        var lineup = GameState.D.Lineup;
        lineup.TryGetValue(_selectedSlot, out var oldUid);

        // 该卡若在别的槽 → 交换
        string otherSlot = null;
        foreach (var kv in lineup)
        {
            if (kv.Value == inst.Uid) { otherSlot = kv.Key; break; }
        }
        if (otherSlot != null)
        {
            if (string.IsNullOrEmpty(oldUid)) lineup.Remove(otherSlot);
            else lineup[otherSlot] = oldUid;
        }
        lineup[_selectedSlot] = inst.Uid;
        SaveSystem.MarkDirty();
        GameState.TouchBestTeamOvr();
        Ftue.OnLineupTouched();
        Refresh();
    }
}
