using System;
using Godot;
using SanguoBasketball.Core;

namespace SanguoBasketball.UI;

/// <summary>
/// 模拟广告播放器（开发期占位）：真机接入平台 SDK 后由 SdkAdService 替代。
/// 展示一个全屏"广告播放中"浮层，2.5 秒后回调完整观看。
/// </summary>
public static class MockAdOverlay
{
    public static void Play(string placementId, Action<bool> done)
    {
        if (ScreenManager.I == null)
        {
            done?.Invoke(true);
            return;
        }
        var ov = new MockAdView(placementId, done);
        ov.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        ScreenManager.I.AddChild(ov);
    }
}

public partial class MockAdView : Control
{
    private readonly string _placement;
    private readonly Action<bool> _done;
    private float _t;
    private Label _count;

    public MockAdView(string placement, Action<bool> done)
    {
        _placement = placement;
        _done = done;
        MouseFilter = MouseFilterEnum.Stop;
    }

    public override void _Ready()
    {
        var bg = new ColorRect { Color = new Color("#101319") };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var v = UiKit.VBox(14);
        v.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        v.Alignment = BoxContainer.AlignmentMode.Center;
        v.Position = new Vector2(-160, -110);
        v.CustomMinimumSize = new Vector2(320, 220);
        AddChild(v);

        var tag = UiKit.L("激励视频 · 模拟播放", 13, Palette.Gamboge, true);
        tag.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(tag);

        var nameMap = new System.Collections.Generic.Dictionary<string, string>
        {
            ["ADP_REVIVE"] = "复活点名广告位 ADP_REVIVE",
            ["ADP_ALTAR"] = "七星坛颁奖广告位 ADP_ALTAR",
        };
        nameMap.TryGetValue(_placement, out var pname);
        var sub = UiKit.L(pname ?? _placement, 12, Palette.Gray);
        sub.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(sub);

        _count = UiKit.L("2", 42, Colors.White, true);
        _count.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(_count);

        var note = UiKit.L("（接入平台 SDK 后此处为真实激励视频）", 11, Palette.Gray);
        note.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(note);
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        float left = Math.Max(0, 2.5f - _t);
        _count.Text = Math.Ceiling(left).ToString("0");
        if (_t >= 2.5f)
        {
            SetProcess(false);
            var t = CreateTween();
            t.TweenProperty(this, "modulate:a", 0.0f, 0.18f);
            t.TweenCallback(Callable.From(() =>
            {
                _done?.Invoke(true);
                QueueFree();
            }));
        }
    }
}
