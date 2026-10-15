using System;
using System.Collections.Generic;
using Godot;
using SanguoBasketball.Core;

namespace SanguoBasketball.UI;

/// <summary>
/// UI 工具箱：主题/字体、控件工厂、弹窗、Toast、屏幕栈、动效助手。
/// </summary>
public static class UiKit
{
    public static SystemFont FontRegular { get; private set; }
    public static SystemFont FontBold { get; private set; }
    public static Theme Theme { get; private set; }

    public static void Init()
    {
        string[] cjk = { "Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans CJK SC", "PingFang SC", "sans-serif" };
        FontRegular = new SystemFont { FontNames = cjk, FontWeight = 500 };
        FontBold = new SystemFont { FontNames = cjk, FontWeight = 800 };
        Theme = new Theme { DefaultFont = FontRegular, DefaultFontSize = 14 };
    }

    // ---------------- 基础控件 ----------------
    public static Label L(string text, int size = 14, Color? color = null, bool bold = false)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color ?? Palette.Ink);
        if (bold) l.AddThemeFontOverride("font", FontBold);
        return l;
    }

    public static Label LWrap(string text, int size = 14, Color? color = null)
    {
        var l = L(text, size, color);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return l;
    }

    public static void SetFont(Control c, bool bold)
    {
        c.AddThemeFontOverride("font", bold ? FontBold : FontRegular);
    }

    public static StyleBoxFlat SB(Color bg, int radius = 0, Color? border = null, int borderW = 0)
    {
        var sb = new StyleBoxFlat { BgColor = bg };
        if (radius > 0) sb.SetCornerRadiusAll(radius);
        if (border.HasValue && borderW > 0)
        {
            sb.BorderColor = border.Value;
            sb.SetBorderWidthAll(borderW);
        }
        return sb;
    }

    public static PanelContainer Card(int radius = 14, Color? bg = null, Color? border = null, int borderW = 2, int pad = 0)
    {
        var p = new PanelContainer();
        var sb = SB(bg ?? Palette.CardBg, radius, border, borderW);
        sb.ContentMarginLeft = sb.ContentMarginRight = sb.ContentMarginTop = sb.ContentMarginBottom = pad;
        p.AddThemeStyleboxOverride("panel", sb);
        return p;
    }

    public static MarginContainer Pad(int left, int top, int right, int bottom)
    {
        var m = new MarginContainer();
        m.AddThemeConstantOverride("margin_left", left);
        m.AddThemeConstantOverride("margin_top", top);
        m.AddThemeConstantOverride("margin_right", right);
        m.AddThemeConstantOverride("margin_bottom", bottom);
        return m;
    }

    public static VBoxContainer VBox(int sep = 8)
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", sep);
        return v;
    }

    public static HBoxContainer HBox(int sep = 8)
    {
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", sep);
        return h;
    }

    public static Control Spacer(float h = 0, float w = 0)
    {
        var s = new Control();
        if (h > 0) s.CustomMinimumSize = new Vector2(s.CustomMinimumSize.X, h);
        if (w > 0) s.CustomMinimumSize = new Vector2(w, s.CustomMinimumSize.Y);
        return s;
    }

    // ---------------- 按钮 ----------------
    /// <summary>主按钮（朱砂 52px 圆角 14 底部厚度阴影，FX-01 按压）。</summary>
    public static Button BtnPrimary(string text, Action onPressed, float height = 52, int fontSize = 20)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(0, height), FocusMode = Control.FocusModeEnum.None };
        var normal = SB(Palette.Cinnabar, 14);
        normal.ShadowColor = new Color(0, 0, 0, 0.25f);
        normal.ShadowSize = 3;
        normal.ShadowOffset = new Vector2(0, 3);
        var hover = (StyleBoxFlat)normal.Duplicate();
        hover.BgColor = Palette.Cinnabar.Lightened(0.06f);
        var pressed = SB(Palette.Cinnabar.Darkened(0.12f), 14);
        var disabled = SB(Palette.Gray, 14);
        b.AddThemeStyleboxOverride("normal", normal);
        b.AddThemeStyleboxOverride("hover", hover);
        b.AddThemeStyleboxOverride("pressed", pressed);
        b.AddThemeStyleboxOverride("disabled", disabled);
        b.AddThemeColorOverride("font_color", Colors.White);
        b.AddThemeColorOverride("font_hover_color", Colors.White);
        b.AddThemeColorOverride("font_pressed_color", Colors.White);
        b.AddThemeColorOverride("font_disabled_color", new Color(1, 1, 1, 0.6f));
        b.AddThemeFontOverride("font", FontBold);
        b.AddThemeFontSizeOverride("font_size", fontSize);
        if (onPressed != null) b.Pressed += onPressed;
        Fx.Press(b);
        return b;
    }

    /// <summary>次按钮（墨线白底 48px）。</summary>
    public static Button BtnSecondary(string text, Action onPressed, float height = 48, int fontSize = 16)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(0, height), FocusMode = Control.FocusModeEnum.None };
        var normal = SB(Palette.CardBg, 12, Palette.Ink, 2);
        var hover = (StyleBoxFlat)normal.Duplicate();
        hover.BgColor = Palette.Paper;
        var pressed = SB(Palette.Paper.Darkened(0.05f), 12, Palette.Ink, 2);
        var disabled = SB(Palette.Paper, 12, Palette.Gray, 2);
        b.AddThemeStyleboxOverride("normal", normal);
        b.AddThemeStyleboxOverride("hover", hover);
        b.AddThemeStyleboxOverride("pressed", pressed);
        b.AddThemeStyleboxOverride("disabled", disabled);
        b.AddThemeColorOverride("font_color", Palette.Ink);
        b.AddThemeColorOverride("font_hover_color", Palette.Ink);
        b.AddThemeColorOverride("font_pressed_color", Palette.Ink);
        b.AddThemeColorOverride("font_disabled_color", Palette.Gray);
        b.AddThemeFontOverride("font", FontBold);
        b.AddThemeFontSizeOverride("font_size", fontSize);
        if (onPressed != null) b.Pressed += onPressed;
        Fx.Press(b);
        return b;
    }

    /// <summary>朱砂描边警示按钮（回收等危险操作）。</summary>
    public static Button BtnDanger(string text, Action onPressed, float height = 44, int fontSize = 15)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(0, height), FocusMode = Control.FocusModeEnum.None };
        var normal = SB(Palette.CardBg, 12, Palette.Cinnabar, 2);
        var pressed = SB(new Color("#FBEAE6"), 12, Palette.Cinnabar, 2);
        b.AddThemeStyleboxOverride("normal", normal);
        b.AddThemeStyleboxOverride("hover", pressed);
        b.AddThemeStyleboxOverride("pressed", pressed);
        b.AddThemeColorOverride("font_color", Palette.Cinnabar);
        b.AddThemeColorOverride("font_hover_color", Palette.Cinnabar);
        b.AddThemeColorOverride("font_pressed_color", Palette.Cinnabar);
        b.AddThemeFontOverride("font", FontBold);
        b.AddThemeFontSizeOverride("font_size", fontSize);
        if (onPressed != null) b.Pressed += onPressed;
        Fx.Press(b);
        return b;
    }

    /// <summary>圆形悬浮钮（加速/跳过/设置）。</summary>
    public static Button BtnRound(string text, Action onPressed, float size = 48, int fontSize = 15, Color? border = null, Color? bg = null)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(size, size), FocusMode = Control.FocusModeEnum.None };
        int r = (int)(size / 2);
        var normal = SB(bg ?? Palette.CardBg, r, border ?? Palette.Ink, 2);
        var pressed = SB(Palette.Paper, r, border ?? Palette.Ink, 2);
        b.AddThemeStyleboxOverride("normal", normal);
        b.AddThemeStyleboxOverride("hover", pressed);
        b.AddThemeStyleboxOverride("pressed", pressed);
        b.AddThemeStyleboxOverride("disabled", SB(new Color(0.85f, 0.85f, 0.83f), r, Palette.Gray, 2));
        b.AddThemeColorOverride("font_color", Palette.Ink);
        b.AddThemeColorOverride("font_hover_color", Palette.Ink);
        b.AddThemeColorOverride("font_pressed_color", Palette.Ink);
        b.AddThemeColorOverride("font_disabled_color", Palette.Gray);
        b.AddThemeFontOverride("font", FontBold);
        b.AddThemeFontSizeOverride("font_size", fontSize);
        if (onPressed != null) b.Pressed += onPressed;
        Fx.Press(b);
        return b;
    }

    /// <summary>幽灵按钮（无边框纯文字操作）。</summary>
    public static Button BtnGhost(string text, Action onPressed, int fontSize = 13, Color? color = null)
    {
        var b = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, Flat = true };
        b.AddThemeColorOverride("font_color", color ?? Palette.StoneBlue);
        b.AddThemeFontSizeOverride("font_size", fontSize);
        b.AddThemeFontOverride("font", FontBold);
        if (onPressed != null) b.Pressed += onPressed;
        return b;
    }

    /// <summary>广告按钮：主按钮规格 + 15s 进度环图标（FX-19）。</summary>
    public static Control AdButton(string text, float ringSeconds, Action onPressed, float height = 52)
    {
        var wrap = new Control { CustomMinimumSize = new Vector2(0, height) };
        var btn = BtnPrimary(text, onPressed, height);
        btn.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        wrap.AddChild(btn);
        var ring = new ProgressRing { RingSeconds = ringSeconds, RingColor = Palette.Gamboge };
        ring.AnchorLeft = 0; ring.AnchorRight = 0; ring.AnchorTop = 0.5f; ring.AnchorBottom = 0.5f;
        ring.OffsetLeft = 16; ring.OffsetRight = 42; ring.OffsetTop = -13; ring.OffsetBottom = 13;
        ring.MouseFilter = Control.MouseFilterEnum.Ignore;
        wrap.AddChild(ring);
        Fx.FloatLoop(ring, 1.12f, 1.6f);
        return wrap;
    }

    // ---------------- 屏幕栈 ----------------
    public static void PushScreen(Control screen, bool animate = true)
    {
        ScreenManager.I?.Push(screen, animate);
    }

    public static void PopScreen() => ScreenManager.I?.Pop();

    // ---------------- 弹窗 / Toast ----------------
    public static Popup ShowPopup(string title, Control content, bool dismissable = false, float width = 332, Action onClosed = null)
        => Popup.Show(title, content, dismissable, width, onClosed);

    public static Popup Confirm(string title, string message, string okText, Action onOk,
        string cancelText = "取消", bool danger = false, Action onCancel = null)
    {
        var v = VBox(14);
        v.AddChild(LWrap(message, 14, Palette.InkSoft));
        var row = HBox(10);
        row.Alignment = BoxContainer.AlignmentMode.Center;
        var cancel = BtnSecondary(cancelText, () => { onCancel?.Invoke(); Popup.Current?.Close(); }, 44, 15);
        cancel.CustomMinimumSize = new Vector2(120, 44);
        var ok = danger ? BtnDanger(okText, () => { onOk?.Invoke(); Popup.Current?.Close(); }, 44, 15)
                        : BtnPrimary(okText, () => { onOk?.Invoke(); Popup.Current?.Close(); }, 44, 15);
        ok.CustomMinimumSize = new Vector2(140, 44);
        row.AddChild(cancel);
        row.AddChild(ok);
        v.AddChild(row);
        return ShowPopup(title, v, false, 332, null);
    }

    public static void Toast(string msg)
    {
        ScreenManager.I?.Toast(msg);
    }

    public static string Fmt(long v)
    {
        return v.ToString("N0");
    }

    /// <summary>星级字符串（★n☆6-n）。</summary>
    public static string Stars(int star)
    {
        star = Math.Clamp(star, 0, 6);
        return new string('★', star) + new string('☆', 6 - star);
    }

    /// <summary>横向筛选行（派系/品质/星级通用）：当前选中项红底反白。</summary>
    public static Control FilterRow(string title, string[] options, Func<string> getCurrent, Action<string> onPick)    {
        var row = HBox(6);
        var t = L(title, 11, Palette.Gray);
        t.CustomMinimumSize = new Vector2(28, 0);
        t.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(t);
        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 30),
        };
        var h = HBox(5);
        scroll.AddChild(h);
        row.AddChild(scroll);
        foreach (var opt in options)
        {
            string o = opt;
            var b = BtnSecondary(opt, () =>
            {
                onPick(o);
            }, 26, 11);
            b.CustomMinimumSize = new Vector2(0, 26);
            if (getCurrent() == o)
            {
                b.AddThemeColorOverride("font_color", Colors.White);
                b.AddThemeColorOverride("font_hover_color", Colors.White);
                b.AddThemeColorOverride("font_pressed_color", Colors.White);
                b.AddThemeStyleboxOverride("normal", SB(Palette.Cinnabar, 12));
                b.AddThemeStyleboxOverride("hover", SB(Palette.Cinnabar, 12));
                b.AddThemeStyleboxOverride("pressed", SB(Palette.Cinnabar.Darkened(0.1f), 12));
            }
            h.AddChild(b);
        }
        return row;
    }
}

public static class UIArt
{
    /// <summary>
    /// 武将详情页背景：势力底图 + 品质装饰（品质越高叠加越多层次）。
    /// 同派系低品质=简约，高品质叠加横幅/四角纹样/光晕。
    /// </summary>
    public static Control FactionBackdrop(string faction, string quality, Color? baseFallback = null)
    {
        int tier = Quality.Tier(quality);
        var qcol = Quality.Color(quality);
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };

        var tex = Art.FactionBg(faction);
        if (tex != null)
        {
            var tr = new TextureRect
            {
                Texture = tex,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Modulate = new Color(1, 1, 1, 0.92f),
            };
            tr.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            root.AddChild(tr);
        }
        else
        {
            var paper = new ColorRect { Color = baseFallback ?? Palette.Paper, MouseFilter = Control.MouseFilterEnum.Ignore };
            paper.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            root.AddChild(paper);
        }

        // 品质 ≥ 蓝：顶部品质色横幅
        if (tier >= 1)
        {
            var band = new ColorRect { Color = new Color(qcol, 0.16f), MouseFilter = Control.MouseFilterEnum.Ignore };
            band.AnchorLeft = 0; band.AnchorRight = 1; band.AnchorTop = 0; band.AnchorBottom = 0;
            band.OffsetTop = 0; band.OffsetBottom = 74;
            root.AddChild(band);
        }
        // 品质 ≥ 紫：四角纹样
        if (tier >= 2)
        {
            for (int i = 0; i < 4; i++)
            {
                var corner = new ColorRect { Color = new Color(qcol, 0.55f), MouseFilter = Control.MouseFilterEnum.Ignore };
                corner.AnchorLeft = i % 2 == 0 ? 0 : 1;
                corner.AnchorRight = i % 2 == 0 ? 0 : 1;
                corner.AnchorTop = i / 2 == 0 ? 0 : 1;
                corner.AnchorBottom = i / 2 == 0 ? 0 : 1;
                float dx = i % 2 == 0 ? 10 : -30;
                float dy = i / 2 == 0 ? 12 : -30;
                corner.OffsetLeft = dx; corner.OffsetRight = dx + 20;
                corner.OffsetTop = dy; corner.OffsetBottom = dy + 20;
                root.AddChild(corner);
            }
        }
        // 金色：底部光晕 + 中央淡金晕染
        if (tier >= 3)
        {
            var glow = new ColorRect { Color = new Color(qcol, 0.20f), MouseFilter = Control.MouseFilterEnum.Ignore };
            glow.AnchorLeft = 0; glow.AnchorRight = 1; glow.AnchorTop = 1; glow.AnchorBottom = 1;
            glow.OffsetTop = -180; glow.OffsetBottom = 0;
            root.AddChild(glow);
            var halo = new ColorRect { Color = new Color(qcol, 0.10f), MouseFilter = Control.MouseFilterEnum.Ignore };
            halo.AnchorLeft = 0.5f; halo.AnchorRight = 0.5f; halo.AnchorTop = 0.38f; halo.AnchorBottom = 0.38f;
            halo.OffsetLeft = -130; halo.OffsetRight = 130; halo.OffsetTop = -130; halo.OffsetBottom = 130;
            root.AddChild(halo);
        }
        return root;
    }
}

// ============================================================
//  控件扩展
// ============================================================
public static class NodeExt{
    /// <summary>立即清空并释放所有子节点（重建列表用）。</summary>
    public static void ClearChildren(this Node node)
    {
        foreach (var c in node.GetChildren())
        {
            node.RemoveChild(c);
            c.QueueFree();
        }
    }
}

// ============================================================
//  动效助手（04 文档 FX-01/02/05/04/06/12/14/21）
// ============================================================
public static class Fx
{
    private static MotionCfg M => ConfigDb.I.Motion;

    public static float Dur(string key, float fb) => M.F(key, fb);
    public static float Nested(string path, float fb) => M.Nested(path, fb);

    /// <summary>FX-01 按钮按压：缩放 1.0→0.96→1.0。</summary>
    public static void Press(BaseButton c)
    {
        c.Resized += () => c.PivotOffset = c.Size / 2;
        c.ButtonDown += () =>
        {
            var t = c.CreateTween();
            t.TweenProperty(c, "scale", new Vector2(0.96f, 0.96f), Dur("dur_instant", 0.12f) * 0.5f)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        };
        c.ButtonUp += () =>
        {
            var t = c.CreateTween();
            t.TweenProperty(c, "scale", Vector2.One, Dur("dur_instant", 0.12f))
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        };
    }

    /// <summary>FX-02 页面转场（新页右滑入）。</summary>
    public static void PageIn(Control node)
    {
        float dur = Dur("dur_standard", 0.24f);
        var offset = Nested("transition.new_shift", 24f);
        node.Modulate = new Color(1, 1, 1, 0);
        node.Position = new Vector2(offset, 0);
        var t = node.CreateTween();
        t.SetParallel(true);
        t.TweenProperty(node, "modulate:a", 1.0f, dur).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        t.TweenProperty(node, "position:x", 0.0f, dur).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    /// <summary>返回时旧页轻微退场。</summary>
    public static void PageOut(Control node, Action onDone)
    {
        float dur = Dur("dur_standard", 0.24f);
        var t = node.CreateTween();
        t.SetParallel(true);
        t.TweenProperty(node, "modulate:a", 0.0f, dur * 0.8f);
        t.TweenProperty(node, "position:x", 24.0f, dur * 0.8f);
        t.Chain().TweenCallback(Callable.From(onDone));
    }

    /// <summary>FX-05 弹窗出现：0.85→1.05→1.0。</summary>
    public static void PopIn(Control node)
    {
        float dur = Dur("dur_standard", 0.24f);
        node.PivotOffset = node.Size / 2;
        node.Scale = new Vector2(0.85f, 0.85f);
        node.Modulate = new Color(1, 1, 1, 0);
        var t = node.CreateTween();
        t.SetParallel(true);
        t.TweenProperty(node, "scale", new Vector2(1.05f, 1.05f), dur * 0.7f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        t.TweenProperty(node, "modulate:a", 1.0f, dur * 0.5f);
        t.Chain().TweenProperty(node, "scale", Vector2.One, dur * 0.4f)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    public static void PopOut(Control node, Action onDone)
    {
        var t = node.CreateTween();
        t.SetParallel(true);
        t.TweenProperty(node, "scale", new Vector2(0.9f, 0.9f), 0.16f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        t.TweenProperty(node, "modulate:a", 0.0f, 0.16f);
        t.Chain().TweenCallback(Callable.From(onDone));
    }

    /// <summary>FX-06 数值滚动（数字翻牌感）。</summary>
    public static void RollNumber(Label label, double from, double to, Action<double> format, float dur = -1)
    {
        if (dur <= 0) dur = Dur("number_roll.dur", 0.5f);
        var t = label.CreateTween();
        t.TweenMethod(Callable.From((float v) => format(v)), (float)from, (float)to, dur)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    /// <summary>FX-11 卡牌入场（延迟到入树后播放；容器子节点只做缩放+淡入，避免与容器布局冲突）。</summary>
    public static void CardEnter(Control node, float delay = 0)
    {
        Callable.From(() => CardEnterNow(node, delay)).CallDeferred();
    }

    private static void CardEnterNow(Control node, float delay)
    {
        if (!GodotObject.IsInstanceValid(node) || !node.IsInsideTree()) return;
        float dur = Nested("card_enter.dur", 0.36f);
        float y = Nested("card_enter.y", 40f);
        node.Modulate = new Color(1, 1, 1, 0);
        node.PivotOffset = node.Size / 2;
        node.Scale = new Vector2(0.85f, 0.85f);
        var t = node.CreateTween();
        t.SetParallel(true);
        t.TweenProperty(node, "modulate:a", 1.0f, dur * 0.6f).SetDelay(delay);
        t.TweenProperty(node, "scale", Vector2.One, dur)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out)
            .SetDelay(delay);
        if (node.GetParent() is not Container)
        {
            t.TweenProperty(node, "position:y", node.Position.Y, dur)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out)
                .SetDelay(delay)
                .From(node.Position.Y + y);
        }
    }

    /// <summary>FX-12 呼吸浮动。</summary>
    public static void Breathe(Control node, float amp = -1, float period = -1)
    {
        if (amp <= 0) amp = Nested("breathing.amp", 3f);
        if (period <= 0) period = Nested("breathing.period", 3f);
        float baseY = node.Position.Y;
        var t = node.CreateTween();
        t.SetLoops();
        t.TweenProperty(node, "position:y", baseY - amp, period * 0.5f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        t.TweenProperty(node, "position:y", baseY, period * 0.5f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
    }

    /// <summary>FX-19 常驻微浮动。</summary>
    public static void FloatLoop(Control node, float scale, float period)
    {
        var t = node.CreateTween();
        t.SetLoops();
        t.TweenProperty(node, "scale", new Vector2(scale, scale), period * 0.5f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        t.TweenProperty(node, "scale", Vector2.One, period * 0.5f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
    }

    /// <summary>FX-20 胜利：上浮 + 淡入；失败：灰化。</summary>
    public static void RiseIn(Control node, float dist = 24f)
    {
        node.Modulate = new Color(1, 1, 1, 0);
        var t = node.CreateTween();
        t.SetParallel(true);
        t.TweenProperty(node, "modulate:a", 1.0f, Dur("dur_emphasis", 0.36f));
        t.TweenProperty(node, "position:y", node.Position.Y, Dur("dur_emphasis", 0.36f))
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out)
            .From(node.Position.Y + dist);
    }

    public static void Shake(Control node, float strength = 4f, int times = 3)
    {
        var t = node.CreateTween();
        float x = node.Position.X;
        for (int i = 0; i < times; i++)
        {
            t.TweenProperty(node, "position:x", x + strength, 0.04f);
            t.TweenProperty(node, "position:x", x - strength, 0.04f);
        }
        t.TweenProperty(node, "position:x", x, 0.04f);
    }
}
