namespace HextechRunes;

internal static partial class HextechRuneSelectionCoordinator
{
	private static List<RelicModel> BuildSelectableRunePool(Player player, HextechRarityTier rarity, RunState runState, IReadOnlySet<ModelId>? excludedIds = null)
	{
		return HextechRunePoolBuilder.BuildSelectableRunePool(player, rarity, runState, excludedIds);
	}

	private static List<RelicModel> BuildSelectableRunesForRarity(
		Player player,
		HextechRarityTier rarity,
		RunState runState,
		IReadOnlySet<ModelId>? excludedIds = null,
		bool useEndlessTagWindow = false)
	{
		return HextechRunePoolBuilder.BuildSelectableRunesForRarity(player, rarity, runState, excludedIds, useEndlessTagWindow);
	}

	private static List<RelicModel> BuildStableSelectableRunesForRarity(
		Player player,
		HextechRarityTier rarity,
		RunState runState,
		int selectionStageIndex,
		IReadOnlySet<ModelId>? excludedIds = null,
		bool useEndlessTagWindow = false)
	{
		return HextechRunePoolBuilder.BuildStableSelectableRunesForRarity(player, rarity, runState, selectionStageIndex, excludedIds, useEndlessTagWindow);
	}

	private static Dictionary<string, int> BuildOwnedRuneTagCounts(Player player, bool useEndlessTagWindow)
	{
		return HextechRunePoolBuilder.BuildOwnedRuneTagCounts(player, useEndlessTagWindow);
	}

	private static List<int> BuildRuneTagWeights(
		IReadOnlyList<RelicModel> pool,
		IReadOnlyDictionary<string, int> tagCounts,
		bool useEndlessTagWindow,
		out int totalWeight)
	{
		return HextechRunePoolBuilder.BuildRuneTagWeights(pool, tagCounts, useEndlessTagWindow, out totalWeight);
	}

	private static int SelectWeightedIndex(IReadOnlyList<int> weights, int roll)
	{
		return HextechRunePoolBuilder.SelectWeightedIndex(weights, roll);
	}

	private static RelicModel CreateSelectableRuneOption(Player player, RelicModel relic)
	{
		return HextechRunePoolBuilder.CreateSelectableRuneOption(player, relic);
	}

	// 玩家候选只排除本局已见过的符文。敌我同名不再互相回避:敌方持有的海克斯照样可以出现在玩家候选里。
	private static HashSet<ModelId> CreateBaseExcludedIds(HextechMayhemModifier modifier, Player player)
	{
		return modifier.GetSeenPlayerRuneIds(player);
	}

	private static HashSet<ModelId> CreateSeenOptionIds(IEnumerable<RelicModel> options, IEnumerable<ModelId>? alreadySeenIds = null)
	{
		HashSet<ModelId> seenOptionIds = options
			.Select(static relic => relic.CanonicalInstance?.Id ?? relic.Id)
			.ToHashSet();
		if (alreadySeenIds != null)
		{
			seenOptionIds.UnionWith(alreadySeenIds);
		}

		return seenOptionIds;
	}

	private static MonsterHexKind? FirstMonsterHexOrNull(IEnumerable<MonsterHexKind>? monsterHexes)
	{
		if (monsterHexes == null)
		{
			return null;
		}

		foreach (MonsterHexKind monsterHex in monsterHexes)
		{
			return monsterHex;
		}

		return null;
	}

	// 敌方重掷只避开同一界面上其他敌方槽位的海克斯,不看玩家候选。
	private static HashSet<ModelId> CreateEnemyHexRerollExcludedIds(IReadOnlyList<MonsterHexKind?> currentMonsterHexes, int rerollSlotIndex)
	{
		HashSet<ModelId> excludedIds = [];
		for (int i = 0; i < currentMonsterHexes.Count; i++)
		{
			if (i != rerollSlotIndex && currentMonsterHexes[i].HasValue)
			{
				excludedIds.Add(GetMonsterHexIconRelicId(currentMonsterHexes[i]!.Value));
			}
		}

		return excludedIds;
	}

	private static HextechRarityTier GetRarityForOptions(IReadOnlyList<RelicModel> relics)
	{
		return HextechRunePoolBuilder.GetRarityForOptions(relics);
	}

	public static void RemoveRunesFromGrabBags(Player player)
	{
		foreach (RelicModel relic in HextechCatalog.GetCanonicalRunes())
		{
			player.RelicGrabBag.Remove(relic);
			player.RunState.SharedRelicGrabBag.Remove(relic);
		}
	}

	private static bool IsCurrentRun(RunState runState)
	{
		return ReferenceEquals(RunManager.Instance.DebugOnlyGetState(), runState);
	}
}
