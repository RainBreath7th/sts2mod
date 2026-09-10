using static HextechRunes.HextechSelectionHelpers;

namespace HextechRunes;

internal static partial class HextechRuneSelectionCoordinator
{
	private static IReadOnlyList<RelicModel> RerollSingleOptionAndTrack(
		HextechMayhemModifier modifier,
		Player player,
		IReadOnlyList<RelicModel> currentOptions,
		int slotIndex,
		HashSet<ModelId> seenOptionIds,
		HextechRarityTier? rarityOverride = null,
		int chaosRerollOrdinal = 0)
	{
		IReadOnlyList<RelicModel> rerolled = RerollSingleOption(
			player,
			(RunState)player.RunState,
			currentOptions,
			slotIndex,
			seenOptionIds,
			modifier.IsEndlessLoopActive,
			rarityOverride, chaosRerollOrdinal);
		if (!ReferenceEquals(rerolled, currentOptions))
		{
			ModelId rerolledId = rerolled[slotIndex].CanonicalInstance?.Id ?? rerolled[slotIndex].Id;
			seenOptionIds.Add(rerolledId);
			MarkRelicsSeen([ rerolled[slotIndex] ]);
			modifier.RecordSeenPlayerRunes(player, [ rerolled[slotIndex] ]);
		}

		return rerolled;
	}

	private static IReadOnlyList<RelicModel> RerollSingleOption(
		Player player,
		RunState runState,
		IReadOnlyList<RelicModel> currentOptions,
		int slotIndex,
		HashSet<ModelId> seenOptionIds,
		bool useEndlessTagWindow,
		HextechRarityTier? rarityOverride, int chaosRerollOrdinal)
	{
		if (slotIndex < 0 || slotIndex >= currentOptions.Count)
		{
			return currentOptions;
		}

		HashSet<ModelId> currentOptionIds = currentOptions
			.Select(static relic => relic.CanonicalInstance?.Id ?? relic.Id)
			.ToHashSet();
		HashSet<ModelId> excludedIds = new(currentOptionIds);
		excludedIds.UnionWith(seenOptionIds);
		HextechRarityTier rarity = rarityOverride ?? GetRarityForOption(currentOptions[slotIndex]);
		List<RelicModel> candidates = ConstrainRerollCandidates(
			player,
			BuildSelectableRunePool(player, rarity, runState, excludedIds),
			currentOptions,
			slotIndex);
		if (candidates.Count == 0 && seenOptionIds.Count > 0)
		{
			// 池被「已见」清空:重置(清空)已见集,让重随能重新刷到此前见过的符文(仍排除当前选项)。
			seenOptionIds.Clear();
			candidates = ConstrainRerollCandidates(
				player,
				BuildSelectableRunePool(player, rarity, runState, currentOptionIds),
				currentOptions,
				slotIndex);
		}

		if (candidates.Count == 0)
		{
			return currentOptions;
		}

		Dictionary<string, int> tagCounts = BuildOwnedRuneTagCounts(player, useEndlessTagWindow);
		List<int> weights = HextechRunePoolBuilder.BuildSelectionWeights(candidates, tagCounts, useEndlessTagWindow,
			HextechRunePoolBuilder.GetRuneCharacterPool(player), HextechWeightedRuneOptions.GetWeight(currentOptions), out int totalWeight);
		int selectedIndex = SelectWeightedIndex(weights, runState.Rng.Niche.NextInt(totalWeight));
		List<RelicModel> updated = currentOptions.ToList();
		updated[slotIndex] = CreateSelectableRuneOption(player, candidates[selectedIndex]);
		return new HextechWeightedRuneOptions(HextechRuneGeneration.Transform(player, rarity, runState, -1, updated, slotIndex, chaosRerollOrdinal),
			HextechRunePoolBuilder.AdvanceCharacterWeight(player, HextechWeightedRuneOptions.GetWeight(currentOptions), candidates[selectedIndex]));
	}

	private static IReadOnlyList<RelicModel> RerollSingleOptionAndTrackMultiplayer(
		HextechMayhemModifier modifier,
		Player player,
		IReadOnlyList<RelicModel> currentOptions,
		int slotIndex,
		int selectionStageIndex,
		int rerollOrdinal,
		HashSet<ModelId> seenOptionIds,
		HextechRarityTier? rarityOverride = null)
	{
		IReadOnlyList<RelicModel> rerolled = RerollSingleOptionMultiplayer(
			player,
			currentOptions,
			slotIndex,
			selectionStageIndex,
			rerollOrdinal,
			seenOptionIds,
			modifier.IsEndlessLoopActive,
			rarityOverride);
		if (!ReferenceEquals(rerolled, currentOptions))
		{
			ModelId rerolledId = rerolled[slotIndex].CanonicalInstance?.Id ?? rerolled[slotIndex].Id;
			seenOptionIds.Add(rerolledId);
			MarkRelicsSeen([ rerolled[slotIndex] ]);
			modifier.RecordSeenPlayerRunes(player, [ rerolled[slotIndex] ]);
			HextechLog.Info($"[{ModInfo.Id}][Mayhem] RerollSingleOptionMultiplayer: player={player.NetId} slot={slotIndex} ordinal={rerollOrdinal} relic={rerolledId.Entry}");
		}

		return rerolled;
	}

	private static IReadOnlyList<RelicModel> RerollSingleOptionMultiplayer(
		Player player,
		IReadOnlyList<RelicModel> currentOptions,
		int slotIndex,
		int selectionStageIndex,
		int rerollOrdinal,
		HashSet<ModelId> seenOptionIds,
		bool useEndlessTagWindow,
		HextechRarityTier? rarityOverride)
	{
		if (slotIndex < 0 || slotIndex >= currentOptions.Count)
		{
			return currentOptions;
		}

		HashSet<ModelId> currentOptionIds = currentOptions
			.Select(static relic => relic.CanonicalInstance?.Id ?? relic.Id)
			.ToHashSet();
		HashSet<ModelId> excludedIds = new(currentOptionIds);
		excludedIds.UnionWith(seenOptionIds);

		HextechRarityTier rarity = rarityOverride ?? GetRarityForOption(currentOptions[slotIndex]);
		RunState runState = (RunState)player.RunState;
		List<RelicModel> pool = ConstrainRerollCandidates(
				player,
				BuildSelectableRunePool(player, rarity, runState, excludedIds),
				currentOptions,
				slotIndex)
			.OrderBy(static relic => (relic.CanonicalInstance?.Id ?? relic.Id).Entry, StringComparer.Ordinal)
			.ToList();
		if (pool.Count == 0 && seenOptionIds.Count > 0)
		{
			// 池被「已见」清空:重置(清空)已见集,让重随能重新刷到此前见过的符文(仍排除当前选项)。
			seenOptionIds.Clear();
			pool = ConstrainRerollCandidates(
					player,
					BuildSelectableRunePool(player, rarity, runState, currentOptionIds),
					currentOptions,
					slotIndex)
				.OrderBy(static relic => (relic.CanonicalInstance?.Id ?? relic.Id).Entry, StringComparer.Ordinal)
				.ToList();
		}

		if (pool.Count == 0)
		{
			return currentOptions;
		}

		int index = GetMultiplayerRerollIndex(player, pool, rarity, slotIndex, selectionStageIndex, rerollOrdinal, useEndlessTagWindow, HextechWeightedRuneOptions.GetWeight(currentOptions));
		List<RelicModel> updated = currentOptions.ToList();
		updated[slotIndex] = CreateSelectableRuneOption(player, pool[index]);
		return new HextechWeightedRuneOptions(HextechRuneGeneration.Transform(player, rarity, runState, selectionStageIndex, updated, slotIndex, rerollOrdinal),
			HextechRunePoolBuilder.AdvanceCharacterWeight(player, HextechWeightedRuneOptions.GetWeight(currentOptions), pool[index]));
	}

	private static HextechRarityTier GetRarityForOption(RelicModel relic)
	{
		return GetRarityForOptions([ relic ]);
	}

	private static List<RelicModel> ConstrainRerollCandidates(
		Player player,
		IEnumerable<RelicModel> candidates,
		IReadOnlyList<RelicModel> currentOptions,
		int slotIndex)
	{
		bool upgradeAlreadyPresent = currentOptions
			.Where((_, index) => index != slotIndex)
			.Any(HextechRunePoolBuilder.IsUpgradeRune);
		return HextechRunePoolBuilder.ConstrainCandidates(candidates, upgradeAlreadyPresent);
	}

	private static int GetMultiplayerRerollIndex(
		Player player,
		IReadOnlyList<RelicModel> pool,
		HextechRarityTier rarity,
		int slotIndex,
		int selectionStageIndex,
		int rerollOrdinal,
		bool useEndlessTagWindow, int characterWeightPercent)
	{
		RunState runState = (RunState)player.RunState;
		Dictionary<string, int> tagCounts = BuildOwnedRuneTagCounts(player, useEndlessTagWindow);
		List<int> weights = HextechRunePoolBuilder.BuildSelectionWeights(pool, tagCounts, useEndlessTagWindow,
			HextechRunePoolBuilder.GetRuneCharacterPool(player), characterWeightPercent, out int totalWeight);
		List<string> parts =
		[
			runState.Rng.StringSeed,
			"|act:",
			selectionStageIndex.ToString(),
			"|player:",
			HextechStableRandom.PlayerKey(player),
			"|rarity:",
			((int)rarity).ToString(),
			"|slot:",
			slotIndex.ToString(),
			"|ordinal:",
			rerollOrdinal.ToString()
		];
		for (int i = 0; i < pool.Count; i++)
		{
			parts.Add("|pool:");
			parts.Add((pool[i].CanonicalInstance?.Id ?? pool[i].Id).Entry);
			parts.Add(":");
			parts.Add(weights[i].ToString());
		}

		int roll = HextechStableRandom.IndexFromRawParts(totalWeight, parts.ToArray());
		return SelectWeightedIndex(weights, roll);
	}
}
