using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using SanguoBasketball.Core;
using SanguoBasketball.Systems;

namespace SanguoBasketball.UI;

/// <summary>SCR_BAG · 军库（材料 / 兵粮 / 虎符 / 武将；含批量回收与武将分类）。</summary>
public partial class BagTab : Control
{
    private string _cat = "mat";
    private bool _batch;
    private readonly HashSet<string> _selected = new();
    private readonly Dictionary<string, bool> _qualityChecked = new()
    {
        ["green"] = false, ["blue"] = false, ["purple"] = false, ["gold"] = false
    };

    // 武将分类筛选
    private string _faction = "全部";
    private string _quality = "全部";
    private int _star = 0;

    private VBoxContainer _root;
    private VBoxContainer _listBox;
    private VBoxContainer _filtersBox;
    private HBoxContainer _qualityBar;
    private HBoxContainer _summaryBar;
    private Label _summaryLabel;
    private Button _batchBtn;
    private readonly Dictionary<string, Button> _catBtns = new();

    public override void _Ready()
    {
        Build();
        Refresh();
    }

    private void Build()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var margin = UiKit.Pad(16, 4, 16, 4);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(margin);
        _root = UiKit.VBox(7);
        margin.AddChild(_root);

        var head = UiKit.HBox(8);
        head.AddChild(UiKit.L("军 库", 18, Palette.Ink, true));
        var sp = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        head.AddChild(sp);
        _batchBtn = UiKit.BtnSecondary("批量回收", ToggleBatch, 34, 13);
        head.AddChild(_batchBtn);
        _root.AddChild(head);

        // 分类 Tab（v2：聚焦状态修复）
        var catRow = UiKit.HBox(6);
        foreach (var (id, cn) in new[] { ("mat", "材料"), ("food", "兵粮"), ("tali", "虎符"), ("hero", "武将") })
        {
            string cid = id;
            var b = UiKit.BtnSecondary(cn, () =>
            {
                _cat = cid;
                _batch = false;
                _selected.Clear();
                ResetQualityChecks();
                Refresh();
            }, 36, 13);
            b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _catBtns[id] = b;
            catRow.AddChild(b);
        }
        _root.AddChild(catRow);

        // 武将筛选栏（仅武将页显示）
        _filtersBox = UiKit.VBox(3);
        _root.AddChild(_filtersBox);

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _root.AddChild(scroll);
        _listBox = UiKit.VBox(6);
        _listBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_listBox);

        // 批量汇总条（含四色勾选框）
        _summaryBar = UiKit.HBox(6);
        _summaryLabel = UiKit.L("", 11, Palette.Ink);
        _summaryLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _summaryBar.AddChild(_summaryLabel);
        _qualityBar = UiKit.HBox(4);
        _summaryBar.AddChild(_qualityBar);
        _summaryBar.AddChild(UiKit.BtnDanger("回收", DoBatchRecycle, 34, 13));
        _root.AddChild(_summaryBar);
        _summaryBar.Visible = false;
    }

    private void StyleCatTabs()
    {
        foreach (var (id, b) in _catBtns)
        {
            bool active = id == _cat;
            b.AddThemeColorOverride("font_color", active ? Colors.White : Palette.Ink);
            b.AddThemeColorOverride("font_pressed_color", active ? Colors.White : Palette.Ink);
            b.AddThemeColorOverride("font_hover_color", active ? Colors.White : Palette.Ink);
            b.AddThemeStyleboxOverride("normal", UiKit.SB(active ? Palette.Cinnabar : Palette.CardBg, 12, active ? Palette.Cinnabar : Palette.Ink, 2));
            b.AddThemeStyleboxOverride("hover", UiKit.SB(active ? Palette.Cinnabar : Palette.Paper, 12, active ? Palette.Cinnabar : Palette.Ink, 2));
            b.AddThemeStyleboxOverride("pressed", UiKit.SB(Palette.Cinnabar.Darkened(0.1f), 12, Palette.Cinnabar, 2));
        }
    }

    private void RebuildQualityBar()
    {
        _qualityBar.ClearChildren();
        foreach (var (q, cn) in new[] { ("green", "绿"), ("blue", "蓝"), ("purple", "紫"), ("gold", "金") })
        {
            string quality = q;
            bool on = _qualityChecked[quality];
            var b = UiKit.BtnSecondary((on ? "☑" : "☐") + cn, () => ToggleQuality(quality), 30, 12);
            b.CustomMinimumSize = new Vector2(46, 30);
            if (on)
            {
                b.AddThemeColorOverride("font_color", Colors.White);
                b.AddThemeColorOverride("font_hover_color", Colors.White);
                b.AddThemeColorOverride("font_pressed_color", Colors.White);
                b.AddThemeStyleboxOverride("normal", UiKit.SB(Quality.Color(quality).Darkened(0.15f), 12));
                b.AddThemeStyleboxOverride("hover", UiKit.SB(Quality.Color(quality).Darkened(0.05f), 12));
                b.AddThemeStyleboxOverride("pressed", UiKit.SB(Quality.Color(quality), 12));
            }
            _qualityBar.AddChild(b);
        }
    }

    private void ResetQualityChecks()
    {
        foreach (var k in _qualityChecked.Keys.ToList()) _qualityChecked[k] = false;
    }

    private void ToggleBatch()
    {
        _batch = !_batch;
        _selected.Clear();
        ResetQualityChecks();
        _batchBtn.Text = _batch ? "退出批量" : "批量回收";
        Refresh();
    }

    private void ToggleQuality(string quality)
    {
        bool turnOn = !_qualityChecked[quality];
        _qualityChecked[quality] = turnOn;
        if (_cat == "mat")
        {
            foreach (var kv in GameState.D.Mats)
            {
                if (kv.Value <= 0) continue;
                var parts = kv.Key.Split(':');
                if (parts.Length != 2 || parts[1] != quality) continue;
                string key = "mat:" + kv.Key;
                if (turnOn) _selected.Add(key);
                else _selected.Remove(key);
            }
        }
        else if (_cat == "hero")
        {
            foreach (var h in GameState.D.Heroes)
            {
                if (h.Def.QualityKey != quality) continue;
                string key = "hero:" + h.Uid;
                if (turnOn && !GameState.IsEquipped(h)) _selected.Add(key);
                if (!turnOn) _selected.Remove(key);
            }
        }
        Refresh();
    }

    // ---------------- 列表 ----------------
    private void Refresh()
    {
        StyleCatTabs();
        _listBox.ClearChildren();
        _filtersBox.Visible = _cat == "hero";
        switch (_cat)
        {
            case "mat": BuildMats(); break;
            case "food": BuildFoods(); break;
            case "tali": BuildTalis(); break;
            case "hero": BuildHeroes(); break;
        }
        long total = BatchValue();
        _summaryBar.Visible = _batch;
        if (_batch) RebuildQualityBar();
        _summaryLabel.Text = _selected.Count == 0
            ? "勾选品质或点选条目"
            : $"已选 {_selected.Count} 件 · 可回收 {total:N0} 金币";
    }

    private void BuildMats()
    {
        var entries = new List<(string key, string typeId, string quality, int count)>();
        foreach (var kv in GameState.D.Mats)
        {
            if (kv.Value <= 0) continue;
            var parts = kv.Key.Split(':');
            if (parts.Length != 2) continue;
            entries.Add((kv.Key, parts[0], parts[1], kv.Value));
        }
        entries.Sort((a, b) =>
        {
            int q = Quality.Tier(b.quality).CompareTo(Quality.Tier(a.quality));
            return q != 0 ? q : string.CompareOrdinal(a.typeId, b.typeId);
        });
        if (entries.Count == 0)
        {
            _listBox.AddChild(UiKit.L("没有材料，去城池·市场抽一批吧", 12, Palette.Gray));
            return;
        }
        foreach (var e in entries)
        {
            string key = "mat:" + e.key;
            int price = ConfigDb.I.C.RecycleMat.TryGetValue(e.quality, out int p) ? p : 0;
            bool selected = _selected.Contains(key);
            var row = ItemUi.MatRow(e.typeId, e.quality, e.count,
                _batch ? () => ToggleSel(key) : () => ShowMatDetail(e.typeId, e.quality, price),
                _batch ? (selected ? "✓ 已选" : "点选") : ("回收 " + price + " 金/个"),
                forceEnabled: true);
            if (selected) Highlight(row);
            _listBox.AddChild(row);
        }
    }

    private void ShowMatDetail(string typeId, string quality, int unitPrice)
    {
        var special = ConfigDb.I.SpecialMat(typeId);
        var mt = ConfigDb.I.MatType(typeId);
        var v = UiKit.VBox(8);
        string desc;
        if (special != null)
        {
            desc = $"{special.Name}·{Quality.Cn(quality)}：{special.StatCn} 提升，但大幅降低 {Attr.Cns[special.SubIdx]}（独立资源不参与乘区）。";
        }
        else
        {
            desc = $"{mt.Name}·{Quality.Cn(quality)}：{Attr.Cns[mt.MainIdx]} 主加成 / {Attr.Cns[mt.SubIdx]} 副减益。给武将使用时永久提升属性。";
        }
        v.AddChild(UiKit.LWrap(desc, 12, Palette.InkSoft));
        var row = UiKit.HBox(8);
        row.Alignment = BoxContainer.AlignmentMode.Center;
        row.AddChild(UiKit.BtnPrimary("去武将详情使用", () =>
        {
            Popup.Current?.Close();
            var best = GameState.D.Heroes.OrderByDescending(h => HeroCalc.Ovr(h)).FirstOrDefault();
            if (best != null) UiKit.PushScreen(new HeroDetailScreen(best));
        }, 42, 13));
        row.AddChild(UiKit.BtnDanger($"回收 +{unitPrice} 金", () =>
        {
            if (GameState.UseMat(typeId, quality, 1))
            {
                GameState.AddCoins(unitPrice);
                SaveSystem.Save();
                Popup.Current?.Close();
                Refresh();
                UiKit.Toast($"回收成功 +{unitPrice} 金币");
            }
        }, 42, 13));
        v.AddChild(row);
        UiKit.ShowPopup((special != null ? special.Name : mt.Name) + " · 材料", v, true, 320);
    }

    private void BuildFoods()
    {
        foreach (var fd in ConfigDb.I.C.Ext.FoodDefs)
        {
            int count = GameState.GetFood(fd.Key);
            var row = ItemUi.FoodRow(fd.Key, count, () => ShowFoodUse(fd));
            if (count <= 0) row.Modulate = new Color(1, 1, 1, 0.6f);
            _listBox.AddChild(row);
        }
        _listBox.AddChild(UiKit.L("提示：体力已满的球员不可使用，避免浪费", 11, Palette.Gray));
    }

    private void ShowFoodUse(FoodDefDef fd)
    {
        if (GameState.GetFood(fd.Key) <= 0)
        {
            UiKit.Toast("数量不足");
            return;
        }
        if (fd.Target == "team")
        {
            UiKit.Confirm("使用 " + fd.Name, $"全队场上 5 人各恢复 {fd.Effect} 体力（不超过各自上限）。", "使用", () =>
            {
                if (!GameState.UseFood(fd.Key, 1)) return;
                foreach (var p in LineupUtil.FieldPlayers())
                {
                    if (p == null) continue;
                    p.Sta = Math.Min(HeroCalc.StaCapTotal(p), p.Sta + fd.Effect);
                }
                SaveSystem.Save();
                Refresh();
                UiKit.Toast($"全队恢复 {fd.Effect} 体力");
            }, "取消", false);
            return;
        }

        var v = UiKit.VBox(6);
        v.AddChild(UiKit.L($"选择一名球员（体力 +{fd.Effect}）", 12, Palette.Gray));
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 320), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var list = UiKit.VBox(6);
        list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(list);
        v.AddChild(scroll);

        var players = GameState.D.Heroes.OrderBy(p => (float)p.Sta / HeroCalc.StaCapTotal(p)).ToList();
        foreach (var h in players.Take(40))
        {
            int cap = HeroCalc.StaCapTotal(h);
            bool full = h.Sta >= cap;
            var row = ItemUi.HeroRow(h, $"{h.Sta}/{cap}", () =>
            {
                if (!GameState.UseFood(fd.Key, 1)) return;
                h.Sta = Math.Min(cap, h.Sta + fd.Effect);
                SaveSystem.Save();
                Popup.Current?.Close();
                Refresh();
                UiKit.Toast($"{h.Def.Name} 体力 +{fd.Effect}");
            }, !full, full ? "体力已满" : null);
            list.AddChild(row);
        }
        UiKit.ShowPopup("使用 " + fd.Name, v, false, 340);
    }

    private void BuildTalis()
    {
        _listBox.AddChild(ItemUi.TaliRow("copper", GameState.D.Copper));
        _listBox.AddChild(ItemUi.TaliRow("jade", GameState.D.Jade));
        _listBox.AddChild(UiKit.L("虎符用于兵营抽取武将，暂不支持回收。", 11, Palette.Gray));
    }

    // ---------------- 武将（v2：双列、出战标记、分类筛选） ----------------
    private void RebuildHeroFilters()
    {
        _filtersBox.ClearChildren();
        _filtersBox.AddChild(UiKit.FilterRow("派系", new[] { "全部", "蜀", "魏", "吴", "群" },
            () => _faction, v => { _faction = v; Refresh(); }));
        _filtersBox.AddChild(UiKit.FilterRow("品质", new[] { "全部", "绿", "蓝", "紫", "金" },
            () => _quality, v => { _quality = v; Refresh(); }));
        _filtersBox.AddChild(UiKit.FilterRow("星级", new[] { "全部", "1★", "2★", "3★", "4★", "5★", "6★" },
            () => _star == 0 ? "全部" : _star + "★",
            v => { _star = v == "全部" ? 0 : int.Parse(v.Substring(0, 1)); Refresh(); }));
    }

    /// <summary>出战状态：首（首发）/ 教（教练）/ 替（替补）/ null。</summary>
    private static string SlotMark(HeroInst inst)
    {
        foreach (var slot in Slots.Field)
            if (GameState.D.Lineup.TryGetValue(slot, out var uid) && uid == inst.Uid) return "首";
        foreach (var slot in Slots.Coach)
            if (GameState.D.Lineup.TryGetValue(slot, out var uid) && uid == inst.Uid) return "教";
        foreach (var slot in Slots.Bench)
            if (GameState.D.Lineup.TryGetValue(slot, out var uid) && uid == inst.Uid) return "替";
        return null;
    }

    private void BuildHeroes()
    {
        RebuildHeroFilters();
        var list = GameState.D.Heroes.Where(h =>
        {
            if (_faction != "全部" && h.Def.Faction != _faction) return false;
            if (_quality != "全部" && Quality.Cn(h.Def.QualityKey) != _quality) return false;
            if (_star > 0 && h.Star != _star) return false;
            return true;
        }).ToList();

        // 出战武将置顶
        list.Sort((a, b) =>
        {
            string ma = SlotMark(a), mb = SlotMark(b);
            int oa = ma == "首" ? 0 : ma == "教" ? 1 : ma == "替" ? 2 : 3;
            int ob = mb == "首" ? 0 : mb == "教" ? 1 : mb == "替" ? 2 : 3;
            if (oa != ob) return oa.CompareTo(ob);
            int q = Quality.Tier(b.Def.QualityKey).CompareTo(Quality.Tier(a.Def.QualityKey));
            if (q != 0) return q;
            return HeroCalc.Ovr(b).CompareTo(HeroCalc.Ovr(a));
        });

        if (list.Count == 0)
        {
            _listBox.AddChild(UiKit.L("没有符合条件的武将", 12, Palette.Gray));
            return;
        }

        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 6);
        foreach (var h in list)
        {
            grid.AddChild(BuildHeroCell(h));
        }
        _listBox.AddChild(grid);
    }

    private Control BuildHeroCell(HeroInst inst)
    {
        string mark = SlotMark(inst);
        bool inLineup = mark != null;
        string key = "hero:" + inst.Uid;
        bool selected = _selected.Contains(key);

        var card = UiKit.Card(12,
            inLineup ? new Color("#E7DFCE") : Palette.CardBg,
            Quality.Color(inst.Def.QualityKey), 2, 6);
        card.MouseFilter = MouseFilterEnum.Stop;
        card.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        var h = UiKit.HBox(7);
        card.AddChild(h);

        var hc = new HeroCard(40);
        hc.Setup(inst);
        hc.MouseFilter = MouseFilterEnum.Ignore;
        h.AddChild(hc);

        var col = UiKit.VBox(1);
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        col.MouseFilter = MouseFilterEnum.Ignore;
        var nameRow = UiKit.HBox(4);
        nameRow.MouseFilter = MouseFilterEnum.Ignore;
        nameRow.AddChild(UiKit.L(inst.Def.Name, 12, Palette.Ink, true));
        if (inLineup)
        {
            var chip = UiKit.Card(4, mark == "首" ? Palette.Cinnabar : mark == "教" ? Palette.StoneBlue : Palette.Bamboo, null, 0, 1);
            var chipLabel = UiKit.L(mark, 10, Colors.White, true);
            chipLabel.MouseFilter = MouseFilterEnum.Ignore;
            chip.AddChild(chipLabel);
            chip.MouseFilter = MouseFilterEnum.Ignore;
            nameRow.AddChild(chip);
        }
        col.AddChild(nameRow);
        col.AddChild(UiKit.L($"OVR {HeroCalc.Ovr(inst)} · {inst.Star}★", 10, Palette.Gray));
        col.AddChild(UiKit.L($"偏好 {inst.Def.PrefText}", 10, Palette.StoneBlue));
        h.AddChild(col);

        if (_batch)
        {
            var sel = UiKit.L(selected ? "✓" : "○", 14, selected ? Palette.Bamboo : Palette.Gray, true);
            sel.MouseFilter = MouseFilterEnum.Ignore;
            h.AddChild(sel);
        }

        card.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                if (_batch) ToggleSel(key);
                else UiKit.PushScreen(new HeroDetailScreen(inst));
            }
        };
        return card;
    }

    private static void Highlight(Control row)
    {
        var sp = row.GetChildOrNull<HBoxContainer>(0);
        if (sp != null)
        {
            var dot = UiKit.L("✓", 14, Palette.Bamboo, true);
            sp.AddChild(dot);
        }
    }

    private void ToggleSel(string key)
    {
        if (!_selected.Remove(key)) _selected.Add(key);
        Refresh();
    }

    // ---------------- 批量回收 ----------------
    private long BatchValue()
    {
        long total = 0;
        foreach (var key in _selected)
        {
            if (key.StartsWith("mat:"))
            {
                var parts = key.Substring(4).Split(':');
                if (parts.Length == 2)
                {
                    int count = GameState.GetMat(parts[0], parts[1]);
                    int price = ConfigDb.I.C.RecycleMat.TryGetValue(parts[1], out int p) ? p : 0;
                    total += (long)count * price;
                }
            }
            else if (key.StartsWith("hero:"))
            {
                var h = GameState.HeroByUid(key.Substring(5));
                if (h != null)
                {
                    long price = ConfigDb.I.C.RecycleHero.TryGetValue(h.Def.QualityKey, out int p) ? p : 0;
                    total += price * h.Star;
                }
            }
        }
        return total;
    }

    private void DoBatchRecycle()
    {
        if (_selected.Count == 0)
        {
            UiKit.Toast("先勾选品质或点选条目");
            return;
        }
        long total = BatchValue();
        int matCount = _selected.Count(k => k.StartsWith("mat:"));
        int heroCount = _selected.Count(k => k.StartsWith("hero:"));

        UiKit.Confirm("批量回收确认",
            $"将永久销毁 材料 ×{matCount} 类 / 武将 ×{heroCount} 名，获得合计 {total:N0} 金币。\n回收后图鉴仍保留，此操作不可撤销。",
            $"确认回收 {total:N0} 金币", () =>
            {
                foreach (var key in _selected.ToList())
                {
                    if (key.StartsWith("mat:"))
                    {
                        var parts = key.Substring(4).Split(':');
                        if (parts.Length == 2)
                        {
                            int count = GameState.GetMat(parts[0], parts[1]);
                            GameState.UseMat(parts[0], parts[1], count);
                        }
                    }
                    else if (key.StartsWith("hero:"))
                    {
                        var h = GameState.HeroByUid(key.Substring(5));
                        // v2：取消“最后一张”保护，仅保护已装备
                        if (h != null && !GameState.IsEquipped(h)) GameState.RemoveHero(h);
                    }
                }
                GameState.AddCoins(total);
                SaveSystem.Save();
                _selected.Clear();
                ResetQualityChecks();
                Refresh();
                UiKit.Toast($"批量回收完成 +{total:N0} 金币");
            }, "取消", true);
    }

    // ---------------- 调试入口（截图/自测） ----------------
    public void DebugBatch()
    {
        if (!_batch) ToggleBatch();
        ToggleQuality("green");
    }

    public void DebugHeroTab()
    {
        _cat = "hero";
        _batch = false;
        Refresh();
    }
}
