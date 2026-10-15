using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using SanguoBasketball.Core;
using SanguoBasketball.Systems;

namespace SanguoBasketball.UI;

/// <summary>SCR_HERO_DETAIL · 武将详情（军库版：使用材料 / 进化升星 / 回收）。</summary>
public partial class HeroDetailScreen : Control
{
    private readonly HeroInst _inst;
    private VBoxContainer _root;

    public HeroDetailScreen(HeroInst inst)
    {
        _inst = inst;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Rebuild();
    }

    private void Rebuild()
    {
        this.ClearChildren();
        var def = _inst.Def;

        // 势力 + 品质 背景
        var backdrop = UIArt.FactionBackdrop(def.Faction, def.QualityKey);
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);

        var margin = UiKit.Pad(16, 46, 16, 12);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(margin);
        _root = UiKit.VBox(8);
        margin.AddChild(_root);

        // 头部
        var head = UiKit.HBox(8);
        head.AddChild(UiKit.BtnSecondary("← 返回", () => UiKit.PopScreen(), 36, 13));
        var sp = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        head.AddChild(sp);
        head.AddChild(UiKit.L("武将详情", 15, Palette.InkSoft, true));
        _root.AddChild(head);

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _root.AddChild(scroll);
        var col = UiKit.VBox(10);
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(col);

        // 大卡面（v2：高度按卡片实际尺寸，杜绝与名称行重叠）
        var big = new HeroCard(150);
        big.Setup(_inst);
        big.MouseFilter = MouseFilterEnum.Ignore;
        var cardHolder = new Control { CustomMinimumSize = new Vector2(0, big.CardH + 14), MouseFilter = MouseFilterEnum.Ignore };
        big.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
        big.OffsetLeft = -75; big.OffsetRight = 75;
        cardHolder.AddChild(big);
        col.AddChild(cardHolder);

        // 名称 + OVR
        var nameRow = UiKit.HBox(8);
        nameRow.Alignment = BoxContainer.AlignmentMode.Center;
        nameRow.AddChild(UiKit.L(def.FullName, 20, Palette.Ink, true));
        var ovrBtn = UiKit.BtnSecondary($"OVR {HeroCalc.Ovr(_inst)}", () => UiKit.Confirm("OVR 公式",
            "OVR = 九项属性平均值（四舍五入）。\n属性 = 基础 × 星级系数 × 得意系数 + 材料加成。", "知道了", null, "关闭", false), 34, 13);
        nameRow.AddChild(ovrBtn);
        col.AddChild(nameRow);

        // 星级 + 下一星预览
        var starRow = UiKit.HBox(8);
        starRow.Alignment = BoxContainer.AlignmentMode.Center;
        starRow.AddChild(UiKit.L(UiKit.Stars(_inst.Star), 16, Palette.Gamboge, true));
        if (_inst.Star < ConfigDb.I.C.EvolveMaxStar)
        {
            int ns = _inst.Star + 1;
            starRow.AddChild(UiKit.L($"→ 全部×{ConfigDb.I.StarS(ns):0.00}，得意×{ConfigDb.I.StarD(ns):0.00}", 11, Palette.StoneBlue));
        }
        col.AddChild(starRow);

        // 偏好位置 + 得意属性
        var infoRow = UiKit.HBox(6);
        infoRow.Alignment = BoxContainer.AlignmentMode.Center;
        var prefChip = UiKit.Card(8, new Color(Palette.StoneBlue, 0.12f), null, 0, 5);
        prefChip.AddChild(UiKit.L("偏好位置 " + def.PrefText, 12, Palette.StoneBlue, true));
        infoRow.AddChild(prefChip);
        string sig = string.Join(" ", def.Signature.Select(k =>
        {
            int idx = Attr.KeyToIndex(k);
            return $"{Attr.Cns[idx]}×{ConfigDb.I.StarD(_inst.Star):0.00}";
        }));
        infoRow.AddChild(UiKit.L("得意: " + sig, 12, Palette.Cinnabar, true));
        col.AddChild(infoRow);

        // 独立资源（v2：智商 / 体力上限）
        int iq = HeroCalc.IqTotal(_inst);
        int cap = HeroCalc.StaCapTotal(_inst);
        var resRow = UiKit.HBox(8);
        resRow.Alignment = BoxContainer.AlignmentMode.Center;
        resRow.AddChild(UiKit.L($"智商 {iq}", 13, Palette.Ink, true));
        resRow.AddChild(UiKit.L($"体力上限 {cap}", 13, Palette.Ink, true));
        resRow.AddChild(UiKit.L($"当前体力 {_inst.Sta}/{cap}", 12, Palette.Gray));
        col.AddChild(resRow);

        // 属性面板
        var panel = UiKit.Card(14, Palette.CardBg, new Color(0, 0, 0, 0.12f), 1);
        var pv = UiKit.VBox(4);
        panel.AddChild(pv);
        var core = HeroCalc.CoreAttrs(_inst);
        var grid = UiKit.HBox(12);
        var left = UiKit.VBox(4);
        var right = UiKit.VBox(4);
        left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        for (int i = 0; i < Attr.COUNT; i++)
        {
            var row = AttrRow(i, core[i]);
            if (i % 2 == 0) left.AddChild(row); else right.AddChild(row);
        }
        grid.AddChild(left);
        grid.AddChild(right);
        pv.AddChild(grid);
        col.AddChild(panel);

        // 操作入口
        var ops = UiKit.HBox(8);
        var useBtn = UiKit.BtnSecondary("使用材料", ShowMaterialPopup, 46, 14);
        useBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        ops.AddChild(useBtn);
        var evoBtn = UiKit.BtnPrimary("进化升星", ShowEvolvePopup, 46, 14);
        evoBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        evoBtn.Disabled = _inst.Star >= ConfigDb.I.C.EvolveMaxStar;
        ops.AddChild(evoBtn);
        var recBtn = UiKit.BtnDanger("回收", ShowRecyclePopup, 46, 14);
        recBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        ops.AddChild(recBtn);
        col.AddChild(ops);

        // 装备状态
        int slotIdx = GameState.EquippedSlot(_inst);
        string equipText = slotIdx >= 0 ? $"出战中：{Slots.Cn(Slots.All[slotIdx])}" : "未出战 · 可在阵容页指派";
        var capsule = UiKit.Card(12, new Color(Palette.StoneBlue, 0.10f), null, 0, 6);
        var cl = UiKit.L(equipText, 12, Palette.StoneBlue);
        cl.HorizontalAlignment = HorizontalAlignment.Center;
        capsule.AddChild(cl);
        col.AddChild(capsule);
    }

    private static Control AttrRow(int idx, float value)
    {
        var v = UiKit.VBox(1);
        var head = UiKit.HBox(4);
        head.AddChild(UiKit.L(Attr.Cns[idx], 11, Palette.Gray));
        head.AddChild(UiKit.L(((int)MathF.Round(value)).ToString(), 13, Palette.Ink, true));
        v.AddChild(head);
        var bar = new AttrBar();
        bar.SetValue(value);
        v.AddChild(bar);
        return v;
    }

    // ---------------- 调试入口（截图/自测） ----------------
    public void DebugShowMaterials() => ShowMaterialPopup();

    // ---------------- 使用材料（v2：仅显示已拥有；含独立资源材料；点空白关闭） ----------------
    private void ShowMaterialPopup()
    {
        var v = UiKit.VBox(6);
        var status = UiKit.L("点击材料即喂给该武将（可连点批量）· 点空白处关闭", 11, Palette.Gray);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 360), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var list = UiKit.VBox(6);
        list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(list);
        v.AddChild(status);
        v.AddChild(scroll);

        void BuildList()
        {
            list.ClearChildren();
            bool any = false;

            // 独立资源材料优先
            foreach (var sm in ConfigDb.I.C.Ext.SpecialMats)
            {
                foreach (string q in new[] { "gold", "purple", "blue", "green" })
                {
                    int count = GameState.GetMat(sm.Id, q);
                    if (count <= 0) continue;
                    any = true;
                    string typeId = sm.Id;
                    string quality = q;
                    var row = ItemUi.Row(Art.MatIcon(typeId),
                        $"{sm.Name}·{Quality.Cn(quality)}",
                        $"{sm.StatCn} +{sm.Q[q].Main} / {Attr.Cns[sm.SubIdx]} {sm.Q[q].Sub}",
                        "×" + count, () =>
                        {
                            if (GrowthSystem.UseMaterial(_inst, typeId, quality, 1) > 0)
                            {
                                status.Text = $"{sm.Name}·{Quality.Cn(quality)} 已使用：{sm.StatCn} {sm.Q[q].Main:+0;-0}，{Attr.Cns[sm.SubIdx]} {sm.Q[q].Sub:+0;-0}";
                                SaveSystem.Save();
                                Rebuild();
                            }
                            BuildList();
                        }, true, Quality.Color(quality));
                    list.AddChild(row);
                }
            }

            // 普通属性材料（仅已拥有）
            foreach (string q in new[] { "gold", "purple", "blue", "green" })
            {
                foreach (var mt in ConfigDb.I.C.Ext.MatTypes)
                {
                    int count = GameState.GetMat(mt.Id, q);
                    if (count <= 0) continue;
                    any = true;
                    string typeId = mt.Id;
                    string quality = q;
                    var row = ItemUi.Row(Art.MatIcon(typeId), mt.Name + "·" + Quality.Cn(quality),
                        $"{Attr.Cns[mt.MainIdx]} +{ConfigDb.I.C.MatQuality[q].Main} / {Attr.Cns[mt.SubIdx]} {ConfigDb.I.C.MatQuality[q].Sub}",
                        "×" + count, () =>
                        {
                            if (GrowthSystem.UseMaterial(_inst, typeId, quality, 1) > 0)
                            {
                                var (main, sub) = GrowthSystem.MaterialPreview(typeId, quality);
                                var mt2 = ConfigDb.I.MatType(typeId);
                                status.Text = $"{mt2.Name}·{Quality.Cn(quality)} 已使用：{Attr.Cns[mt2.MainIdx]} {main:+0;-0}，{Attr.Cns[mt2.SubIdx]} {sub:+0;-0}";
                                SaveSystem.Save();
                                Rebuild();
                            }
                            BuildList();
                        }, true, Quality.Color(quality));
                    list.AddChild(row);
                }
            }

            if (!any)
                list.AddChild(UiKit.L("军库中还没有材料，去城池·市场或挑战副本获取", 12, Palette.Gray));
        }
        BuildList();
        UiKit.ShowPopup("使用材料", v, true, 344);
    }

    // ---------------- 进化升星（v2：仅显示同一武将的祭品） ----------------
    private void ShowEvolvePopup()
    {
        var c = ConfigDb.I.C;
        if (_inst.Star >= c.EvolveMaxStar)
        {
            UiKit.Toast("已达最高星级");
            return;
        }

        var clone = _inst.Clone();
        clone.Star = _inst.Star + 1;
        int curOvr = HeroCalc.Ovr(_inst);
        int newOvr = HeroCalc.Ovr(clone);

        var v = UiKit.VBox(6);
        v.AddChild(UiKit.LWrap(
            $"进化后：{_inst.Star}★ → {clone.Star}★，OVR {curOvr} → {newOvr}（全部×{ConfigDb.I.StarS(clone.Star):0.00}，得意×{ConfigDb.I.StarD(clone.Star):0.00}）",
            12, Palette.StoneBlue));
        v.AddChild(UiKit.L($"祭品必须为相同的武将卡（{_inst.Def.FullName}）· 点空白处关闭", 11, Palette.Gray));

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 320), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var list = UiKit.VBox(6);
        list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(list);
        v.AddChild(scroll);

        var candidates = GameState.HeroesOfDef(_inst.DefId)
            .Where(h => h.Uid != _inst.Uid)
            .OrderByDescending(h => h.Star)
            .ToList();

        if (candidates.Count == 0)
        {
            list.AddChild(UiKit.LWrap($"还没有另一张「{_inst.Def.Name}」。\n升星需要相同的卡牌，去兵营抽卡碰碰运气吧。", 12, Palette.Gray));
        }
        foreach (var h in candidates.Take(50))
        {
            bool ok = GrowthSystem.CanEvolve(_inst, h, out string err);
            var row = ItemUi.HeroRow(h, ok ? "献祭" : "", () =>
            {
                UiKit.Confirm("确认进化", $"将消耗祭品「{h.Def.FullName} {h.Star}★」，{_inst.Def.FullName} 升至 {clone.Star}★。此操作不可撤销。",
                    "确认进化", () =>
                    {
                        if (GrowthSystem.Evolve(_inst, h))
                        {
                            Popup.Current?.Close();
                            var flash = new ColorRect { Color = new Color(1, 1, 1, 0) };
                            flash.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                            flash.MouseFilter = MouseFilterEnum.Ignore;
                            AddChild(flash);
                            var t = flash.CreateTween();
                            t.TweenProperty(flash, "color:a", 0.85f, 0.18f);
                            t.TweenProperty(flash, "color:a", 0.0f, 0.42f);
                            t.TweenCallback(Callable.From(() => flash.QueueFree()));
                            UiKit.Toast($"{_inst.Def.Name} 进化成功！{_inst.Star}★");
                            Rebuild();
                        }
                        else
                        {
                            UiKit.Toast(err);
                        }
                    }, "取消", false);
            }, ok, err);
            list.AddChild(row);
        }
        UiKit.ShowPopup("进化升星", v, true, 348);
    }

    // ---------------- 回收（v2：取消最后一张保护，仅保护出战中的卡） ----------------
    private void ShowRecyclePopup()
    {
        long price = ConfigDb.I.C.RecycleHero.TryGetValue(_inst.Def.QualityKey, out int basePrice) ? basePrice : 0;
        price *= _inst.Star;

        if (GameState.IsEquipped(_inst))
        {
            UiKit.Confirm("无法回收", "该卡正在出战（首发/教练/替补），请先从阵容中卸下", "知道了", null, "关闭", false);
            return;
        }

        UiKit.Confirm("回收确认",
            $"{_inst.Def.FullName}（{_inst.Star}★）将被永久销毁，获得 {price} 金币。\n回收后图鉴仍会保留。",
            $"确认回收 {price} 金币", () =>
            {
                GameState.RemoveHero(_inst);
                GameState.AddCoins(price);
                SaveSystem.Save();
                UiKit.Toast($"回收成功，+{price} 金币");
                UiKit.PopScreen();
            }, "取消", true);
    }
}

/// <summary>属性条（对数归一，长条视觉）。</summary>
public partial class AttrBar : Control
{
    private float _v;

    public AttrBar()
    {
        CustomMinimumSize = new Vector2(0, 6);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void SetValue(float v)
    {
        _v = v;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float rel = Mathf.Clamp(400f / (400f + Math.Max(0, _v)), 0.06f, 1f);
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0, 0, 0, 0.08f));
        DrawRect(new Rect2(Vector2.Zero, new Vector2(Size.X * rel, Size.Y)), Palette.StoneBlue);
    }
}
