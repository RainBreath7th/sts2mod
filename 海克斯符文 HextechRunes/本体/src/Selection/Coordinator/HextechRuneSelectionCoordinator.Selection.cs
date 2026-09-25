using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using static HextechRunes.HextechSelectionHelpers;

namespace HextechRunes;

internal static partial class HextechRuneSelectionCoordinator
{
	private static async Task<RuneSelectionResult> SelectRune(
		HextechMayhemModifier modifier,
		Player player,
		int actIndex,
		int choiceOrdinal,
		IReadOnlyList<RelicModel> options,
		RelicModel? monsterHexRelic,
		HextechEnemyHexAdjustmentOptions? enemyHexOptions = null)
	{
		string context = $"rune-choice act={actIndex} ordinal={choiceOrdinal}";
		RunManager runManager = RunManager.Instance;
		NetGameType gameType = runManager.NetService.Type;
		if (gameType is NetGameType.Singleplayer or NetGameType.None)
		{
			MarkRelicsSeen(options);
			modifier.RecordSeenPlayerRunes(player, options);
			HashSet<ModelId> seenOptionIds = CreateSeenOptionIds(options, modifier.GetSeenPlayerRuneIds(player));
			HextechGoldenRerollSession goldenReroll = CreateGoldenRerollSession(
				modifier,
				player,
				actIndex,
				choiceOrdinal,
				options);
			HextechRuneSelectionScreen screen = await CreateRuneSelectionScreenAsync(
				options,
				monsterHexRelic,
				(relics, slotIndex, rerollOrdinal) => RerollSingleOptionAndTrack(
					modifier,
					player,
					relics,
					slotIndex,
					seenOptionIds,
					GetGoldenRerollOverride(goldenReroll), rerollOrdinal),
				enemyHexOptions,
				modifier.PlayerRuneRerollLimit,
				goldenRerollSession: goldenReroll,
				selfPickPool: BuildSelfPickPool(modifier, player, options));
			RelicModel? selectedRelic = (await screen.RelicsSelected()).FirstOrDefault();
			return new RuneSelectionResult(selectedRelic, HextechWeightedRuneOptions.Copy(screen.CurrentRelics), screen.RerollHistory.Count, screen.CurrentMonsterHex, screen.CurrentMonsterHexes);
		}

		PlayerChoiceSynchronizer synchronizer = await WaitForPlayerChoiceSynchronizerAsync(runManager);

		uint choiceId = synchronizer.ReserveChoiceId(player);
		if (IsLocalPlayer(runManager, player))
		{
			MarkRelicsSeen(options);
			modifier.RecordSeenPlayerRunes(player, options);
			HashSet<ModelId> seenOptionIds = CreateSeenOptionIds(options, modifier.GetSeenPlayerRuneIds(player));
			HextechGoldenRerollSession goldenReroll = CreateGoldenRerollSession(
				modifier,
				player,
				actIndex,
				choiceOrdinal,
				options);
			HextechRuneSelectionScreen screen = await CreateRuneSelectionScreenAsync(
				options,
				monsterHexRelic,
				(relics, slotIndex, rerollOrdinal) => RerollSingleOptionAndTrackMultiplayer(
					modifier,
					player,
					relics,
					slotIndex,
					actIndex,
					rerollOrdinal,
					seenOptionIds,
					GetGoldenRerollOverride(goldenReroll)),
				enemyHexOptions,
				modifier.PlayerRuneRerollLimit,
				goldenRerollSession: goldenReroll,
				selfPickPool: BuildSelfPickPool(modifier, player, options));
			RelicModel? selectedRelic;
			try
			{
				selectedRelic = (await screen.RelicsSelected()).FirstOrDefault();
			}
			catch (OperationCanceledException)
			{
				uint canceledChoiceId = SyncLocalHextechChoice(
					synchronizer,
					player,
					choiceId,
					CreateRuneChoiceResult(actIndex, choiceOrdinal, screen, selectedRelic: null),
					context);
				HextechLog.Info($"[{ModInfo.Id}][Mayhem] RuneChoice sync canceled: act={actIndex} ordinal={choiceOrdinal} player={player.NetId} choiceId={canceledChoiceId}");
				throw;
			}

			uint sentChoiceId = SyncLocalHextechChoice(
				synchronizer,
				player,
				choiceId,
				CreateRuneChoiceResult(actIndex, choiceOrdinal, screen, selectedRelic),
				context);
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] RuneChoice sync local: act={actIndex} ordinal={choiceOrdinal} player={player.NetId} choiceId={sentChoiceId}");
			return new RuneSelectionResult(selectedRelic, HextechWeightedRuneOptions.Copy(screen.CurrentRelics), screen.RerollHistory.Count, screen.CurrentMonsterHex, screen.CurrentMonsterHexes);
		}

		HextechLog.Info($"[{ModInfo.Id}][Mayhem] RuneChoice wait remote: act={actIndex} ordinal={choiceOrdinal} player={player.NetId} choiceId={choiceId}");
		(PlayerChoiceResult remoteChoice, uint receivedChoiceId)? received = await TryWaitForRemoteHextechChoice(
			synchronizer,
			(RunState)player.RunState,
			player,
			choiceId,
			result => HextechChoiceCodec.IsRuneSelection(result, actIndex, choiceOrdinal),
			context,
			RemoteRuneChoicePollFrames,
			() => ShouldKeepWaitingForRemoteRuneChoice((RunState)player.RunState));
		if (!received.HasValue)
		{
			throw new OperationCanceledException(
				$"Remote rune selection was interrupted: {context} player={player.NetId} choiceId={choiceId}.");
		}

		(PlayerChoiceResult remoteChoice, uint receivedChoiceId) = received.Value;
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] RuneChoice remote received: act={actIndex} ordinal={choiceOrdinal} player={player.NetId} choiceId={receivedChoiceId}");
		return ResolveRemoteRuneChoice(modifier, player, actIndex, choiceOrdinal, remoteChoice);
	}

	private static async Task<RuneSelectionResult> SelectRuneMultiplayer(
		HextechMayhemModifier modifier,
		PendingRuneSelection selection,
		PlayerChoiceSynchronizer synchronizer,
		int actIndex,
		int choiceOrdinal,
		RelicModel? monsterHexRelic,
		HextechEnemyHexAdjustmentOptions? enemyHexOptions = null,
		Func<HextechRuneSelectionScreen, Task>? afterLocalSelection = null,
		Action<HextechRuneSelectionScreen>? screenCreated = null,
		Func<Task?>? getConcurrentTask = null,
		CancellationToken cancellationToken = default)
	{
		string context = $"rune-choice act={actIndex} ordinal={choiceOrdinal}";
		cancellationToken.ThrowIfCancellationRequested();
		if (selection.IsLocal)
		{
			MarkRelicsSeen(selection.Options);
			modifier.RecordSeenPlayerRunes(selection.Player, selection.Options);
			HashSet<ModelId> seenOptionIds = CreateSeenOptionIds(selection.Options, modifier.GetSeenPlayerRuneIds(selection.Player));
			HextechGoldenRerollSession goldenReroll = CreateGoldenRerollSession(
				modifier,
				selection.Player,
				actIndex,
				choiceOrdinal,
				selection.Options);
			HextechRuneSelectionScreen screen = await CreateRuneSelectionScreenAsync(
				selection.Options,
				monsterHexRelic,
				(relics, slotIndex, rerollOrdinal) => RerollSingleOptionAndTrackMultiplayer(
					modifier,
					selection.Player,
					relics,
					slotIndex,
					actIndex,
					rerollOrdinal,
					seenOptionIds,
					GetGoldenRerollOverride(goldenReroll)),
				enemyHexOptions,
				modifier.PlayerRuneRerollLimit,
				goldenRerollSession: goldenReroll,
				cancellationToken: cancellationToken,
				selfPickPool: BuildSelfPickPool(modifier, selection.Player, selection.Options));
			screenCreated?.Invoke(screen);
			RelicModel? selectedRelic;
			try
			{
				Task<IEnumerable<RelicModel>> localSelection = screen.RelicsSelected(removeOverlay: false);
				selectedRelic = (await WaitForSelectionWithConcurrentFailure(
					localSelection,
					getConcurrentTask?.Invoke(),
					context,
					cancellationToken)).FirstOrDefault();
			}
			catch (OperationCanceledException)
			{
				if (IsMultiplayerConnected())
				{
					uint canceledChoiceId = SyncLocalHextechChoice(
						synchronizer,
						selection.Player,
						selection.ChoiceId,
						CreateRuneChoiceResult(actIndex, choiceOrdinal, screen, selectedRelic: null),
						context);
					HextechLog.Info($"[{ModInfo.Id}][Mayhem] RuneChoice sync canceled: act={actIndex} ordinal={choiceOrdinal} player={selection.Player.NetId} choiceId={canceledChoiceId}");
				}

				throw;
			}

			uint sentChoiceId = SyncLocalHextechChoice(
				synchronizer,
				selection.Player,
				selection.ChoiceId,
				CreateRuneChoiceResult(actIndex, choiceOrdinal, screen, selectedRelic),
				context);
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] RuneChoice sync local: act={actIndex} ordinal={choiceOrdinal} player={selection.Player.NetId} choiceId={sentChoiceId}");
			_ = RequireCompletedSelection(
				selectedRelic,
				$"local {context} player={selection.Player.NetId} choiceId={sentChoiceId}");

			if (afterLocalSelection != null)
			{
				await afterLocalSelection(screen).WaitAsync(cancellationToken);
			}

			return new RuneSelectionResult(selectedRelic, HextechWeightedRuneOptions.Copy(screen.CurrentRelics), screen.RerollHistory.Count, screen.CurrentMonsterHex, screen.CurrentMonsterHexes, screen);
		}

		HextechLog.Info($"[{ModInfo.Id}][Mayhem] RuneChoice wait remote: act={actIndex} ordinal={choiceOrdinal} player={selection.Player.NetId} choiceId={selection.ChoiceId}");
		(PlayerChoiceResult remoteChoice, uint receivedChoiceId)? received = await TryWaitForRemoteHextechChoice(
			synchronizer,
			(RunState)selection.Player.RunState,
			selection.Player,
			selection.ChoiceId,
			result => HextechChoiceCodec.IsRuneSelection(result, actIndex, choiceOrdinal),
			context,
			RemoteRuneChoicePollFrames,
			() => ShouldKeepWaitingForRemoteRuneChoice((RunState)selection.Player.RunState),
			cancellationToken: cancellationToken);
		if (!received.HasValue)
		{
			throw new OperationCanceledException(
				$"Remote rune selection was interrupted: {context} player={selection.Player.NetId} choiceId={selection.ChoiceId}.");
		}

		(PlayerChoiceResult remoteChoice, uint receivedChoiceId) = received.Value;
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] RuneChoice remote received: act={actIndex} ordinal={choiceOrdinal} player={selection.Player.NetId} choiceId={receivedChoiceId}");
		return ResolveRemoteRuneChoice(modifier, selection.Player, actIndex, choiceOrdinal, remoteChoice);
	}

	private static bool ShouldKeepWaitingForRemoteRuneChoice(RunState runState)
	{
		return IsCurrentRun(runState) && IsMultiplayerConnected();
	}

	private static async Task<HextechRuneSelectionScreen> CreateRuneSelectionScreenAsync(
		IReadOnlyList<RelicModel> relics,
		RelicModel? monsterHexRelic,
		Func<IReadOnlyList<RelicModel>, int, int, IReadOnlyList<RelicModel>>? rerollFunc = null,
		HextechEnemyHexAdjustmentOptions? enemyHexOptions = null,
		int playerRuneRerollLimit = 1,
		string? titleOverride = null,
		HextechGoldenRerollSession? goldenRerollSession = null,
		CancellationToken cancellationToken = default,
		IReadOnlyList<RelicModel>? selfPickPool = null,
		bool continueOnly = false)
	{
		await WaitForSingletonAsync(static () => NOverlayStack.Instance, cancellationToken: cancellationToken);
		HextechRuneSelectionScreen selectionScreen = HextechRuneSelectionScreen.Create(
			relics,
			monsterHexRelic,
			rerollFunc,
			enemyHexOptions,
			playerRuneRerollLimit,
			titleOverride,
			goldenRerollSession: goldenRerollSession,
			selfPickPool: selfPickPool,
			continueOnly: continueOnly);
		if (NOverlayStack.Instance == null)
		{
			throw new InvalidOperationException("NOverlayStack is not available for rune selection.");
		}

		NOverlayStack.Instance.Push(selectionScreen);
		enemyHexOptions?.ScreenCreated?.Invoke(selectionScreen);
		return selectionScreen;
	}

	/// <summary>
	/// 本稀有度已无任何可选海克斯(未见的和见过没选的都抽完、其余全部拥有/互斥/禁用):只给说明和"继续"按钮。
	/// 纯本机界面,不抽取、不同步、不发放;联机时各端对"无候选"的判断一致,不需要额外协议。
	/// </summary>
	private static async Task ShowNoRuneOptionsScreenAsync(CancellationToken cancellationToken = default)
	{
		HextechRuneSelectionScreen? screen = null;
		try
		{
			screen = await CreateRuneSelectionScreenAsync(
				[],
				null,
				titleOverride: new LocString("relic_collection", "HEXTECH_NO_RUNE_OPTIONS_TITLE").GetRawText(),
				cancellationToken: cancellationToken,
				continueOnly: true);
			// 联机批次被取消(断线、换局)时不能一直等玩家点继续。
			Task completed = await Task.WhenAny(
				screen.RelicsSelected(removeOverlay: false),
				Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken));
			cancellationToken.ThrowIfCancellationRequested();
			await completed;
		}
		finally
		{
			if (screen != null)
			{
				await screen.DismissAfterSelectionComplete();
			}
		}
	}

	private static async Task<RuneSelectionResult> SelectRuneWithLocalScreen(
		HextechMayhemModifier modifier,
		Player player,
		IReadOnlyList<RelicModel> options,
		RelicModel? monsterHexRelic,
		HextechEnemyHexAdjustmentOptions? enemyHexOptions,
		bool useMultiplayerReroll,
		bool removeOverlay,
		string? titleOverride = null)
	{
		MarkRelicsSeen(options);
		modifier.RecordSeenPlayerRunes(player, options);
		HashSet<ModelId> seenOptionIds = CreateSeenOptionIds(options, modifier.GetSeenPlayerRuneIds(player));
		HextechRuneSelectionScreen screen = await CreateRuneSelectionScreenAsync(
			options,
			monsterHexRelic,
			useMultiplayerReroll
				? (relics, slotIndex, rerollOrdinal) => RerollSingleOptionAndTrackMultiplayer(modifier, player, relics, slotIndex, modifier.GetCurrentStageIndex(), rerollOrdinal, seenOptionIds)
				: (relics, slotIndex, rerollOrdinal) => RerollSingleOptionAndTrack(modifier, player, relics, slotIndex, seenOptionIds, chaosRerollOrdinal: rerollOrdinal),
			enemyHexOptions,
			modifier.PlayerRuneRerollLimit,
			titleOverride);
		RelicModel? selectedRelic = (await screen.RelicsSelected(removeOverlay)).FirstOrDefault();
		return new RuneSelectionResult(selectedRelic, HextechWeightedRuneOptions.Copy(screen.CurrentRelics), screen.RerollHistory.Count, screen.CurrentMonsterHex, screen.CurrentMonsterHexes, removeOverlay ? null : screen);
	}

	/// <summary>
	/// 玩家海克斯重随次数为无限时,选择界面改为自选:列出本次候选稀有度的全部合法海克斯。
	/// 合法池与重随同一套规则(配置启用、版本可用、本幕允许、角色可用、已拥有与互斥排除),不排除"已见",不消耗随机数。
	/// 只在本机构造给界面用;远端只收到最终候选与选中序号,按 ID 还原,不需要这份池。
	/// </summary>
	private static IReadOnlyList<RelicModel>? BuildSelfPickPool(HextechMayhemModifier modifier, Player player, IReadOnlyList<RelicModel> options)
	{
		if (modifier.PlayerRuneRerollLimit != HextechRuneConfiguration.InfiniteRerollLimit || options.Count == 0)
		{
			return null;
		}

		try
		{
			HextechRarityTier rarity = GetRarityForOptions(options);
			List<RelicModel> pool = BuildSelectableRunePool(player, rarity, (RunState)player.RunState)
				.Select(relic => CreateSelectableRuneOption(player, relic))
				.ToList();
			return pool.Count > 0 ? pool : null;
		}
		catch (Exception ex)
		{
			// 构造失败就退回普通的三选一界面,不阻断本幕选择。
			Log.Warn($"[{ModInfo.Id}][Mayhem] Self-pick pool unavailable, falling back to regular choices: player={player.NetId} error={ex.GetType().Name}: {ex.Message}");
			return null;
		}
	}

	private static PlayerChoiceResult CreateRuneChoiceResult(int actIndex, int choiceOrdinal, HextechRuneSelectionScreen screen, RelicModel? selectedRelic)
	{
		int selectedIndex = IndexOfRelicInstance(screen.CurrentRelics, selectedRelic);
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] CreateRuneChoiceResult: act={actIndex} ordinal={choiceOrdinal} selectedIndex={selectedIndex} rerolls={string.Join(",", screen.RerollHistory)}");
		return HextechChoiceCodec.CreateRuneSelection(actIndex, choiceOrdinal, selectedIndex, screen.RerollHistory, screen.CurrentRelics);
	}

	private static RuneSelectionResult ResolveRemoteRuneChoice(
		HextechMayhemModifier modifier,
		Player player,
		int actIndex,
		int choiceOrdinal,
		PlayerChoiceResult remoteChoice)
	{
		if (!HextechChoiceCodec.TryDecodeRuneSelection(remoteChoice, actIndex, choiceOrdinal, out int selectedIndex, out List<int> rerollHistory, out List<ModelId> syncedOptionIds))
		{
			string message =
				$"Malformed rune selection payload: act={actIndex} ordinal={choiceOrdinal} " +
				$"player={player.NetId} result={remoteChoice}";
			throw CreateProtocolFailure($"rune-choice act={actIndex} ordinal={choiceOrdinal}", message);
		}

		if (syncedOptionIds.Count == 0)
		{
			string message =
				$"Rune selection payload omitted authoritative final options: act={actIndex} " +
				$"ordinal={choiceOrdinal} player={player.NetId}";
			throw CreateProtocolFailure($"rune-choice act={actIndex} ordinal={choiceOrdinal}", message);
		}

		if (!TryCreateSyncedRuneOptions(player, syncedOptionIds, actIndex, choiceOrdinal, out List<RelicModel> syncedOptions))
		{
			string message =
				$"Failed to load authoritative rune options: act={actIndex} ordinal={choiceOrdinal} " +
				$"player={player.NetId} ids={string.Join(",", syncedOptionIds.Select(static id => id.Entry))}";
			throw CreateProtocolFailure($"rune-choice act={actIndex} ordinal={choiceOrdinal}", message);
		}

		if (!HextechGeneratedRuneDataCodec.Restore(remoteChoice, syncedOptions))
			throw CreateProtocolFailure("generated rune data", "Invalid or missing generated rune recipe.");
		if (!HextechRuneWeightCodec.TryRestore(remoteChoice, syncedOptions, out syncedOptions))
			throw CreateProtocolFailure("character rune weight", "Rune selection omitted a valid final character weight.");

		if (selectedIndex < -1 || selectedIndex >= syncedOptions.Count)
		{
			string message =
				$"Invalid rune selection index: act={actIndex} ordinal={choiceOrdinal} player={player.NetId} " +
				$"index={selectedIndex} optionCount={syncedOptions.Count}";
			throw CreateProtocolFailure($"rune-choice act={actIndex} ordinal={choiceOrdinal}", message);
		}

		MarkRelicsSeen(syncedOptions);
		modifier.RecordSeenPlayerRunes(player, syncedOptions);
		RelicModel syncedSelectedRelic = RequireCompletedSelection(
			selectedIndex >= 0 ? syncedOptions[selectedIndex] : null,
			$"remote rune-choice act={actIndex} ordinal={choiceOrdinal} player={player.NetId}");
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] ResolveRemoteRuneChoice: player={player.NetId} selectedIndex={selectedIndex} rerolls={string.Join(",", rerollHistory)} syncedOptions={string.Join(",", syncedOptions.Select(o => (o.CanonicalInstance?.Id ?? o.Id).Entry))}");
		return new RuneSelectionResult(syncedSelectedRelic, syncedOptions, rerollHistory.Count, null);
	}

	private static async Task<T> WaitForSelectionWithConcurrentFailure<T>(
		Task<T> selectionTask,
		Task? concurrentTask,
		string context,
		CancellationToken cancellationToken)
	{
		if (concurrentTask == null)
		{
			return await selectionTask.WaitAsync(cancellationToken);
		}

		using CancellationTokenSource monitorCancellation =
			CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		Task concurrentFailure = WaitForFailureAsync(concurrentTask, monitorCancellation.Token);
		Task<T> cancelableSelection = selectionTask.WaitAsync(cancellationToken);
		try
		{
			Task winner = await Task.WhenAny(cancelableSelection, concurrentFailure);
			if (winner == concurrentFailure)
			{
				await concurrentFailure;
			}

			return await cancelableSelection;
		}
		finally
		{
			monitorCancellation.Cancel();
			ObserveCompletion(concurrentFailure, $"{context} concurrent task monitor");
		}
	}

	private static async Task WaitForFailureAsync(Task task, CancellationToken cancellationToken)
	{
		await task.WaitAsync(cancellationToken);
		await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
	}

	private static bool TryCreateSyncedRuneOptions(
		Player player,
		IReadOnlyList<ModelId> optionIds,
		int actIndex,
		int choiceOrdinal,
		out List<RelicModel> options)
	{
		options = new(optionIds.Count);
		try
		{
			foreach (ModelId id in optionIds)
			{
				RelicModel relic = ModelDb.GetById<RelicModel>(id);
				options.Add(CreateSelectableRuneOption(player, relic));
			}

			return options.Count > 0;
		}
		catch (Exception ex)
		{
			Log.Error($"[{ModInfo.Id}][Mayhem] ResolveRemoteRuneChoice: failed to load synced option model: act={actIndex} ordinal={choiceOrdinal} player={player.NetId} ids={string.Join(",", optionIds)} error={ex}");
			options.Clear();
			return false;
		}
	}
}
