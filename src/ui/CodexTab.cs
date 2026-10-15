using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using SanguoBasketball.Core;
using SanguoBasketball.Systems;

namespace SanguoBasketball.UI;

/// <summary>SCR_CODEX · 武将图鉴：拥有过即解锁；三行筛选（派系/品质/星级）。</summary>
public partial class CodexTab : Control
{
    private string _faction = "全部";
    private string _quality = "全部";

    private VBoxContainer _grid;
    private VBoxContainer _filtersBox;
    private Label _progress;

    public override void _Ready()
    {
        Build();
        Refresh();
    }

    private void Build()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var margin = UiKit.Pad(16, 48, 16, 8);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(margin);
        var root = UiKit.VBox(6);
        margin.AddChild(root);

        var head = UiKit.HBox(8);
        head.AddChild(UiKit.L("武将图鉴", 18, Palette.Ink, true));
        var sp = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        head.AddChild(sp);
        _progress = UiKit.L("", 13, Palette.StoneBlue, true);
        head.AddChild(_progress);
        root.AddChild(head);

        _filtersBox = UiKit.VBox(3);
        root.AddChild(_filtersBox);

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        root.AddChild(scroll);
        _grid = UiKit.VBox(8);
        _grid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_grid);
    }

    private void RebuildFilters()
    {
        _filtersBox.ClearChildren();
        _filtersBox.AddChild(UiKit.FilterRow("派系", new[] { "全部", "蜀", "魏", "吴", "群" },
            () => _faction, v => { _faction = v; Refresh(); }));
        _filtersBox.AddChild(UiKit.FilterRow("品质", new[] { "全部", "绿", "蓝", "紫", "金" },
            () => _quality, v => { _quality = v; Refresh(); }));
    }

    private void Refresh()
    {
        int total = ConfigDb.I.HeroList.Count;
        int unlocked = GameState.D.Codex.Count;
        _progress.Text = $"进度 {unlocked}/{total}";

        RebuildFilters();
        _grid.ClearChildren();
        var filtered = ConfigDb.I.HeroList.Where(h =>
        {
            if (_faction != "全部" && h.Faction != _faction) return false;
            if (_quality != "全部" && Quality.Cn(h.QualityKey) != _quality) return false;
            return true;
        }).ToList();

        var grid = new GridContainer { Columns = 4 };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 10);
        _grid.AddChild(grid);

        int i = 0;
        foreach (var def in filtered)
        {
            bool owned = GameState.D.Codex.Contains(def.Id);
            var holder = new Control { CustomMinimumSize = new Vector2(76, 100), MouseFilter = MouseFilterEnum.Stop };
            var card = new HeroCard(72);
            if (owned)
            {
                // v2：图鉴卡不显示星级
                card.SetupRaw(def.Id, def.Name, def.QualityKey, 0, null);
            }
            else
            {
                card.SetupSilhouette("?", def.QualityKey, 0, null);
            }
            card.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
            card.OffsetLeft = -36; card.OffsetRight = 36;
            card.MouseFilter = MouseFilterEnum.Ignore;
            holder.AddChild(card);
            HeroDef d = def;
            holder.GuiInput += ev =>
            {
                if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                {
                    if (!GameState.D.Codex.Contains(d.Id)) { UiKit.Toast("尚未结识 · 去兵营抽卡"); return; }
                    UiKit.PushScreen(new CodexHeroDetailScreen(d));
                }
            };
            grid.AddChild(holder);
            Fx.CardEnter(holder, Math.Min(i, 10) * 0.04f);
            i++;
        }
        if (filtered.Count == 0)
            _grid.AddChild(UiKit.L("没有符合条件的武将", 12, Palette.Gray));
    }
}
