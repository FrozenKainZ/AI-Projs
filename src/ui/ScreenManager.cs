using System;
using System.Collections.Generic;
using Godot;
using SanguoBasketball.Core;

namespace SanguoBasketball.UI;

/// <summary>屏幕栈 + 弹窗层 + Toast 层（01 文档 §5 全局状态机的 UI 载体）。</summary>
public partial class ScreenManager : Control
{
    public static ScreenManager I { get; private set; }

    public Control World { get; private set; }
    public Control PopupLayer { get; private set; }
    public Control ToastLayer { get; private set; }

    private readonly List<Control> _stack = new();

    public override void _Ready()
    {
        I = this;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        World = new Control { MouseFilter = MouseFilterEnum.Ignore };
        World.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(World);

        PopupLayer = new Control { MouseFilter = MouseFilterEnum.Ignore };
        PopupLayer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(PopupLayer);

        ToastLayer = new Control { MouseFilter = MouseFilterEnum.Ignore };
        ToastLayer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(ToastLayer);
    }

    public Control Top => _stack.Count > 0 ? _stack[_stack.Count - 1] : null;

    public void Push(Control screen, bool animate = true)
    {
        var prev = Top;
        if (prev != null && animate)
        {
            var t = prev.CreateTween();
            t.SetParallel(true);
            t.TweenProperty(prev, "modulate:a", 0.6f, Fx.Dur("dur_standard", 0.24f));
            t.TweenProperty(prev, "position:x", -8.0f, Fx.Dur("dur_standard", 0.24f));
        }
        _stack.Add(screen);
        World.AddChild(screen);
        if (animate) Fx.PageIn(screen);
    }

    public void Pop()
    {
        if (_stack.Count <= 1) return;
        var top = _stack[_stack.Count - 1];
        _stack.RemoveAt(_stack.Count - 1);
        var below = Top;
        if (below != null)
        {
            var t = below.CreateTween();
            t.SetParallel(true);
            t.TweenProperty(below, "modulate:a", 1.0f, Fx.Dur("dur_fast", 0.2f));
            t.TweenProperty(below, "position:x", 0.0f, Fx.Dur("dur_fast", 0.2f));
        }
        Fx.PageOut(top, () => top.QueueFree());
    }

    /// <summary>清空栈并推入新根（切换底部 Tab 用）。</summary>
    public void ResetTo(Control screen)
    {
        foreach (var s in _stack) s.QueueFree();
        _stack.Clear();
        Push(screen, true);
    }

    public void PushPopup(Control popup) => PopupLayer.AddChild(popup);

    /// <summary>FX-21 Toast：顶部滑入 → 2s → 淡出。</summary>
    public void Toast(string msg)
    {
        var wrap = new Control { MouseFilter = MouseFilterEnum.Ignore };
        wrap.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        ToastLayer.AddChild(wrap);

        var panel = UiKit.Card(20, new Color(Palette.Ink, 0.92f), null, 0, 10);
        panel.MouseFilter = MouseFilterEnum.Ignore;
        panel.AddChild(UiKit.L(msg, 13, Colors.White));
        wrap.AddChild(panel);

        float fadeIn = Fx.Nested("toast.in", 0.2f);
        float hold = Fx.Nested("toast.hold", 2.0f);
        float fadeOut = Fx.Nested("toast.out", 0.16f);

        Callable.From(() =>
        {
            float w = panel.Size.X;
            panel.Position = new Vector2((Size.X - w) / 2f, -46);
            var t = panel.CreateTween();
            t.TweenProperty(panel, "position:y", 12.0f, fadeIn)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            t.TweenInterval(hold);
            t.SetParallel(true);
            t.TweenProperty(panel, "position:y", -46.0f, fadeOut);
            t.TweenProperty(panel, "modulate:a", 0.0f, fadeOut);
            t.Chain().TweenCallback(Callable.From(() => wrap.QueueFree()));
        }).CallDeferred();
    }
}

/// <summary>
/// 通用弹窗（03 文档 §3.4）：宽 310+ 居中、宣纸白底、描边、印章式标题块、背景压暗 45%。
/// </summary>
public partial class Popup : Control
{
    public static Popup Current;

    private Action _onClosed;
    private bool _dismissable;
    private bool _closing;
    private ColorRect _dim;
    public PanelContainer Panel;

    public static Popup Show(string title, Control content, bool dismissable = false, float width = 332, Action onClosed = null)
    {
        var p = new Popup();
        p.Build(title, content, dismissable, width, onClosed);
        ScreenManager.I.PushPopup(p);
        return p;
    }

    private void Build(string title, Control content, bool dismissable, float width, Action onClosed)
    {
        _dismissable = dismissable;
        _onClosed = onClosed;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        _dim = new ColorRect { Color = new Color(0, 0, 0, 0.45f), MouseFilter = MouseFilterEnum.Stop };
        _dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _dim.Modulate = new Color(1, 1, 1, 0);
        AddChild(_dim);
        var dt = _dim.CreateTween();
        dt.TweenProperty(_dim, "modulate:a", 1.0f, 0.2f);
        if (dismissable)
        {
            _dim.GuiInput += ev =>
            {
                if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) Close();
            };
        }

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        Panel = UiKit.Card(16, Palette.CardBg, Palette.Ink, 2, 0);
        Panel.CustomMinimumSize = new Vector2(width, 0);
        center.AddChild(Panel);

        var box = UiKit.VBox(12);
        Panel.AddChild(box);

        // 标题（印章式色块 + 文字）
        var head = UiKit.HBox(8);
        var seal = UiKit.Card(4, Palette.Cinnabar, null, 0, 0);
        seal.CustomMinimumSize = new Vector2(22, 22);
        var sealLabel = UiKit.L("令", 12, Colors.White, true);
        sealLabel.HorizontalAlignment = HorizontalAlignment.Center;
        sealLabel.VerticalAlignment = VerticalAlignment.Center;
        seal.AddChild(sealLabel);
        head.AddChild(seal);
        var titleLabel = UiKit.L(title, 16, Palette.Ink, true);
        titleLabel.VerticalAlignment = VerticalAlignment.Center;
        head.AddChild(titleLabel);
        box.AddChild(head);

        box.AddChild(content);

        if (dismissable)
        {
            var closeRow = UiKit.HBox(0);
            closeRow.Alignment = BoxContainer.AlignmentMode.Center;
            closeRow.AddChild(UiKit.BtnGhost("关闭", Close, 13, Palette.Gray));
            box.AddChild(closeRow);
        }

        Fx.PopIn(Panel);
        if (Current != null && Current != this && IsInstanceValid(Current))
            Current.Close();
        Current = this;
    }

    public void Close()
    {
        if (_closing) return;
        _closing = true;
        var t = CreateTween();
        t.TweenProperty(_dim, "modulate:a", 0.0f, 0.15f);
        Fx.PopOut(Panel, () =>
        {
            if (Current == this) Current = null;
            _onClosed?.Invoke();
            QueueFree();
        });
    }
}
