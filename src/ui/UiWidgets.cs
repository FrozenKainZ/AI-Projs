using System;
using System.Collections.Generic;
using Godot;
using SanguoBasketball.Core;
using SanguoBasketball.Systems;

namespace SanguoBasketball.UI;

// ============================================================
//  通用控件：体力条 / 红点 / 数字滚动 / 进度环 / 武将卡 / 底部导航 / 资源条
// ============================================================

/// <summary>体力条（03 文档 §3.3：高 6px 圆角，绿/黄/红三色）。</summary>
public partial class StaminaBar : Control
{
    private int _cur = 100, _max = 100;

    public StaminaBar(float w, float h = 6)
    {
        CustomMinimumSize = new Vector2(w, h);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void SetValue(int cur, int max)
    {
        _cur = cur;
        _max = Math.Max(1, max);
        QueueRedraw();
    }

    public override void _Draw()
    {
        float pct = Mathf.Clamp(_cur / (float)_max, 0f, 1f);
        Color c = pct >= 0.5f ? Palette.StaminaGreen : pct >= 0.2f ? Palette.StaminaYellow : Palette.StaminaRed;
        var bg = new Rect2(Vector2.Zero, Size);
        DrawRect(bg, new Color(0, 0, 0, 0.12f));
        DrawRect(new Rect2(Vector2.Zero, new Vector2(Size.X * pct, Size.Y)), c);
    }
}

/// <summary>红点（FX-14 脉冲）。</summary>
public partial class RedDot : Control
{
    private float _t;

    public RedDot(float size = 8)
    {
        CustomMinimumSize = new Vector2(size, size);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float period = Fx.Nested("reddot.period", 1.2f);
        float k = 0.5f + 0.5f * MathF.Sin(_t / period * MathF.Tau);
        float sc = 1.0f + 0.2f * k;
        float alpha = 1.0f - 0.25f * k;
        var center = Size / 2;
        float r = Size.X / 2 * sc;
        DrawCircle(center, r, new Color(1, 1, 1, alpha));
        DrawCircle(center, r * 0.75f, new Color(Palette.Cinnabar, alpha));
    }
}

/// <summary>数字滚动标签（FX-06）。</summary>
public partial class NumberLabel : Label
{
    public long Value;
    private long _shown;

    public NumberLabel(long value, int size = 16, Color? color = null, bool bold = true)
    {
        Value = _shown = value;
        AddThemeFontSizeOverride("font_size", size);
        AddThemeColorOverride("font_color", color ?? Palette.Ink);
        if (bold) AddThemeFontOverride("font", UiKit.FontBold);
        Text = value.ToString("N0");
    }

    public void SetValue(long v, bool animate = true)
    {
        long old = Value;
        Value = v;
        if (!animate || !IsInsideTree())
        {
            _shown = v;
            Text = v.ToString("N0");
            return;
        }
        Fx.RollNumber(this, old, v, val =>
        {
            _shown = (long)val;
            Text = _shown.ToString("N0");
        });
    }
}

/// <summary>进度环（广告按钮 15s 倒计时，FX-19）。</summary>
public partial class ProgressRing : Control
{
    public float RingSeconds = 15f;
    public Color RingColor = Palette.Gamboge;
    private float _progress; // 0..1
    private Tween _tween;

    public ProgressRing()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(26, 26);
    }

    public void StartCountdown(float seconds, Action onDone)
    {
        _tween?.Kill();
        _progress = 0;
        _tween = CreateTween();
        _tween.TweenMethod(Callable.From((float v) => { _progress = v; QueueRedraw(); }), 0f, 1f, seconds)
            .SetTrans(Tween.TransitionType.Linear);
        _tween.TweenCallback(Callable.From(() => onDone?.Invoke()));
    }

    public void Reset()
    {
        _tween?.Kill();
        _progress = 0;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var center = Size / 2;
        float r = Math.Min(Size.X, Size.Y) / 2 - 2;
        DrawArc(center, r, 0, Mathf.Tau, 48, new Color(RingColor, 0.25f), 2.5f, true);
        if (_progress > 0.001f)
            DrawArc(center, r, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * _progress, 48, RingColor, 2.5f, true);
    }
}

/// <summary>
/// 武将卡（03 文档 §3.2）：品质描边、角标、星级、体力条、可显示犯规点。
/// 支持玩家卡（HeroInst）与对手卡（OpponentPlayer / 剪影）。
/// </summary>
public partial class HeroCard : Control
{
    public float W;
    public float StripH => Math.Max(0.40f * W, 21f);
    public float CardH => W + StripH;
    public float TotalH => CardH + (ShowStaminaBar ? 10 : 0);

    public bool ShowStaminaBar = false;
    public HeroInst Inst;
    public string DefId;
    public string CardName = "";
    public string QualityKey = "green";
    public int Star = 1;
    public bool Silhouette;

    private TextureRect _art;
    private Panel _border;
    private ColorRect _strip;
    private Label _name;
    private Label _stars;
    private Label _badge;
    private StaminaBar _sta;
    private FoulDots _fouls;

    public HeroCard(float w)
    {
        W = w;
        CustomMinimumSize = new Vector2(w, CardH);
        MouseFilter = MouseFilterEnum.Stop;
    }

    private void Ensure()
    {
        if (_art != null) return;
        _art = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _art.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_art);

        float stripH = StripH;
        _strip = new ColorRect { Color = new Color(Palette.Ink, 0.72f), MouseFilter = MouseFilterEnum.Ignore };
        _strip.AnchorLeft = 0; _strip.AnchorRight = 1; _strip.AnchorTop = 1; _strip.AnchorBottom = 1;
        _strip.OffsetLeft = 0; _strip.OffsetRight = 0; _strip.OffsetTop = -stripH; _strip.OffsetBottom = 0;
        AddChild(_strip);

        _stars = UiKit.L("", (int)Math.Max(8, W * 0.15f), Palette.Gamboge, true);
        _stars.HorizontalAlignment = HorizontalAlignment.Center;
        _stars.AnchorLeft = 0; _stars.AnchorRight = 1; _stars.AnchorTop = 1; _stars.AnchorBottom = 1;
        _stars.OffsetLeft = 0; _stars.OffsetRight = 0;
        _stars.OffsetTop = -stripH + 1;
        _stars.OffsetBottom = -(stripH * 0.48f);
        _stars.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_stars);

        _name = UiKit.L("", (int)Math.Max(9, W * 0.17f), Colors.White, true);
        _name.HorizontalAlignment = HorizontalAlignment.Center;
        _name.VerticalAlignment = VerticalAlignment.Center;
        _name.AnchorLeft = 0; _name.AnchorRight = 1; _name.AnchorTop = 1; _name.AnchorBottom = 1;
        _name.OffsetLeft = 0; _name.OffsetRight = 0;
        _name.OffsetTop = -(stripH * 0.55f);
        _name.OffsetBottom = -1;
        _name.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_name);

        _border = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        _border.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_border);

        _badge = UiKit.L("", (int)Math.Max(9, W * 0.16f), Colors.White, true);
        _badge.AddThemeConstantOverride("outline_size", 3);
        _badge.AddThemeColorOverride("font_outline_color", new Color("#3A2410"));
        _badge.Position = new Vector2(W * 0.06f, W * 0.05f);
        _badge.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_badge);

        if (ShowStaminaBar)
        {
            _sta = new StaminaBar(W, 5);
            _sta.Position = new Vector2(0, CardH + 3);
            AddChild(_sta);
        }
        _fouls = new FoulDots(W, 7);
        _fouls.Position = new Vector2(0, CardH + (ShowStaminaBar ? 10 : 2));
        _fouls.Visible = false;
        AddChild(_fouls);
    }

    public void Setup(HeroInst inst, string badge = null, bool showStamina = false)
    {
        ShowStaminaBar = showStamina;
        Ensure();
        Inst = inst;
        DefId = inst.DefId;
        var def = inst.Def;
        CardName = def.Name;
        QualityKey = def.QualityKey;
        Star = inst.Star;
        Silhouette = false;
        Refresh(badge);
        if (showStamina) _sta?.SetValue(inst.Sta, Systems.HeroCalc.StaCapTotal(inst));
    }

    public void SetupRaw(string defId, string name, string quality, int star, string badge = null)
    {
        Ensure();
        Inst = null;
        DefId = defId;
        CardName = name;
        QualityKey = quality;
        Star = star;
        Silhouette = false;
        Refresh(badge);
    }

    public void SetupSilhouette(string name, string quality, int star, string badge = null)
    {
        Ensure();
        Inst = null;
        DefId = null;
        CardName = name;
        QualityKey = quality;
        Star = star;
        Silhouette = true;
        Refresh(badge);
    }

    private void Refresh(string badge)
    {
        var tex = DefId != null ? Art.Hero(DefId) : null;
        _art.Texture = tex;
        _art.Modulate = Silhouette ? new Color(0.30f, 0.31f, 0.35f) : Colors.White;
        if (tex == null)
        {
            // 占位：品质色底
            _art.Texture = null;
            _art.Modulate = Colors.White;
        }
        _name.Text = CardName;
        _stars.Text = starString(Star);
        _stars.Visible = Star > 0;
        _badge.Text = badge ?? "";
        _badge.Visible = !string.IsNullOrEmpty(badge);
        var col = Quality.Color(QualityKey);
        _border.AddThemeStyleboxOverride("panel", UiKit.SB(new Color(0, 0, 0, 0), 8, col, 3));
        QueueRedraw();
    }

    private static string starString(int star)
    {
        star = Math.Clamp(star, 0, 6);
        return new string('★', star) + new string('☆', Math.Max(0, 6 - star));
    }

    public void SetFouls(int n, bool warn)
    {
        Ensure();
        if (_fouls == null) return;
        _fouls.Visible = n > 0;
        _fouls.SetFouls(n, warn);
    }

    public void SetStamina(int cur, int max)
    {
        Ensure();
        _sta?.SetValue(cur, max);
    }

    public override void _Draw()
    {
        // 无图占位：品质色块 + 首字
        var tex = DefId != null ? Art.Hero(DefId) : null;
        if (tex != null) return;
        float cx = W / 2;
        var c = Quality.Color(QualityKey);
        DrawRect(new Rect2(Vector2.Zero, new Vector2(W, CardH)), Silhouette ? new Color(0.85f, 0.85f, 0.84f) : c.Darkened(0.55f));
        if (Silhouette)
        {
            // 背影剪影简笔
            DrawCircle(new Vector2(cx, CardH * 0.34f), W * 0.16f, new Color(0.55f, 0.56f, 0.6f));
            DrawRect(new Rect2(cx - W * 0.22f, CardH * 0.48f, W * 0.44f, CardH * 0.34f), new Color(0.55f, 0.56f, 0.6f));
        }
        else if (!string.IsNullOrEmpty(CardName))
        {
            var font = UiKit.FontBold;
            int fs = (int)(W * 0.4f);
            var size = font.GetStringSize(CardName.Substring(0, 1), HorizontalAlignment.Center, -1, fs);
            DrawString(font, new Vector2(cx - size.X / 2, CardH * 0.62f), CardName.Substring(0, 1),
                HorizontalAlignment.Left, -1, fs, new Color(1, 1, 1, 0.9f));
        }
    }
}

/// <summary>犯规点行。</summary>
public partial class FoulDots : Control
{
    private int _n;
    private bool _warn;

    public FoulDots(float w, float h)
    {
        CustomMinimumSize = new Vector2(w, h);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void SetFouls(int n, bool warn)
    {
        _n = n;
        _warn = warn;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float r = Size.Y / 2 - 0.5f;
        for (int i = 0; i < 6; i++)
        {
            var c = i < _n ? Palette.Cinnabar : new Color(0, 0, 0, 0.10f);
            if (_warn && i < _n) c = Palette.Cinnabar.Lightened(0.3f);
            DrawCircle(new Vector2(r + i * (r * 2 + 3), Size.Y / 2), r, c);
        }
    }
}

/// <summary>底部五宫格导航（作战 / 阵容 / 城池 / 军库 / 图鉴）。</summary>
public partial class BottomNav : PanelContainer
{
    public Action<string> OnTab;
    private readonly Dictionary<string, (Panel circle, Label label, Label icon)> _items = new();
    private string _active = "main";

    public static readonly (string id, string cn, string icon)[] Tabs =
    {
        ("main", "作战", "战"),
        ("lineup", "阵容", "阵"),
        ("gacha", "城池", "城"),
        ("bag", "军库", "库"),
        ("codex", "图鉴", "鉴"),
    };

    public BottomNav()
    {
        var sb = UiKit.SB(Palette.CardBg, 0);
        sb.BorderColor = new Color(0, 0, 0, 0.08f);
        sb.BorderWidthTop = 1;
        AddThemeStyleboxOverride("panel", sb);
        CustomMinimumSize = new Vector2(0, 84);

        var row = UiKit.HBox(0);
        row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(row);

        foreach (var (id, cn, icon) in Tabs)
        {
            var item = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 78) };
            var btn = new Button { Flat = true, FocusMode = FocusModeEnum.None };
            btn.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            string tabId = id;
            btn.Pressed += () => OnTab?.Invoke(tabId);
            item.AddChild(btn);

            var circle = new Panel { CustomMinimumSize = new Vector2(28, 28), MouseFilter = MouseFilterEnum.Ignore };
            circle.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
            circle.Position = new Vector2(-14, 10);
            item.AddChild(circle);
            var iconLabel = UiKit.L(icon, 13, Palette.Ink, true);
            iconLabel.HorizontalAlignment = HorizontalAlignment.Center;
            iconLabel.VerticalAlignment = VerticalAlignment.Center;
            iconLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            iconLabel.MouseFilter = MouseFilterEnum.Ignore;
            circle.AddChild(iconLabel);

            var label = UiKit.L(cn, 12, Palette.Gray);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.AnchorLeft = 0; label.AnchorRight = 1; label.AnchorTop = 0; label.AnchorBottom = 0;
            label.OffsetTop = 44; label.OffsetLeft = 0; label.OffsetRight = 0;
            label.MouseFilter = MouseFilterEnum.Ignore;
            item.AddChild(label);

            _items[id] = (circle, label, iconLabel);
            row.AddChild(item);
        }
        SetActive(_active);
    }

    public void SetActive(string id)
    {
        _active = id;
        foreach (var (tid, (circle, label, icon)) in _items)
        {
            bool active = tid == id;
            circle.AddThemeStyleboxOverride("panel", UiKit.SB(active ? Palette.Cinnabar : new Color("#E2E6EE"), 14));
            label.AddThemeColorOverride("font_color", active ? Palette.Cinnabar : Palette.Gray);
            icon.AddThemeColorOverride("font_color", active ? Colors.White : Palette.Ink);
        }
    }

    /// <summary>FX-03 Tab 切换弹跳。</summary>
    public void Bounce(string id, Control node)
    {
        _ = id;
        var t = node.CreateTween();
        t.TweenProperty(node, "scale", new Vector2(1.15f, 1.15f), 0.12f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        t.TweenProperty(node, "scale", Vector2.One, 0.12f);
    }

    public Panel CircleOf(string id) => _items.TryGetValue(id, out var v) ? v.circle : null;
}

/// <summary>全局顶栏：主公名号 + 头衔 + 金币 / 铜虎符 / 玉虎符（所有页面常驻）。</summary>
public partial class ResourceBar : HBoxContainer
{
    public NumberLabel CoinLabel;
    public NumberLabel CopperLabel;
    public NumberLabel JadeLabel;

    public ResourceBar()
    {
        AddThemeConstantOverride("separation", 6);
        Alignment = AlignmentMode.End;

        var coinChip = MakeChip(Art.CoinIcon(), () => UiKit.Toast("金币 · 市场抽卡与挑战副本的通用货币"));
        CoinLabel = new NumberLabel(GameState.D.Coins, 12, Palette.Ink);
        BindLabel(coinChip, CoinLabel, 24);
        AddChild(coinChip);

        var copperChip = MakeChip(Art.TaliIcon("copper"), () => UiKit.Toast("铜虎符 · 兵营单抽（10 枚 = 一次十连）"));
        CopperLabel = new NumberLabel(GameState.D.Copper, 12, Palette.Ink);
        BindLabel(copperChip, CopperLabel, 24);
        AddChild(copperChip);

        var jadeChip = MakeChip(Art.TaliIcon("jade"), () => UiKit.Toast("玉虎符 · 兵营十连（1 枚 = 一次十连）"));
        JadeLabel = new NumberLabel(GameState.D.Jade, 12, Palette.Ink);
        BindLabel(jadeChip, JadeLabel, 24);
        AddChild(jadeChip);
    }

    private static void BindLabel(Control chip, NumberLabel label, float iconW)
    {
        label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        label.OffsetLeft = iconW + 2;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.AddThemeFontSizeOverride("font_size", 12);
        chip.AddChild(label);
    }

    private Control MakeChip(Texture2D icon, Action onClick)
    {
        var chip = new Panel { CustomMinimumSize = new Vector2(74, 30), MouseFilter = MouseFilterEnum.Stop };
        var sb = UiKit.SB(Colors.White, 15, Palette.Ink, 2);
        sb.ShadowColor = new Color(0, 0, 0, 0.12f);
        sb.ShadowSize = 2;
        sb.ShadowOffset = new Vector2(0, 2);
        chip.AddThemeStyleboxOverride("panel", sb);
        if (icon != null)
        {
            var tr = new TextureRect { Texture = icon, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore };
            tr.AnchorLeft = 0; tr.AnchorRight = 0; tr.AnchorTop = 0.5f; tr.AnchorBottom = 0.5f;
            tr.OffsetLeft = 4; tr.OffsetRight = 24; tr.OffsetTop = -9; tr.OffsetBottom = 9;
            chip.AddChild(tr);
        }
        chip.GuiInput += (ev) =>
        {
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) onClick?.Invoke();
        };
        return chip;
    }

    public void Refresh()
    {
        CoinLabel?.SetValue(GameState.D.Coins, false);
        CopperLabel?.SetValue(GameState.D.Copper, false);
        JadeLabel?.SetValue(GameState.D.Jade, false);
    }

    private double _refreshT;

    public override void _Process(double delta)
    {
        // 资源变动后轻量自刷新（避免各处手动通知）
        _refreshT += delta;
        if (_refreshT < 0.45) return;
        _refreshT = 0;
        Refresh();
    }
}
