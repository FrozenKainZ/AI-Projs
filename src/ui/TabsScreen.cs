using System;
using Godot;
using SanguoBasketball.Core;

namespace SanguoBasketball.UI;

/// <summary>底部五宫格宿主：作战 / 阵容 / 抽卡 / 背包 / 图鉴。</summary>
public partial class TabsScreen : Control
{
    public static TabsScreen I { get; private set; }

    public BottomNav Nav { get; private set; }
    public MainTab MainTabRef { get; private set; }
    public ResourceBar Header { get; private set; }

    private Control _content;
    private Control _current;
    private Label _rankLabel;
    private string _lastRank = "";

    public override void _Process(double delta)
    {
        // 头衔随历史最强综评变化
        string r = GameState.RankTitle();
        if (r != _lastRank)
        {
            _lastRank = r;
            if (_rankLabel != null) _rankLabel.Text = $"主公 · {r}";
        }
    }

    public override void _Ready()
    {
        I = this;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var bg = new ColorRect { Color = Palette.Paper, MouseFilter = MouseFilterEnum.Ignore };
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        // ---- 全局顶栏：主公名号 + 头衔 + 金币/虎符/玉虎符（五个页签常驻） ----
        var headerMargin = UiKit.Pad(16, 0, 16, 0);
        headerMargin.AnchorLeft = 0; headerMargin.AnchorRight = 1; headerMargin.AnchorTop = 0; headerMargin.AnchorBottom = 0;
        headerMargin.OffsetTop = 44; headerMargin.OffsetBottom = 86;
        headerMargin.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(headerMargin);
        var headRow = UiKit.HBox(8);
        headerMargin.AddChild(headRow);

        var nameCol = UiKit.VBox(0);
        _rankLabel = UiKit.L($"主公 · {GameState.RankTitle()}", 14, Palette.Ink, true);
        nameCol.AddChild(_rankLabel);
        nameCol.AddChild(UiKit.L("三国篮球志", 9, Palette.Gray));
        headRow.AddChild(nameCol);
        var sp = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        headRow.AddChild(sp);
        Header = new ResourceBar();
        headRow.AddChild(Header);

        _content = new Control { MouseFilter = MouseFilterEnum.Ignore };
        _content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _content.OffsetTop = 90;
        _content.OffsetBottom = -84;
        AddChild(_content);

        Nav = new BottomNav();
        Nav.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomWide);
        Nav.OffsetTop = -84;
        AddChild(Nav);
        Nav.OnTab = id => Switch(id);

        Switch("main", false);
    }

    public void SwitchTo(string id) => Switch(id);

    /// <summary>当前显示的 Tab 内容（调试/截图用）。</summary>
    public Control GetTab() => _current;

    private void Switch(string id, bool animate = true)
    {
        if (_current != null)
        {
            _current.QueueFree();
            _current = null;
        }
        var c = BuildTab(id);
        _content.AddChild(c);
        _current = c;
        Nav.SetActive(id);
        if (animate)
        {
            c.Modulate = new Color(1, 1, 1, 0);
            var t = c.CreateTween();
            t.TweenProperty(c, "modulate:a", 1.0f, Fx.Dur("dur_standard", 0.24f));
            var circle = Nav.CircleOf(id);
            if (circle != null) Nav.Bounce(id, circle);
        }
    }

    private Control BuildTab(string id) => id switch
    {
        "main" => MainTabRef = new MainTab(),
        "lineup" => new LineupTab(),
        "gacha" => new GachaTab(),
        "bag" => new BagTab(),
        "codex" => new CodexTab(),
        _ => new TmpTab(id)
    };
}
