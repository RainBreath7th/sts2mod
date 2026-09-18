using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.addons.mega_text;

namespace HextechRunes;

internal sealed partial class HextechRuneSelectionScreen : Control, IOverlayScreen, IScreenContext
{
	private void BuildUi()
	{
		ColorRect backdrop = new()
		{
			Name = "DimOverlay",
			Color = new Color(0.02f, 0.025f, 0.035f, 0.56f),
			MouseFilter = MouseFilterEnum.Stop
		};
		backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(backdrop);

		CenterContainer screenCenter = new()
		{
			Name = "ScreenCenter",
			MouseFilter = MouseFilterEnum.Ignore
		};
		screenCenter.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(screenCenter);

		PanelContainer contentPanel = new()
		{
			Name = "ContentPanel",
			CustomMinimumSize = new Vector2(1180f, _enemyOnly ? 460f : 780f),
			MouseFilter = MouseFilterEnum.Ignore
		};
		contentPanel.AddThemeStyleboxOverride("panel", CreateContentPanelStyle());
		screenCenter.AddChild(contentPanel);

		MarginContainer contentMargin = new()
		{
			MouseFilter = MouseFilterEnum.Ignore
		};
		contentMargin.AddThemeConstantOverride("margin_left", 30);
		contentMargin.AddThemeConstantOverride("margin_right", 30);
		contentMargin.AddThemeConstantOverride("margin_top", 28);
		contentMargin.AddThemeConstantOverride("margin_bottom", 28);
		contentPanel.AddChild(contentMargin);

		VBoxContainer root = new()
		{
			MouseFilter = MouseFilterEnum.Ignore,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			Alignment = BoxContainer.AlignmentMode.Center
		};
		root.AddThemeConstantOverride("separation", 20);
		contentMargin.AddChild(root);

		MegaLabel title = new()
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MaxFontSize = 48,
			MinFontSize = 30
		};
		HextechUiTheme.ApplyDefaultMegaLabelTheme(title);
		title.Modulate = new Color(0.96f, 0.97f, 0.99f, 0.98f);
		title.SetTextAutoSize(_titleOverride ?? new LocString(LocTable, "HEXTECH_SELECTION_TITLE").GetRawText());
		root.AddChild(title);

		if (_monsterHexKinds.Count > 0 || _enemyHexControlsEnabled)
		{
			_enemyPreviewHost = new VBoxContainer()
			{
				Name = "EnemyPreviewHost",
				MouseFilter = MouseFilterEnum.Ignore,
				SizeFlagsHorizontal = SizeFlags.ExpandFill
			};
			root.AddChild(_enemyPreviewHost);
			RebuildEnemyPreview();
		}

		HBoxContainer row = new()
		{
			Name = "PlayerCardsRow",
			Alignment = BoxContainer.AlignmentMode.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore
		};
		row.AddThemeConstantOverride("separation", 28);
		root.AddChild(row);
		_cardsRow = row;
		row.Visible = !_enemyOnly;

		RebuildCards();
		if (_enemyOnly)
		{
			_enemyOnlyConfirm = new Button
			{
				Text = new LocString(LocTable, "HEXTECH_ENEMY_CONFIRM").GetRawText(),
				CustomMinimumSize = new Vector2(220f, 58f),
				SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
				FocusMode = FocusModeEnum.All,
				Disabled = !_enemyHexControlsEnabled,
				MouseDefaultCursorShape = CursorShape.PointingHand
			};
			_enemyOnlyConfirm.AddThemeFontSizeOverride("font_size", 26);
			_enemyOnlyConfirm.Pressed += () =>
			{
				if (_enemyHexControlsEnabled && !IsSelectionConfirmGuardActive())
				{
					GetViewport()?.SetInputAsHandled();
					CompleteEnemyOnlySelection();
				}
			};
			root.AddChild(_enemyOnlyConfirm);
		}
		else if (ShouldUsePlayerRuneConfirmation(_metadataMode, _enemyOnly))
		{
			HBoxContainer selectionActions = new()
			{
				Name = "PlayerRuneSelectionActions",
				Alignment = BoxContainer.AlignmentMode.Center,
				SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
				MouseFilter = MouseFilterEnum.Ignore
			};
			selectionActions.AddThemeConstantOverride("separation", 16);

			_playerRuneConfirm = new Button
			{
				Name = "PlayerRuneConfirm",
				Text = new LocString(LocTable, "HEXTECH_ENEMY_CONFIRM").GetRawText(),
				CustomMinimumSize = new Vector2(220f, 58f),
				FocusMode = FocusModeEnum.All,
				Disabled = true,
				MouseDefaultCursorShape = CursorShape.PointingHand
			};
			ApplySelectionActionButtonStyle(_playerRuneConfirm, confirm: true);
			_playerRuneConfirm.AddThemeFontSizeOverride("font_size", 26);
			_playerRuneConfirm.Pressed += OnPlayerRuneConfirmPressed;
			selectionActions.AddChild(_playerRuneConfirm);

			_playerRuneCancel = new Button
			{
				Name = "PlayerRuneCancel",
				Text = new LocString(LocTable, "HEXTECH_CONFIG_CANCEL").GetRawText(),
				CustomMinimumSize = new Vector2(220f, 58f),
				FocusMode = FocusModeEnum.All,
				Disabled = true,
				MouseDefaultCursorShape = CursorShape.PointingHand
			};
			ApplySelectionActionButtonStyle(_playerRuneCancel, confirm: false);
			_playerRuneCancel.AddThemeFontSizeOverride("font_size", 26);
			_playerRuneCancel.Pressed += OnPlayerRuneCancelPressed;
			selectionActions.AddChild(_playerRuneCancel);
			root.AddChild(selectionActions);
		}

		_statusLabel = new MegaLabel()
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MaxFontSize = 22,
			MinFontSize = 16,
			Visible = false
		};
		HextechUiTheme.ApplyDefaultMegaLabelTheme(_statusLabel);
		_statusLabel.Modulate = new Color(0.88f, 0.92f, 0.97f, 0.82f);
		root.AddChild(_statusLabel);
		if (_enemyOnly && !_enemyHexControlsEnabled) ShowWaitingForRemotePlayers();
	}

	private void RebuildEnemyPreview()
	{
		if (_enemyPreviewHost == null)
		{
			return;
		}

		foreach (Node child in _enemyPreviewHost.GetChildren())
		{
			_enemyPreviewHost.RemoveChild(child);
			child.QueueFree();
		}

		_enemyHexRerollButtons.Clear();
		_enemyHexRemoveButtons.Clear();
		_enemyPreviewHost.AddChild(CreateEnemyPreview());
		ConfigureControllerNavigation();
	}

	private void RebuildCards()
	{
		if (_cardsRow == null)
		{
			return;
		}

		foreach (Node child in _cardsRow.GetChildren())
		{
			_cardsRow.RemoveChild(child);
			child.QueueFree();
		}

		_holders.Clear();
		_pendingSelectionOutlines.Clear();
		_rerollButtons.Clear();
		_goldenRerollVisuals.Clear();
		while (_playerRuneRerollCounts.Count < _relics.Count)
		{
			_playerRuneRerollCounts.Add(0);
		}
		if (_pendingPlayerRuneSlot is int pendingSlot
			&& (pendingSlot < 0 || pendingSlot >= _relics.Count))
		{
			_pendingPlayerRuneSlot = null;
		}

		for (int i = 0; i < _relics.Count; i++)
		{
			Control slot = CreateCardSlot(_relics[i], i);
			_cardsRow.AddChild(slot);
		}

		ConfigureControllerNavigation();
		UpdatePlayerRuneActionButtons();
	}

}
