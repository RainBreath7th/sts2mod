using Godot;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace HextechRunes;

/// <summary>
/// 配置菜单覆盖层。它挂在场景根上、不属于原版 ScreenContext,所以手柄相关的几件事要自己做:
/// 进入方向导航时把焦点放进来、打开期间屏蔽背后主菜单的焦点、B 逐层关闭子弹窗、LB/RB 切页签。
/// 鼠标玩家打开时不抢焦点,避免出现焦点框。
/// </summary>
internal sealed partial class HextechControllerOverlay : Control
{
	private readonly List<ModalEntry> _modals = [];
	private Control? _blockedHost;
	private FocusBehaviorRecursiveEnum _blockedHostPreviousBehavior;
	private NControllerManager? _subscribedManager;

	public Action? CancelRequested { get; set; }

	public Control? InitialFocus { get; set; }

	/// <summary>按偏移切换页签(-1 = 上一个,+1 = 下一个)。</summary>
	public Action<int>? CycleTab { get; set; }

	public override void _EnterTree()
	{
		base._EnterTree();
		BlockHostFocus();
		_subscribedManager = NControllerManager.Instance;
		if (_subscribedManager != null)
		{
			_subscribedManager.ControllerDetected += OnControllerDetected;
		}

		if (HextechControllerInput.IsDirectionalNavigation)
		{
			Callable.From(FocusInitialIfOutside).CallDeferred();
		}
	}

	public override void _ExitTree()
	{
		if (_subscribedManager != null && GodotObject.IsInstanceValid(_subscribedManager))
		{
			_subscribedManager.ControllerDetected -= OnControllerDetected;
		}

		_subscribedManager = null;
		ReleaseHostFocusBlock();
		base._ExitTree();
	}

	public override void _Input(InputEvent inputEvent)
	{
		if (IsVisibleInTree())
		{
			HextechControllerInput.TryTranslateSelectToAccept(this, inputEvent);
		}
	}

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (!IsVisibleInTree() || inputEvent.IsEcho())
		{
			return;
		}

		if (inputEvent.IsActionPressed(MegaInput.cancel))
		{
			GetViewport()?.SetInputAsHandled();
			if (TryCloseTopModal())
			{
				return;
			}

			CancelRequested?.Invoke();
			return;
		}

		if (_modals.Count > 0 || CycleTab == null)
		{
			return;
		}

		if (inputEvent.IsActionPressed(MegaInput.viewDeckAndTabLeft))
		{
			GetViewport()?.SetInputAsHandled();
			CycleTab(-1);
		}
		else if (inputEvent.IsActionPressed(MegaInput.viewExhaustPileAndTabRight))
		{
			GetViewport()?.SetInputAsHandled();
			CycleTab(1);
		}
	}

	/// <summary>
	/// 登记覆盖层内的子弹窗(社区配置、上传对话框):打开期间同级内容不可聚焦,B 先关它;
	/// 关闭后焦点回到打开前的位置。子弹窗自己的关闭按钮照常 QueueFree 即可。
	/// </summary>
	internal static void RegisterModal(Control modal, Control? initialFocus)
	{
		HextechControllerOverlay? overlay = FindOverlay(modal);
		overlay?.PushModal(modal, initialFocus);
	}

	/// <summary>关闭动画开始时调用:先恢复主菜单的可聚焦性,焦点才能还给打开菜单的按钮。</summary>
	internal void ReleaseHostFocusBlock()
	{
		if (_blockedHost != null && GodotObject.IsInstanceValid(_blockedHost))
		{
			_blockedHost.FocusBehaviorRecursive = _blockedHostPreviousBehavior;
		}

		_blockedHost = null;
	}

	private void PushModal(Control modal, Control? initialFocus)
	{
		Node? parent = modal.GetParent();
		List<(Control Control, FocusBehaviorRecursiveEnum Previous)> blocked = [];
		if (parent != null)
		{
			foreach (Node sibling in parent.GetChildren())
			{
				if (sibling is Control control && control != modal)
				{
					blocked.Add((control, control.FocusBehaviorRecursive));
					control.FocusBehaviorRecursive = FocusBehaviorRecursiveEnum.Disabled;
				}
			}
		}

		modal.FocusBehaviorRecursive = FocusBehaviorRecursiveEnum.Enabled;
		ModalEntry entry = new(modal, blocked, GetViewport()?.GuiGetFocusOwner());
		_modals.Add(entry);
		modal.TreeExiting += () => PopModal(entry);
		if (HextechControllerInput.IsDirectionalNavigation && initialFocus != null)
		{
			Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(initialFocus) && initialFocus.IsVisibleInTree())
				{
					initialFocus.GrabFocus();
				}
			}).CallDeferred();
		}
	}

	private void PopModal(ModalEntry entry)
	{
		if (!_modals.Remove(entry))
		{
			return;
		}

		foreach ((Control control, FocusBehaviorRecursiveEnum previous) in entry.Blocked)
		{
			if (GodotObject.IsInstanceValid(control))
			{
				control.FocusBehaviorRecursive = previous;
			}
		}

		Control? restore = entry.PreviousFocus;
		if (HextechControllerInput.IsDirectionalNavigation)
		{
			Callable.From(() =>
			{
				if (restore != null && GodotObject.IsInstanceValid(restore) && restore.IsVisibleInTree())
				{
					restore.GrabFocus();
				}
				else
				{
					FocusInitialIfOutside();
				}
			}).CallDeferred();
		}
	}

	private bool TryCloseTopModal()
	{
		for (int i = _modals.Count - 1; i >= 0; i--)
		{
			Control modal = _modals[i].Modal;
			if (GodotObject.IsInstanceValid(modal) && modal.IsInsideTree() && !modal.IsQueuedForDeletion())
			{
				modal.QueueFree();
				return true;
			}

			_modals.RemoveAt(i);
		}

		return false;
	}

	private void OnControllerDetected()
	{
		// 原版切进手柄模式时会先把焦点交给 ScreenContext 的默认控件(背后的主菜单),这里随后把焦点拿回覆盖层。
		Callable.From(FocusInitialIfOutside).CallDeferred();
	}

	private void FocusInitialIfOutside()
	{
		if (!IsInsideTree() || !IsVisibleInTree() || !HextechControllerInput.IsDirectionalNavigation)
		{
			return;
		}

		Control? focused = GetViewport()?.GuiGetFocusOwner();
		if (focused != null && IsAncestorOf(focused))
		{
			return;
		}

		Control? target = _modals.Count > 0 ? null : InitialFocus;
		if (target != null && GodotObject.IsInstanceValid(target) && target.IsVisibleInTree())
		{
			target.GrabFocus();
		}
	}

	// 覆盖层与原版节点平级挂在场景根上,Godot 的方向导航会跨过遮罩落到背后的主菜单按钮;打开期间关掉主菜单的可聚焦性。
	private void BlockHostFocus()
	{
		if (_blockedHost != null)
		{
			return;
		}

		Control? host = NGame.Instance?.MainMenu;
		if (host == null || !GodotObject.IsInstanceValid(host))
		{
			return;
		}

		_blockedHost = host;
		_blockedHostPreviousBehavior = host.FocusBehaviorRecursive;
		host.FocusBehaviorRecursive = FocusBehaviorRecursiveEnum.Disabled;
	}

	private static HextechControllerOverlay? FindOverlay(Node node)
	{
		for (Node? current = node; current != null; current = current.GetParent())
		{
			if (current is HextechControllerOverlay overlay)
			{
				return overlay;
			}
		}

		return null;
	}

	private sealed record ModalEntry(Control Modal, List<(Control Control, FocusBehaviorRecursiveEnum Previous)> Blocked, Control? PreviousFocus);
}

internal static class HextechControllerInput
{
	private static readonly StringName GodotAccept = "ui_accept";

	/// <summary>
	/// 游戏自己判定的输入模式:手柄或纯键盘时为方向导航。模组据此决定是否给默认焦点,鼠标玩家不会看到焦点框。
	/// 不能自己从事件类型猜:Steam Input 下手柄按键到达时已经是合成的动作事件,没有原始 JoypadButton。
	/// </summary>
#if STS2_110_OR_NEWER
	internal static bool IsDirectionalNavigation => NControllerManager.Instance?.IsUsingDirectionalNavigation == true;
#else
	// 0.107.1 只有手柄/鼠标两种模式,没有纯键盘方向导航。
	internal static bool IsDirectionalNavigation => NControllerManager.Instance?.IsUsingController == true;
#endif

	/// <summary>
	/// 原版把手柄确认键(A/×)映射为 <c>ui_select</c>,只有原版可点击控件认它;Godot 的 Button 与本模组
	/// 自绘控件只响应 <c>ui_accept</c>,而游戏里没有任何手柄键映射到 <c>ui_accept</c>。在 root 子树内有焦点时,
	/// 把 ui_select 的按下/松开转成 ui_accept 重新派发给焦点控件;原版可点击控件不转换,避免触发两次。
	/// </summary>
	internal static bool TryTranslateSelectToAccept(Control root, InputEvent inputEvent)
	{
		if (inputEvent.IsEcho())
		{
			return false;
		}

		bool pressed = inputEvent.IsActionPressed(MegaInput.select);
		if (!pressed && !inputEvent.IsActionReleased(MegaInput.select))
		{
			return false;
		}

		Viewport? viewport = root.GetViewport();
		Control? focus = viewport?.GuiGetFocusOwner();
		if (focus == null || focus is NClickableControl || (focus != root && !root.IsAncestorOf(focus)))
		{
			return false;
		}

		viewport!.SetInputAsHandled();
		// 延后派发:在本事件的传播过程中嵌套派发新事件会重置视口的"已处理"标记,让原事件继续漏给后面的节点。
		Callable.From(() => Input.ParseInputEvent(new InputEventAction { Action = GodotAccept, Pressed = pressed })).CallDeferred();
		return true;
	}

	/// <summary>手柄焦点框:只画描边,叠在按钮原有样式上。</summary>
	internal static StyleBoxFlat CreateFocusRing(int cornerRadius = 9)
	{
		StyleBoxFlat style = new()
		{
			DrawCenter = false,
			BorderColor = new Color(0.98f, 0.8f, 0.4f, 0.95f)
		};
		style.SetBorderWidthAll(2);
		style.SetCornerRadiusAll(cornerRadius);
		style.SetExpandMarginAll(2f);
		return style;
	}
}
