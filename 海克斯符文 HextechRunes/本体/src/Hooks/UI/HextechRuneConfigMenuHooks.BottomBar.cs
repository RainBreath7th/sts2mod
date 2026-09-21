using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

internal static partial class HextechRuneConfigMenuHooks
{
	private static Control CreateBottomBar(
		Control overlay,
		IReadOnlyList<RuneConfigEntry> playerEntries,
		IReadOnlyList<RuneConfigEntry> enemyEntries,
		IReadOnlyList<RuneConfigEntry> forgeEntries,
		PendingConfig pending,
		IReadOnlyList<NumericValueBinding> numericBindings,
		IReadOnlyList<BooleanValueBinding> booleanBindings,
		IReadOnlyList<RuneIconBinding> playerIconBindings,
		IReadOnlyList<RuneIconBinding> enemyIconBindings,
		IReadOnlyList<RuneIconBinding> forgeIconBindings,
		Label summary,
		Action updateSummary,
		Func<int> getPageIndex,
		bool compactLayout,
		Action?[] shareActions,
		out Action<int> updatePageActions)
	{
		VBoxContainer bar = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		bar.AddThemeConstantOverride("separation", compactLayout ? 6 : 9);

		ColorRect hairline = new()
		{
			Color = new Color(0.86f, 0.74f, 0.42f, 0.28f),
			CustomMinimumSize = new Vector2(0f, 1f),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		bar.AddChild(hairline);

		HBoxContainer row = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Pass
		};
		row.AddThemeConstantOverride("separation", compactLayout ? 7 : 12);

		Button enableAll = CreateActionButton(L("HEXTECH_CONFIG_ENABLE_ALL"), () =>
		{
			switch (getPageIndex())
			{
				case 1:
					pending.DisabledPlayerRuneIds.Clear();
					pending.DisabledMonsterHexIds.Clear();
					UpdateAllRuneIcons(playerIconBindings, pending.DisabledPlayerRuneIds);
					UpdateAllRuneIcons(enemyIconBindings, pending.DisabledMonsterHexIds);
					break;
				case 2:
					pending.DisabledForgeIds.Clear();
					UpdateAllRuneIcons(forgeIconBindings, pending.DisabledForgeIds);
					break;
			}

			updateSummary();
		}, compactLayout);
		Button disableAll = CreateActionButton(L("HEXTECH_CONFIG_DISABLE_ALL"), () =>
		{
			switch (getPageIndex())
			{
				case 1:
					ReplaceDisabledIds(pending.DisabledPlayerRuneIds, playerEntries);
					ReplaceDisabledIds(pending.DisabledMonsterHexIds, enemyEntries);
					UpdateAllRuneIcons(playerIconBindings, pending.DisabledPlayerRuneIds);
					UpdateAllRuneIcons(enemyIconBindings, pending.DisabledMonsterHexIds);
					break;
				case 2:
					ReplaceDisabledIds(pending.DisabledForgeIds, forgeEntries);
					UpdateAllRuneIcons(forgeIconBindings, pending.DisabledForgeIds);
					break;
			}

			updateSummary();
		}, compactLayout);
		Button reset = CreateActionButton(L("HEXTECH_CONFIG_RESET"), () =>
		{
			HextechRunConfigurationSnapshot defaults = HextechRuneConfiguration.GetDefaultSnapshot();
			switch (getPageIndex())
			{
				case 0:
					pending.LoadFrom(defaults, PendingFields.ActCounts);
					UpdateNumericLabels(numericBindings);
					break;
				case 1:
					pending.LoadFrom(defaults, PendingFields.RunePools);
					UpdateAllRuneIcons(playerIconBindings, pending.DisabledPlayerRuneIds);
					UpdateAllRuneIcons(enemyIconBindings, pending.DisabledMonsterHexIds);
					break;
				case 2:
					pending.LoadFrom(defaults, PendingFields.ForgePool);
					UpdateAllRuneIcons(forgeIconBindings, pending.DisabledForgeIds);
					break;
				case 3:
					// UI 偏好不在运行配置快照里,默认值单独从 HextechRelicVisibilityHooks 取。
					pending.LoadFrom(
						defaults,
						PendingFields.Details | PendingFields.ModEnabled | PendingFields.UiPreferences,
						HextechRelicVisibilityHooks.GetDefaultShowHiddenRelicsToggle(),
						HextechRelicVisibilityHooks.GetDefaultShowUpdateNotice(),
						HextechRelicVisibilityHooks.GetDefaultCollapseEnemyHexes());
					UpdateNumericLabels(numericBindings);
					UpdateBooleanToggles(booleanBindings);
					break;
			}

			updateSummary();
		}, compactLayout);

		Button save = CreateActionButton(L("HEXTECH_CONFIG_SAVE_CLOSE"), () =>
		{
			HextechRuneConfiguration.SaveSnapshot(pending.ToSnapshot());
			HextechRelicVisibilityHooks.SetShowHiddenRelicsToggle(pending.ShowHiddenRelicsToggle);
			HextechRelicVisibilityHooks.SetShowUpdateNotice(pending.ShowUpdateNotice);
			HextechRelicVisibilityHooks.SetCollapseEnemyHexes(pending.CollapseEnemyHexes);
			HextechUpdateChecker.ApplyNoticeVisibility(overlay);
			HextechCollectionHooks.RefreshOpenRelicCollections();
			string runeWeights = string.Join("/", pending.RuneWeightsByAct.Select(static weights => string.Join(",", weights)));
			HextechLog.Info($"[{ModInfo.Id}][RuneConfig] Saved run config: playerDisabled={pending.DisabledPlayerRuneIds.Count} enemyDisabled={pending.DisabledMonsterHexIds.Count} forgeDisabled={pending.DisabledForgeIds.Count} playerCounts={string.Join(",", pending.PlayerHexCounts)} enemyCounts={string.Join(",", pending.EnemyHexCounts)} playerRerolls={pending.PlayerRuneRerollLimit} monsterRerolls={pending.MonsterHexRerollLimit} runeWeightsByAct={runeWeights} preventConsecutiveSilver={pending.PreventConsecutiveSilverRunes} goldenRerollChance={pending.GoldenRerollChancePercent}% forgePrice={pending.ForgePrice} showHiddenUiToggle={pending.ShowHiddenRelicsToggle} showUpdateNotice={pending.ShowUpdateNotice} randomForgeDirect={pending.RandomForgeDirectGrant} modEnabled={pending.ModEnabled}");
			CloseOverlayAnimated(overlay);
		}, compactLayout);
		Button cancel = CreateActionButton(L("HEXTECH_CONFIG_CANCEL"), () => CloseWithoutSaving(overlay), compactLayout);

		// 配置分享码：导出=把当前编辑中的配置(pending 态)编码进剪贴板;导入=从剪贴板解析并填充
		// pending 态(界面即预览,可继续修改,「取消」可放弃)——真正落盘仍走「保存并关闭」。
		// 按钮本体放在「杂项」页的分享区(CreateShareSection),这里只填充延迟绑定的动作。
		Func<string> buildPendingCode = () => HextechConfigShareCodec.Export(pending.ToSnapshot());
		shareActions[0] = () =>
		{
			string code = buildPendingCode();
			DisplayServer.ClipboardSet(code);
			updateSummary();
			summary.Text = L("HEXTECH_CONFIG_EXPORT_DONE");
		};
		// 把分享码/社区配置解析结果填充进 pending 编辑态并刷新全部控件(界面即预览,「取消」可放弃)。
		Action<HextechConfigShareCodec.ImportPreview> applyPreview = preview =>
		{
			// PendingFields.ShareCode 刻意不含 ModEnabled 与 UI 偏好(折叠/隐藏遗物开关等),它们不随导入改变。
			pending.LoadFrom(preview.Snapshot, PendingFields.ShareCode);
			UpdateNumericLabels(numericBindings);
			UpdateBooleanToggles(booleanBindings);
			UpdateAllRuneIcons(playerIconBindings, pending.DisabledPlayerRuneIds);
			UpdateAllRuneIcons(enemyIconBindings, pending.DisabledMonsterHexIds);
			UpdateAllRuneIcons(forgeIconBindings, pending.DisabledForgeIds);
			updateSummary();
			summary.Text = string.Format(L("HEXTECH_CONFIG_IMPORT_DONE"), preview.IgnoredUnknownCount);
		};

		shareActions[1] = () =>
		{
			HextechConfigShareCodec.ImportPreview? preview = HextechConfigShareCodec.TryParse(DisplayServer.ClipboardGet());
			if (preview == null)
			{
				updateSummary();
				summary.Text = L("HEXTECH_CONFIG_IMPORT_INVALID");
				return;
			}

			applyPreview(preview);
		};
		shareActions[2] = () => OpenCommunityConfigsPanel(overlay, applyPreview, buildPendingCode, compactLayout);

		// Summary lives on its own centered, wrapping line so its variable width never
		// drives the panel width. It always reserves a line of height to keep the panel
		// size stable across pages.
		summary.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		summary.HorizontalAlignment = HorizontalAlignment.Center;
		summary.VerticalAlignment = VerticalAlignment.Center;
		summary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		summary.CustomMinimumSize = new Vector2(0f, compactLayout ? 18f : 20f);
		bar.AddChild(summary);

		Control spacer = new()
		{
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		bar.AddChild(row);

		// Stable order: Reset / Enable All / Disable All pinned left; Save / Cancel pinned right.
		row.AddChild(reset);
		row.AddChild(enableAll);
		row.AddChild(disableAll);
		row.AddChild(spacer);
		row.AddChild(save);
		row.AddChild(cancel);

		updatePageActions = pageIndex =>
		{
			bool showPoolBulkActions = pageIndex is 1 or 2;
			enableAll.Visible = showPoolBulkActions;
			disableAll.Visible = showPoolBulkActions;
		};
		return bar;
	}

	private static void ReplaceDisabledIds(HashSet<string> target, IEnumerable<RuneConfigEntry> entries)
	{
		target.Clear();
		foreach (RuneConfigEntry entry in entries)
		{
			target.Add(entry.Id);
		}
	}
}
