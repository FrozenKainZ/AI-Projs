using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using SanguoBasketball.Core;
using SanguoBasketball.Systems;

namespace SanguoBasketball.UI;

/// <summary>SCR_GACHA · 城池：市场 / 兵营 抽卡 + 七星坛入口。</summary>
public partial class GachaTab : Control
{
    private string _pool = "market";
    private VBoxContainer _root;
    private Control _bannerHolder;
    private IconTextButton _singleBtn, _tenBtn;
    private Button _freeBtn;
    private Label _taliInfo;
    private Label _altarLabel;
    private RedDot _altarDot;
    private double _altarT;

    public override void _Ready()
    {
        Build();
        Refresh();
    }

    private void Build()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var margin = UiKit.Pad(16, 4, 16, 8);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(margin);
        _root = UiKit.VBox(8);
        margin.AddChild(_root);

        var head = UiKit.HBox(8);
        head.AddChild(UiKit.L("抽 卡", 18, Palette.Ink, true));
        var sp = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        head.AddChild(sp);
        head.AddChild(UiKit.BtnGhost("概率公示", ShowRatesPopup, 13, Palette.StoneBlue));
        head.AddChild(UiKit.BtnGhost("抽卡记录", ShowHistoryPopup, 13, Palette.Gray));
        _root.AddChild(head);

        // 池子切换
        var poolRow = UiKit.HBox(8);
        var marketBtn = UiKit.BtnSecondary("市 场", () => SetPool("market"), 40, 14);
        marketBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var barrackBtn = UiKit.BtnSecondary("兵 营", () => SetPool("barrack"), 40, 14);
        barrackBtn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        poolRow.AddChild(marketBtn);
        poolRow.AddChild(barrackBtn);
        _root.AddChild(poolRow);

        // 横幅插画区
        _bannerHolder = new Control { CustomMinimumSize = new Vector2(0, 150), MouseFilter = MouseFilterEnum.Ignore };
        _root.AddChild(_bannerHolder);

        // 产出预览
        _taliInfo = UiKit.L("", 11, Palette.Gray);
        _root.AddChild(_taliInfo);

        // 抽卡按钮（带货币图标）
        var btnRow = UiKit.HBox(10);
        _singleBtn = new IconTextButton(48, 15, () => DoDraw(1)) { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _tenBtn = new IconTextButton(48, 15, () => DoDraw(10)) { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        btnRow.AddChild(_singleBtn);
        btnRow.AddChild(_tenBtn);
        _root.AddChild(btnRow);

        // 免费十连（FTUE）
        _freeBtn = UiKit.BtnPrimary("免费十连（新手福利 · 限一次）", () =>
        {
            GameState.D.Ftue.FreeTenUsed = true;
            SaveSystem.MarkDirty();
            DoDraw(10, free: true);
            Ftue.OnFreeTenUsed();
            Ftue.TipLineup();
        }, 46, 15);
        _root.AddChild(_freeBtn);

        var fill = new Control { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        _root.AddChild(fill);

        // ---------- 七星坛入口（新家） ----------
        _root.AddChild(BuildAltarCard());
    }

    private Control BuildAltarCard()
    {
        var card = UiKit.Card(14, new Color("#1B2334"), Palette.Gamboge, 2, 10);
        card.MouseFilter = MouseFilterEnum.Stop;
        card.CustomMinimumSize = new Vector2(0, 86);
        card.ClipContents = true;

        var stage = new Control { MouseFilter = MouseFilterEnum.Ignore };
        card.AddChild(stage);
        var bgTex = Art.Bg("bg_altar");
        if (bgTex != null)
        {
            var tr = new TextureRect { Texture = bgTex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0.55f) };
            tr.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            stage.AddChild(tr);
        }

        var h = UiKit.HBox(10);
        h.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        h.OffsetLeft = 10; h.OffsetRight = -10; h.OffsetTop = 8; h.OffsetBottom = -8;
        h.MouseFilter = MouseFilterEnum.Ignore;
        stage.AddChild(h);

        var icon = Art.Icon("altar");
        var iconTex = new TextureRect { Texture = icon, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(56, 56) };
        h.AddChild(iconTex);

        var col = UiKit.VBox(2);
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        col.MouseFilter = MouseFilterEnum.Ignore;
        col.AddChild(UiKit.L("七 星 坛", 16, Colors.White, true));
        col.AddChild(UiKit.L("观看视频祈福 · 领取金币 / 虎符", 10, new Color(1, 1, 1, 0.6f)));
        _altarLabel = UiKit.L("", 12, Palette.Gamboge, true);
        col.AddChild(_altarLabel);
        h.AddChild(col);

        _altarDot = new RedDot(10);
        _altarDot.Position = new Vector2(-4, -2);
        h.AddChild(_altarDot);

        card.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                UiKit.PushScreen(new AltarScreen());
        };
        return card;
    }

    public override void _Process(double delta)
    {
        _altarT += delta;
        if (_altarT < 1.0) return;
        _altarT = 0;
        RefreshAltarCard();
    }

    private void RefreshAltarCard()
    {
        if (_altarLabel == null) return;
        int remain = AltarSystem.Remaining();
        _altarLabel.Text = remain > 0 ? $"今日祈福剩余 {remain}/7 次" : "今日次数已用完 · 明日再来";
        _altarDot.Visible = remain > 0;
    }

    private void SetPool(string pool)
    {
        _pool = pool;
        Refresh();
    }

    private void Refresh()
    {
        // 横幅
        _bannerHolder.ClearChildren();
        var banner = UiKit.Card(14, Palette.CardBg, Palette.Ink, 2, 0);
        banner.ClipContents = true;
        banner.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        // 非容器舞台层，避免 PanelContainer 接管子节点布局
        var stage = new Control { MouseFilter = MouseFilterEnum.Ignore };
        banner.AddChild(stage);
        var tex = Art.Bg(_pool == "market" ? "banner_market" : "banner_barrack");
        if (tex != null)
        {
            var tr = new TextureRect { Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore };
            tr.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            stage.AddChild(tr);
        }
        var tagPlate = UiKit.Card(8, new Color(Palette.CardBg, 0.86f), null, 0, 6);
        tagPlate.AnchorLeft = 0; tagPlate.AnchorRight = 0; tagPlate.AnchorTop = 1; tagPlate.AnchorBottom = 1;
        tagPlate.OffsetLeft = 10; tagPlate.OffsetRight = 210; tagPlate.OffsetTop = -34; tagPlate.OffsetBottom = -10;
        tagPlate.MouseFilter = MouseFilterEnum.Ignore;
        var tag = UiKit.L(_pool == "market" ? "金灿灿市集 · 产出材料 / 兵粮 / 虎符" : "军帐兵营 · 只产武将", 11, Palette.Ink, true);
        tagPlate.AddChild(tag);
        stage.AddChild(tagPlate);
        _bannerHolder.AddChild(banner);

        var coin = Art.CoinIcon();
        var copper = Art.TaliIcon("copper");
        var jade = Art.TaliIcon("jade");

        // 价格与资源（v2：带货币图标）
        if (_pool == "market")
        {
            _singleBtn.SetParts(new (string, Texture2D)[] { ($"单抽 {ConfigDb.I.C.MarketPrice1}", null), ("", coin) });
            _tenBtn.SetParts(new (string, Texture2D)[] { ($"十连 {ConfigDb.I.C.MarketPrice10}", null), ("", coin) });
            _singleBtn.SetDisabled(GameState.D.Coins < ConfigDb.I.C.MarketPrice1);
            _tenBtn.SetDisabled(GameState.D.Coins < ConfigDb.I.C.MarketPrice10);
            _taliInfo.Text = "产出预览：材料 62% · 兵粮 30% · 虎符 8%";
        }
        else
        {
            _singleBtn.SetParts(new (string, Texture2D)[] { ("单抽", null), ("", copper) });
            _tenBtn.SetParts(new (string, Texture2D)[]
            {
                ("十连（1", null), ("", jade), (" / 10", null), ("", copper), ("）", null)
            });
            _singleBtn.SetDisabled(GameState.D.Copper < 1);
            _tenBtn.SetDisabled(GameState.D.Jade < 1 && GameState.D.Copper < 10);
            _taliInfo.Text = "只产武将：绿 64.4% · 蓝 27.68% · 紫 7.06% · 金 0.86%";
        }
        _freeBtn.Visible = !GameState.D.Ftue.FreeTenUsed && Ftue.Step <= 2;
        RefreshAltarCard();
    }

    // ---------------- 抽卡流程 ----------------
    private void DoDraw(int count, bool free = false)
    {
        var rng = new GameRandom(GameRandom.NewSeed());
        List<DrawItem> items;
        if (_pool == "market")
        {
            if (!free)
            {
                int price = count == 10 ? ConfigDb.I.C.MarketPrice10 : ConfigDb.I.C.MarketPrice1;
                if (!GameState.TrySpendCoins(price))
                {
                    UiKit.Toast(GameState.D.Coins < ConfigDb.I.C.MarketPrice1 ? "金币不足，去普通模式赚金币吧" : "还差一点金币");
                    return;
                }
            }
            items = GachaSystem.DrawMarket(rng, count);
        }
        else
        {
            // 兵营池：十连优先玉虎符
            if (count == 10)
            {
                if (GameState.D.Jade >= 1) GameState.TrySpendTali("jade", 1);
                else if (!GameState.TrySpendTali("copper", 10)) { UiKit.Toast("虎符不足（10 铜 或 1 玉）"); return; }
            }
            else
            {
                if (!GameState.TrySpendTali("copper", 1)) { UiKit.Toast("铜虎符不足，去七星坛或市场池转转"); return; }
            }
            items = GachaSystem.DrawBarrack(rng, count);
        }

        // 记录新解锁
        var newDefs = new HashSet<string>();
        foreach (var it in items)
            if (it.Type == "hero" && !GameState.D.Codex.Contains(it.HeroDefId))
                newDefs.Add(it.HeroDefId);

        var heroes = GachaSystem.ApplyToInventory(items);
        foreach (var it in items)
        {
            GachaSystem.PushHistory($"{it.DisplayName}{(string.IsNullOrEmpty(it.SubLabel) ? "" : " · " + it.SubLabel)}");
        }
        SaveSystem.Save();

        ShowResult(items, newDefs);
        Refresh();

        // 首次金色/高稀有度震屏（简化：金卡到位时轻震）
        if (items.Any(i => i.Rarity >= 3))
        {
            var cam = Shell.Instance;
            if (cam != null) Fx.Shake(cam, 3f, 1);
        }
    }

    private void ShowResult(List<DrawItem> items, HashSet<string> newDefs)
    {
        var view = new GachaResultView(items, newDefs);
        view.CustomMinimumSize = new Vector2(320, 0);
        UiKit.ShowPopup("抽卡结果", view, false, 344);
    }

    // ---------------- 调试入口（截图/自测） ----------------
    public void DebugShowRates() => ShowRatesPopup();

    public void DebugTenDraw() => DoDraw(10);

    // ---------------- 概率公示（合规） ----------------
    private void ShowRatesPopup()
    {
        var v = UiKit.VBox(6);
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 380), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var list = UiKit.VBox(4);
        list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(list);
        v.AddChild(scroll);

        void AddSection(string title, IEnumerable<(string, string)> rows)
        {
            list.AddChild(UiKit.L(title, 13, Palette.Ink, true));
            foreach (var (k, val) in rows)
            {
                var h = UiKit.HBox(6);
                h.AddChild(UiKit.L(k, 12, Palette.InkSoft));
                var sp = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
                h.AddChild(sp);
                h.AddChild(UiKit.L(val, 12, Palette.StoneBlue, true));
                list.AddChild(h);
            }
            list.AddChild(ItemUi.HRule(0.1f));
        }

        AddSection("市场池 · 单抽 88 金币", new (string, string)[]
        {
            ("强化材料大类 62%", "绿 58% / 蓝 26% / 紫 13% / 金 3%"),
            ("兵粮大类 30%", "干粮 45% / 军粮 40% / 粮草 15%"),
            ("虎符大类 8%", "铜虎符 92% / 玉虎符 8%"),
        });
        AddSection("兵营池 · 1 铜虎符 / 抽", new (string, string)[]
        {
            ("1 星 92%", "绿 70% / 蓝 24% / 紫 5.5% / 金 0.5%"),
            ("2 星 8%", "蓝 70% / 紫 25% / 金 5%"),
            ("综合", "绿 64.4% / 蓝 27.68% / 紫 7.06% / 金 0.86%"),
        });
        list.AddChild(UiKit.L("抽卡结果独立随机；2 星卡直接以 2 星形态入库。", 11, Palette.Gray));
        UiKit.ShowPopup("概率公示", v, true, 344);
    }

    private void ShowHistoryPopup()
    {
        var v = UiKit.VBox(4);
        if (GameState.D.GachaHistory.Count == 0)
        {
            v.AddChild(UiKit.L("暂无记录", 12, Palette.Gray));
        }
        else
        {
            foreach (var line in GameState.D.GachaHistory)
                v.AddChild(UiKit.L("· " + line, 12, Palette.InkSoft));
        }
        UiKit.ShowPopup("最近 20 条产出", v, true, 320);
    }
}

/// <summary>带货币图标的次按钮（文字+图标混排，内容整体居中）。</summary>
public partial class IconTextButton : Control
{
    private readonly Button _btn;
    private readonly HBoxContainer _row;

    public IconTextButton(float height, int fontSize, Action onTap)
    {
        CustomMinimumSize = new Vector2(0, height);
        _btn = UiKit.BtnSecondary("", onTap, height, fontSize);
        _btn.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_btn);
        var cc = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        cc.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(cc);
        _row = UiKit.HBox(3);
        _row.MouseFilter = MouseFilterEnum.Ignore;
        cc.AddChild(_row);
    }

    public void SetParts((string text, Texture2D icon)[] parts)
    {
        _row.ClearChildren();
        foreach (var (text, icon) in parts)
        {
            if (!string.IsNullOrEmpty(text))
            {
                var l = UiKit.L(text, 15, Palette.Ink, true);
                l.MouseFilter = MouseFilterEnum.Ignore;
                _row.AddChild(l);
            }
            if (icon != null)
            {
                var tr = new TextureRect
                {
                    Texture = icon,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    CustomMinimumSize = new Vector2(20, 20),
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                _row.AddChild(tr);
            }
        }
    }

    public void SetDisabled(bool d) => _btn.Disabled = d;
}

/// <summary>抽卡结果揭示视图：低稀有度 → 高稀有度逐张翻开，支持点击加速与一键全开。</summary>
public partial class GachaResultView : Control
{
    private readonly List<DrawItem> _items;
    private readonly HashSet<string> _newDefs;
    private readonly List<Cell> _cells = new();
    private readonly List<int> _order;
    private int _pi;
    private float _timer;
    private float _gap = 0.28f;
    private bool _done;
    private Label _status;
    private Button _allBtn;

    private sealed class Cell
    {
        public DrawItem Item;
        public PanelContainer Panel;
        public Control Back;
        public VBoxContainer Face;
        public bool Revealed;
    }

    public GachaResultView(List<DrawItem> items, HashSet<string> newDefs)
    {
        _items = items;
        _newDefs = newDefs;
        // 揭示顺序：稀有度从低到高（金色压轴）
        _order = Enumerable.Range(0, items.Count)
            .OrderBy(i => items[i].Rarity)
            .ThenBy(i => i)
            .ToList();
    }

    public override void _Ready()
    {
        var v = UiKit.VBox(10);
        v.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(v);

        _status = UiKit.L("逐张翻开中…（点击加速 / 一键全开）", 11, Palette.Gray);
        _status.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(_status);

        var grid = new GridContainer { Columns = _items.Count > 1 ? 5 : 1 };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 6);
        v.AddChild(grid);

        float size = _items.Count > 1 ? 58 : 140;
        foreach (var item in _items)
        {
            var cell = new Cell { Item = item };
            var holder = new Control { CustomMinimumSize = new Vector2(size, size + 16), MouseFilter = MouseFilterEnum.Ignore };
            cell.Panel = UiKit.Card(10, Palette.CardBg, new Color(0, 0, 0, 0.15f), 2, 3);
            cell.Panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            cell.Panel.MouseFilter = MouseFilterEnum.Ignore;
            holder.AddChild(cell.Panel);

            // 背面（墨牌）
            cell.Back = new Panel { MouseFilter = MouseFilterEnum.Ignore };
            cell.Back.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            cell.Back.AddThemeStyleboxOverride("panel", UiKit.SB(Palette.Ink, 10));
            var seal = UiKit.L("令", (int)(size * 0.3f), new Color(1, 1, 1, 0.6f), true);
            seal.HorizontalAlignment = HorizontalAlignment.Center;
            seal.VerticalAlignment = VerticalAlignment.Center;
            seal.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            seal.MouseFilter = MouseFilterEnum.Ignore;
            cell.Back.AddChild(seal);
            holder.AddChild(cell.Back);

            // 正面
            cell.Face = UiKit.VBox(2);
            cell.Face.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            cell.Face.MouseFilter = MouseFilterEnum.Ignore;
            var tex = Art.DrawIcon(item);
            if (tex != null)
            {
                var tr = new TextureRect { Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore };
                tr.CustomMinimumSize = new Vector2(0, size - 14);
                cell.Face.AddChild(tr);
            }
            var nm = UiKit.L(item.DisplayName, size > 80 ? 14 : 10, Palette.Ink, true);
            nm.HorizontalAlignment = HorizontalAlignment.Center;
            cell.Face.AddChild(nm);
            var sub = UiKit.L(item.SubLabel + (_newDefs.Contains(item.HeroDefId) ? " NEW" : ""), 9, Palette.Cinnabar);
            sub.HorizontalAlignment = HorizontalAlignment.Center;
            cell.Face.AddChild(sub);
            cell.Face.Visible = false;
            holder.AddChild(cell.Face);

            grid.AddChild(holder);
            _cells.Add(cell);
        }

        v.AddChild(UiKit.Spacer(0, 2));
        var btnRow = UiKit.HBox(8);
        btnRow.Alignment = BoxContainer.AlignmentMode.Center;
        _allBtn = UiKit.BtnSecondary("一键全开", RevealAll, 42, 14);
        _allBtn.CustomMinimumSize = new Vector2(130, 42);
        btnRow.AddChild(_allBtn);
        var ok = UiKit.BtnPrimary("收 下", () => Popup.Current?.Close(), 42, 14);
        ok.CustomMinimumSize = new Vector2(130, 42);
        btnRow.AddChild(ok);
        v.AddChild(btnRow);

        SetProcess(true);
        GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { Pressed: true }) _gap = 0.06f;
        };
    }

    public override void _Process(double delta)
    {
        if (_done) return;
        _timer -= (float)delta;
        if (_timer > 0) return;
        if (_pi < _order.Count)
        {
            RevealOne(_order[_pi]);
            _pi++;
            _timer = _gap;
        }
        else
        {
            _done = true;
            _status.Text = "全部揭晓！";
        }
    }

    private void RevealOne(int idx)
    {
        var cell = _cells[idx];
        if (cell.Revealed) return;
        cell.Revealed = true;
        cell.Back.Visible = false;
        cell.Face.Visible = true;
        int tier = cell.Item.Rarity;
        var col = tier switch { 0 => Quality.Color("green"), 1 => Quality.Color("blue"), 2 => Quality.Color("purple"), _ => Quality.Color("gold") };
        cell.Panel.AddThemeStyleboxOverride("panel", UiKit.SB(Palette.CardBg, 10, col, tier >= 2 ? 3 : 2));
        var t = cell.Face.CreateTween();
        cell.Face.PivotOffset = cell.Face.Size / 2;
        cell.Face.Scale = new Vector2(0.6f, 0.6f);
        cell.Face.Modulate = new Color(1, 1, 1, 0.4f);
        t.SetParallel(true);
        t.TweenProperty(cell.Face, "scale", new Vector2(1.1f, 1.1f), 0.18f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        t.TweenProperty(cell.Face, "modulate:a", 1.0f, 0.15f);
        t.Chain().TweenProperty(cell.Face, "scale", Vector2.One, 0.1f);
    }

    private void RevealAll()
    {
        foreach (var idx in _order) RevealOne(idx);
        _pi = _order.Count;
        _done = true;
        _status.Text = "全部揭晓！";
    }
}
