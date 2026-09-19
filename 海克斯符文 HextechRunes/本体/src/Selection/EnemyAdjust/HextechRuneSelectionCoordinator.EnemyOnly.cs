using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Localization;
using static HextechRunes.HextechSelectionHelpers;

namespace HextechRunes;

internal static partial class HextechRuneSelectionCoordinator
{
	internal static bool NeedsEnemyOnlySelection(int playerCount, int newEnemyCount, bool presetChallenge)
		=> playerCount <= 0 && newEnemyCount > 0 && !presetChallenge;

	private static async Task<IReadOnlyList<MonsterHexKind>> SelectEnemyHexesOnly(
		RunState runState, HextechMayhemModifier modifier, int actIndex, HextechRarityTier rarity,
		IReadOnlyList<MonsterHexKind> previousHexes, IReadOnlyList<MonsterHexKind> newHexes)
	{
		RunManager manager = RunManager.Instance;
		EnemyHexAdjustmentSyncContext? sync = null;
		if (manager.NetService.Type is not (NetGameType.Singleplayer or NetGameType.None))
		{
			PlayerChoiceSynchronizer synchronizer = await WaitForPlayerChoiceSynchronizerAsync(manager);
			sync = CreateEnemyHexAdjustmentSyncContext(manager, runState, synchronizer, actIndex, newHexes)
				?? throw new OperationCanceledException("No enemy hex selection authority.");
		}

		bool authority = sync == null || IsLocalPlayer(manager, sync.AuthorityPlayer);
		HashSet<MonsterHexKind> seenHexes = modifier.GetKnownMonsterHexes().ToHashSet();
		seenHexes.UnionWith(newHexes);
		using CancellationTokenSource cancellation = new();
		HextechRuneSelectionScreen? screen = null;
		try
		{
			HextechEnemyHexAdjustmentOptions options = new()
			{
				InitialHexes = newHexes,
				ExcludedHexes = CombineMonsterHexes(previousHexes, newHexes),
				RerollLimit = modifier.MonsterHexRerollLimit,
				ControlsEnabled = authority,
				RerollFunc = authority
					? (hexes, slot, ordinal) => RerollEnemyHexForAct(modifier, rarity, runState, actIndex,
						GetMonsterHexSlot(hexes, slot), ordinal,
						CreateEnemyHexRerollExcludedIds(new HashSet<ModelId>(), hexes, slot), seenHexes)
					: null,
				Changed = authority && sync != null
					? (hexes, counts) => SendEnemyHexAdjustment(sync, hexes, counts, isFinal: false)
					: null,
				ScreenCreated = !authority && sync != null
					? created => sync.RemoteReceiveTask = ReceiveEnemyHexAdjustments(sync, runState, created, cancellation.Token)
					: null
			};
			// 空的玩家候选只显示敌方调整和确认，不抽取、同步或发放虚拟的玩家遗物。
			screen = await CreateRuneSelectionScreenAsync([], null, enemyHexOptions: options,
				titleOverride: new LocString("relic_collection", "HEXTECH_ENEMY_PREVIEW_LABEL").GetRawText(),
				cancellationToken: cancellation.Token);
			if (!authority)
			{
				await sync!.RemoteReceiveTask!;
				if (!screen.EnemyOnlySelectionConfirmed)
					throw new OperationCanceledException("Enemy hex selection interrupted before confirmation.");
			}
			await screen.RelicsSelected(removeOverlay: false);
			if (authority && sync != null)
				SendEnemyHexAdjustment(sync, screen.CurrentMonsterHexSlots, screen.EnemyHexRerollCounts, isFinal: true);
			return screen.CurrentMonsterHexes;
		}
		finally
		{
			cancellation.Cancel();
			if (screen != null) await screen.DismissAfterSelectionComplete();
			await ObserveEnemyHexAdjustmentReceiveTask(sync);
		}
	}
}
