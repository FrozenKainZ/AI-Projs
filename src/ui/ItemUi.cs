using System;
using Godot;
using SanguoBasketball.Core;
using SanguoBasketball.Systems;

namespace SanguoBasketball.UI;

/// <summary>道具/材料/武将行组件（背包、抽卡、详情、七星坛共用）。</summary>
public static class ItemUi
{
    public static Control HRule(float alpha = 0.08f)
    {
        var r = new ColorRect { Color = new Color(0, 0, 0, alpha), CustomMinimumSize = new Vector2(0, 1), MouseFilter = Control.MouseFilterEnum.Ignore };
        return r;
    }

    /// <summary>通用可点行：左图标 + 标题/副标题 + 右值。</summary>
    public static Control Row(Texture2D icon, string title, string subtitle, string right,
        Action onTap, bool enabled = true, Color? borderColor = null, string disabledReason = null)
    {
        var card = UiKit.Card(12, enabled ? Palette.CardBg : new Color(Palette.Paper), borderColor ?? new Color(0, 0, 0, 0.10f), 1, 8);
        var h = UiKit.HBox(10);
        card.AddChild(h);

        var iconHolder = new Control { CustomMinimumSize = new Vector2(40, 40), MouseFilter = Control.MouseFilterEnum.Ignore };
        if (icon != null)
        {
            var tr = new TextureRect { Texture = icon, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = Control.MouseFilterEnum.Ignore };
            tr.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            iconHolder.AddChild(tr);
        }
        else
        {
            var ph = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
            ph.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            ph.AddThemeStyleboxOverride("panel", UiKit.SB(new Color("#E2E6EE"), 8, borderColor ?? new Color(0, 0, 0, 0.1f), 1));
            iconHolder.AddChild(ph);
        }
        h.AddChild(iconHolder);

        var col = UiKit.VBox(2);
        col.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        col.AddChild(UiKit.L(title, 14, enabled ? Palette.Ink : Palette.Gray, true));
        if (!string.IsNullOrEmpty(subtitle))
            col.AddChild(UiKit.L(subtitle, 11, Palette.Gray));
        if (!enabled && !string.IsNullOrEmpty(disabledReason))
            col.AddChild(UiKit.L(disabledReason, 11, Palette.Cinnabar));
        h.AddChild(col);

        if (!string.IsNullOrEmpty(right))
        {
            var rl = UiKit.L(right, 14, enabled ? Palette.Gamboge : Palette.Gray, true);
            rl.VerticalAlignment = VerticalAlignment.Center;
            h.AddChild(rl);
        }

        if (enabled && onTap != null)
        {
            card.MouseFilter = Control.MouseFilterEnum.Stop;
            card.GuiInput += ev =>
            {
                if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) onTap();
            };
        }
        return card;
    }

    /// <summary>材料行（篮球道具 / 独立资源材料 + 品质 + 主/副属性说明）。</summary>
    public static Control MatRow(string typeId, string quality, int count, Action onTap, string rightOverride = null, bool forceEnabled = false)
    {
        string name;
        string sub;
        var special = ConfigDb.I.SpecialMat(typeId);
        if (special != null)
        {
            var sq = special.Q.TryGetValue(quality, out var sv) ? sv : new MatQ();
            name = special.Name;
            sub = $"{Quality.Cn(quality)}·{special.StatCn} +{sq.Main} / {Attr.Cns[special.SubIdx]} {sq.Sub}";
        }
        else
        {
            var mt = ConfigDb.I.MatType(typeId);
            var mq = ConfigDb.I.C.MatQuality[quality];
            name = mt.Name;
            sub = $"{Quality.Cn(quality)}·{Attr.Cns[mt.MainIdx]} +{mq.Main} / {Attr.Cns[mt.SubIdx]} {mq.Sub}";
        }
        bool enabled = forceEnabled || count > 0;
        return Row(Art.MatIcon(typeId), name, sub, rightOverride ?? ("×" + count),
            onTap, enabled, Quality.Color(quality), enabled ? null : "数量不足");
    }

    /// <summary>兵粮行。</summary>
    public static Control FoodRow(string key, int count, Action onTap, string rightOverride = null)
    {
        var fd = ConfigDb.I.FoodDef(key);
        string sub = fd.Target == "team" ? $"全队上场 5 人 体力 +{fd.Effect}" : $"单体体力 +{fd.Effect}";
        bool enabled = count > 0;
        return Row(Art.FoodIcon(key), fd.Name, sub, rightOverride ?? ("×" + count), onTap, enabled,
            Palette.Bamboo, enabled ? null : "数量不足");
    }

    /// <summary>虎符行。</summary>
    public static Control TaliRow(string key, int count)
    {
        string name = key == "copper" ? "铜虎符" : "玉虎符";
        string sub = key == "copper" ? "兵营池 1 抽" : "兵营池 10 连";
        return Row(Art.TaliIcon(key), name, sub, "×" + count, null, false, Palette.StoneBlue,
            null);
    }

    /// <summary>武将行（进化祭品 / 回收候选）：卡 + 名 + 说明 + 右侧状态。</summary>
    public static Control HeroRow(HeroInst inst, string right, Action onTap, bool enabled, string disabledReason = null,
        Control extra = null)
    {
        var card = UiKit.Card(12, enabled ? Palette.CardBg : new Color(Palette.Paper), Quality.Color(inst.Def.QualityKey), 2, 6);
        var h = UiKit.HBox(10);
        card.AddChild(h);
        var hc = new HeroCard(40);
        hc.Setup(inst);
        hc.MouseFilter = Control.MouseFilterEnum.Ignore;
        h.AddChild(hc);
        var col = UiKit.VBox(2);
        col.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        col.AddChild(UiKit.L(inst.Def.FullName, 14, enabled ? Palette.Ink : Palette.Gray, true));
        col.AddChild(UiKit.L($"{(inst.Star)}★ · OVR {HeroCalc.Ovr(inst)}" + (extra == null ? "" : ""), 11, Palette.Gray));
        if (!enabled && !string.IsNullOrEmpty(disabledReason))
            col.AddChild(UiKit.L(disabledReason, 11, Palette.Cinnabar));
        h.AddChild(col);
        if (extra != null) h.AddChild(extra);
        if (!string.IsNullOrEmpty(right))
        {
            var rl = UiKit.L(right, 13, enabled ? Palette.Gamboge : Palette.Gray, true);
            rl.VerticalAlignment = VerticalAlignment.Center;
            h.AddChild(rl);
        }
        if (enabled && onTap != null)
        {
            card.MouseFilter = Control.MouseFilterEnum.Stop;
            card.GuiInput += ev =>
            {
                if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) onTap();
            };
        }
        return card;
    }

    /// <summary>抽卡产出格（结果网格/背包网格通用）。</summary>
    public static Control DrawCell(DrawItem item, float size = 76)
    {
        var col = UiKit.VBox(2);
        var holder = new Control { CustomMinimumSize = new Vector2(size, size), MouseFilter = Control.MouseFilterEnum.Ignore };
        var panel = UiKit.Card(10, Palette.CardBg, Quality.Color(item.Type == "hero" ? ConfigDb.I.Hero(item.HeroDefId).QualityKey : item.Quality ?? "green"), 2, 4);
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        panel.MouseFilter = Control.MouseFilterEnum.Ignore;
        holder.AddChild(panel);
        var tex = Art.DrawIcon(item);
        if (tex != null)
        {
            var tr = new TextureRect { Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = Control.MouseFilterEnum.Ignore };
            tr.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            tr.OffsetLeft = 6; tr.OffsetRight = -6; tr.OffsetTop = 6; tr.OffsetBottom = -6;
            holder.AddChild(tr);
        }
        col.AddChild(holder);
        var name = UiKit.L(item.DisplayName, 10, Palette.InkSoft, true);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(name);
        return col;
    }

    /// <summary>品质字块（角标）。</summary>
    public static Control QualityTag(string quality)
    {
        var p = UiKit.Card(5, Quality.Color(quality), null, 0, 2);
        var l = UiKit.L(Quality.Cn(quality), 10, Colors.White, true);
        p.AddChild(l);
        return p;
    }
}
