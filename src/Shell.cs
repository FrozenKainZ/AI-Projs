using Godot;
using SanguoBasketball.UI;

namespace SanguoBasketball;

/// <summary>
/// 应用根节点（Shell）。负责：
/// - 启动引导（读取配置/存档）
/// - 屏幕栈管理（页面转场 FX-02）
/// - 弹窗层与 Toast 层
/// - 全局命令行开关（--selftest / --shots）
/// </summary>
public partial class Shell : Control
{
    public static Shell Instance { get; private set; }

    public override void _Ready()
    {
        Instance = this;
        Name = "Shell";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Boot.Run();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest || what == NotificationPredelete)
        {
            Core.SaveSystem.SaveOnExit();
        }
    }
}
