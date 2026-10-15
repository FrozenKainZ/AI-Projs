using System;
using System.Linq;
using Godot;
using SanguoBasketball.Core;
using SanguoBasketball.Systems;

namespace SanguoBasketball.UI;

/// <summary>
/// 图鉴 · 武将详情（白板数值版）：
/// 星级选择栏 → 查看该星级下的白板基础属性 / 倍率 / 智商 / 体力上限。
/// 不含强化、升星、回收等操作。
/// </summary>
public partial class CodexHeroDetailScreen : Control
{
    private readonly HeroDef _def;
    private int _star = 1;
    private VBoxContainer _root;

    public CodexHeroDetailScreen(HeroDef def)
    {
        _def = def;
        _star = Math.Min(6, Math.Max(1, GameState.HeroesOfDef(def.Id).Count > 0
            ? GameState.HeroesOfDef(def.Id).Max(h => h.Star)
            : 1));
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Rebuild();
    }

    private void Rebuild()
    {
        this.ClearChildren();

        var backdrop = UIArt.FactionBackdrop(_def.Faction, _def.QualityKey);
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);

        var margin = UiKit.Pad(16, 46, 16, 12);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(margin);
        _root = UiKit.VBox(8);
        margin.AddChild(_root);

        var head = UiKit.HBox(8);
        head.AddChild(UiKit.BtnSecondary("← 返回", () => UiKit.PopScreen(), 36, 13));
        var sp = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        head.AddChild(sp);
        head.AddChild(UiKit.L("图鉴 · " + _def.Name, 15, Palette.InkSoft, true));
        _root.AddChild(head);

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _root.AddChild(scroll);
        var col = UiKit.VBox(10);
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(col);

        // 卡面（图鉴不显示星级）
        var card = new HeroCard(140);
        card.SetupRaw(_def.Id, _def.Name, _def.QualityKey, 0, null);
        card.MouseFilter = MouseFilterEnum.Ignore;
        var cardHolder = new Control { CustomMinimumSize = new Vector2(0, card.CardH + 14), MouseFilter = MouseFilterEnum.Ignore };
        card.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
        card.OffsetLeft = -70; card.OffsetRight = 70;
        cardHolder.AddChild(card);
        col.AddChild(cardHolder);

        // 名称 + 偏好位置
        var nameRow = UiKit.HBox(8);
        nameRow.Alignment = BoxContainer.AlignmentMode.Center;
        nameRow.AddChild(UiKit.L(_def.FullName, 20, Palette.Ink, true));
        var prefChip = UiKit.Card(8, new Color(Palette.StoneBlue, 0.12f), null, 0, 5);
        prefChip.AddChild(UiKit.L("偏好位置 " + _def.PrefText, 12, Palette.StoneBlue, true));
        nameRow.AddChild(prefChip);
        col.AddChild(nameRow);

        // 星级选择栏（勾选后查看该星级白板）
        var starLabel = UiKit.L("选择星级查看白板数值：", 11, Palette.Gray);
        col.AddChild(starLabel);
        var starRow = UiKit.HBox(5);
        for (int s = 1; s <= 6; s++)
        {
            int star = s;
            var b = UiKit.BtnSecondary($"{s}★", () =>
            {
                _star = star;
                Rebuild();
            }, 30, 12);
            b.CustomMinimumSize = new Vector2(46, 30);
            if (s == _star)
            {
                b.AddThemeColorOverride("font_color", Colors.White);
                b.AddThemeColorOverride("font_hover_color", Colors.White);
                b.AddThemeColorOverride("font_pressed_color", Colors.White);
                b.AddThemeStyleboxOverride("normal", UiKit.SB(Palette.Gamboge, 12));
                b.AddThemeStyleboxOverride("hover", UiKit.SB(Palette.Gamboge, 12));
                b.AddThemeStyleboxOverride("pressed", UiKit.SB(Palette.Gamboge.Darkened(0.1f), 12));
            }
            starRow.AddChild(b);
        }
        col.AddChild(starRow);

        // 倍率说明
        double sMul = ConfigDb.I.StarS(_star);
        double dMul = ConfigDb.I.StarD(_star);
        var mulLabel = UiKit.L($"全部属性 ×{sMul:0.00} · 得意属性 ×{dMul:0.00}", 12, Palette.Cinnabar, true);
        mulLabel.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(mulLabel);

        // 得意属性
        string sig = string.Join(" ", _def.Signature.Select(k => Attr.Cns[Attr.KeyToIndex(k)]));
        var sigLabel = UiKit.L("得意: " + sig, 12, Palette.InkSoft);
        sigLabel.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(sigLabel);

        // 白板基础属性（该星级，无材料加成）
        var panel = UiKit.Card(14, Palette.CardBg, new Color(0, 0, 0, 0.12f), 1);
        var pv = UiKit.VBox(4);
        panel.AddChild(pv);
        var grid = UiKit.HBox(12);
        var left = UiKit.VBox(4);
        var right = UiKit.VBox(4);
        left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        for (int i = 0; i < Attr.COUNT; i++)
        {
            double v = _def.Base[i] * sMul * (_def.IsSignature(i) ? dMul : 1.0);
            var row = AttrRow(i, (float)v);
            if (i % 2 == 0) left.AddChild(row); else right.AddChild(row);
        }
        grid.AddChild(left);
        grid.AddChild(right);
        pv.AddChild(grid);
        col.AddChild(panel);

        // 独立资源白板（智商 / 体力上限，随星级加值，不含材料）
        int iq = _def.Iq + (_star - 1) * ConfigDb.I.C.Ext.IqStarStep;
        int cap = ConfigDb.I.C.Ext.StaCapBase + (_star - 1) * ConfigDb.I.C.Ext.StaCapStarStep;
        var resRow = UiKit.HBox(8);
        resRow.Alignment = BoxContainer.AlignmentMode.Center;
        resRow.AddChild(UiKit.L($"智商 {iq}", 13, Palette.Ink, true));
        resRow.AddChild(UiKit.L($"体力上限 {cap}", 13, Palette.Ink, true));
        resRow.AddChild(UiKit.L($"（{_star}★ 白板 · 未含材料加成）", 11, Palette.Gray));
        col.AddChild(resRow);

        var tip = UiKit.L("图鉴数值为该星级白板属性，仅供对照；实际战斗属性还受材料、位置偏好与教练光环影响。", 10, Palette.Gray);
        tip.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        col.AddChild(tip);
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
}
