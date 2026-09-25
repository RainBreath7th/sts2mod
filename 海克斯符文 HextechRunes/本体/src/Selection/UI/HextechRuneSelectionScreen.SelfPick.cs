using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.addons.mega_text;

namespace HextechRunes;

/// <summary>
/// 自选模式:玩家海克斯重随次数设为无限时,不再展示三张候选卡与重随按钮,改为列出本次稀有度的全部合法海克斯,
/// 玩家点选后确认。合法池由协调器按重随同一套规则构造(配置启用、版本可用、本幕允许、角色可用、已拥有与互斥排除)。
/// 确认后当前候选替换为玩家所选的这一个,沿用原有的"最终候选 + 选中序号"同步协议,远端按 ID 还原,联机格式不变。
/// </summary>
internal sealed partial class HextechRuneSelectionScreen : Control, IOverlayScreen, IScreenContext
{
	private const float SelfPickIconSize = 76f;

	private readonly IReadOnlyList<RelicModel>? _selfPickPool;
	private readonly List<Button> _selfPickButtons = new();
	private Button? _selfPickConfirm;
	private MegaLabel? _selfPickConfirmLabel;
	private Button? _selfPickSelectedButton;
	private RelicModel? _selfPickSelected;

	private bool SelfPickMode => !_enemyOnly && _selfPickPool is { Count: > 0 };

	private Control CreateSelfPickPanel()
	{
		VBoxContainer panel = new()
		{
			Name = "SelfPickPanel",
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill
		};
		panel.AddThemeConstantOverride("separation", 14);

		MegaLabel hint = new()
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MaxFontSize = 22,
			MinFontSize = 16
		};
		HextechUiTheme.ApplyDefaultMegaLabelTheme(hint);
		hint.Modulate = new Color(0.88f, 0.92f, 0.97f, 0.86f);
		hint.SetTextAutoSize(new LocString(LocTable, "HEXTECH_SELF_PICK_HINT").GetRawText());
		panel.AddChild(hint);

		ScrollContainer scroll = new()
		{
			Name = "SelfPickScroll",
			CustomMinimumSize = new Vector2(1080f, 340f),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
			MouseFilter = MouseFilterEnum.Stop
		};
		panel.AddChild(scroll);

		HFlowContainer grid = new()
		{
			Name = "SelfPickGrid",
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			Alignment = FlowContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore
		};
		grid.AddThemeConstantOverride("h_separation", 12);
		grid.AddThemeConstantOverride("v_separation", 12);
		scroll.AddChild(grid);

		_selfPickButtons.Clear();
		foreach (RelicModel relic in _selfPickPool!)
		{
			Button button = CreateSelfPickIconButton(relic);
			button.FocusEntered += () => scroll.EnsureControlVisible(button);
			grid.AddChild(button);
			_selfPickButtons.Add(button);
		}

		_selfPickConfirm = CreateConfirmButton("SelfPickConfirm", new Vector2(380f, 60f), out _selfPickConfirmLabel);
		_selfPickConfirmLabel.SetTextAutoSize(new LocString(LocTable, "HEXTECH_SELF_PICK_CONFIRM_EMPTY").GetRawText());
		_selfPickConfirm.Disabled = true;
		_selfPickConfirm.Pressed += ConfirmSelfPick;
		panel.AddChild(_selfPickConfirm);
		return panel;
	}

	private Button CreateSelfPickIconButton(RelicModel relic)
	{
		Button button = new()
		{
			Name = $"SelfPick_{(relic.CanonicalInstance?.Id ?? relic.Id).Entry}",
			CustomMinimumSize = new Vector2(SelfPickIconSize + 16f, SelfPickIconSize + 16f),
			FocusMode = FocusModeEnum.All,
			ToggleMode = false
		};
		ApplySelfPickButtonStyle(button, selected: false);
		button.AddThemeStyleboxOverride("focus", HextechControllerInput.CreateFocusRing(10));

		// 按钮不是容器,用铺满按钮的 CenterContainer 把固定尺寸的图标居中。
		CenterContainer iconCenter = new()
		{
			MouseFilter = MouseFilterEnum.Ignore
		};
		iconCenter.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		iconCenter.AddChild(CreateRelicTexture(relic, SelfPickIconSize));
		button.AddChild(iconCenter);

		AttachRelicHoverTips(button, relic);
		button.MouseDefaultCursorShape = CursorShape.PointingHand;
		button.Pressed += () => SelectSelfPick(button, relic);
		return button;
	}

	private static void ApplySelfPickButtonStyle(Button button, bool selected)
	{
		Color background = selected ? new Color(0.2f, 0.17f, 0.08f, 0.95f) : new Color(0.08f, 0.1f, 0.14f, 0.8f);
		Color border = selected ? new Color(0.98f, 0.8f, 0.4f, 1f) : new Color(0.46f, 0.55f, 0.68f, 0.55f);
		button.AddThemeStyleboxOverride("normal", CreateSelfPickStyle(background, border, selected ? 3 : 1));
		button.AddThemeStyleboxOverride("hover", CreateSelfPickStyle(background.Lightened(0.08f), new Color(0.98f, 0.8f, 0.4f, 0.9f), selected ? 3 : 2));
		button.AddThemeStyleboxOverride("pressed", CreateSelfPickStyle(background.Darkened(0.1f), new Color(0.98f, 0.8f, 0.4f, 1f), 3));
		button.AddThemeStyleboxOverride("disabled", CreateSelfPickStyle(background.Darkened(0.3f), border.Darkened(0.4f), 1));
	}

	private static StyleBoxFlat CreateSelfPickStyle(Color background, Color border, int borderWidth)
	{
		StyleBoxFlat style = new()
		{
			BgColor = background,
			BorderColor = border
		};
		style.SetBorderWidthAll(borderWidth);
		style.SetCornerRadiusAll(10);
		return style;
	}

	private void SelectSelfPick(Button button, RelicModel relic)
	{
		if (_choiceLocked || IsSelectionConfirmGuardActive())
		{
			return;
		}

		if (_selfPickSelectedButton != null && GodotObject.IsInstanceValid(_selfPickSelectedButton))
		{
			ApplySelfPickButtonStyle(_selfPickSelectedButton, selected: false);
		}

		_selfPickSelectedButton = button;
		_selfPickSelected = relic;
		ApplySelfPickButtonStyle(button, selected: true);
		if (_selfPickConfirm != null)
		{
			LocString confirm = new(LocTable, "HEXTECH_SELF_PICK_CONFIRM");
			confirm.Add("Rune", relic.Title.GetFormattedText());
			_selfPickConfirmLabel?.SetTextAutoSize(confirm.GetFormattedText());
			_selfPickConfirm.Disabled = false;
		}
	}

	private void ConfirmSelfPick()
	{
		if (_choiceLocked || _selfPickSelected == null || IsSelectionConfirmGuardActive())
		{
			return;
		}

		RelicModel chosen = _selfPickSelected;
		// 最终候选只剩玩家所选的这一个;原列表带角色权重就沿用同一个权重,不带就不补,避免改写存档里的权重。
		_relics = _relics is HextechWeightedRuneOptions weighted
			? new HextechWeightedRuneOptions([chosen], weighted.CharacterWeightPercent)
			: [chosen];
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] SelectionScreen.ConfirmSelfPick: relic={(chosen.CanonicalInstance?.Id ?? chosen.Id).Entry} poolSize={_selfPickPool?.Count ?? 0}");
		OnHolderSelected(chosen);
	}

	private void LockSelfPickControls()
	{
		foreach (Button button in _selfPickButtons)
		{
			button.Disabled = true;
		}

		if (_selfPickConfirm != null)
		{
			_selfPickConfirm.Disabled = true;
		}
	}
}
