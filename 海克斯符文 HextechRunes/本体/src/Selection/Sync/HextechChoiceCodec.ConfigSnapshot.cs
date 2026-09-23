namespace HextechRunes;

internal static partial class HextechChoiceCodec
{
	private static void AppendDisabledPlayerRuneConfig(List<int> payload, IReadOnlySet<string> disabledPlayerRuneIds)
	{
		IReadOnlyList<ModelId> ids = PlayerRuneIdsByOrdinal.Value;
		int wordCount = ids.Count / PlayerRuneConfigBitsPerWord
			+ (ids.Count % PlayerRuneConfigBitsPerWord == 0 ? 0 : 1);
		ValidateProtocolCount(wordCount, MaxPlayerRuneConfigBitsetWords, nameof(disabledPlayerRuneIds));
		int[] words = new int[wordCount];
		for (int i = 0; i < ids.Count; i++)
		{
			if (!disabledPlayerRuneIds.Contains(ids[i].Entry))
			{
				continue;
			}

			words[i / PlayerRuneConfigBitsPerWord] |= 1 << (i % PlayerRuneConfigBitsPerWord);
		}

		payload.Add(PlayerRuneConfigBitsetVersion);
		payload.Add(wordCount);
		payload.AddRange(words);
	}

	private static bool TryDecodeDisabledPlayerRuneConfig(List<int> payload, int cursor, out HashSet<string> disabledPlayerRuneIds, out int nextCursor)
	{
		disabledPlayerRuneIds = [];
		nextCursor = cursor;
		if (payload.Count <= cursor)
		{
			return true;
		}

		if (payload[cursor] != PlayerRuneConfigBitsetVersion)
		{
			return true;
		}

		cursor++;
		if (payload.Count <= cursor)
		{
			return false;
		}

		int wordCount = payload[cursor++];
		if (wordCount < 0
			|| wordCount > MaxPlayerRuneConfigBitsetWords
			|| !HasRemaining(payload, cursor, wordCount))
		{
			return false;
		}

		IReadOnlyList<ModelId> ids = PlayerRuneIdsByOrdinal.Value;
		for (int i = 0; i < ids.Count; i++)
		{
			int wordIndex = i / PlayerRuneConfigBitsPerWord;
			int bitIndex = i % PlayerRuneConfigBitsPerWord;
			if (wordIndex < wordCount && (payload[cursor + wordIndex] & (1 << bitIndex)) != 0)
			{
				disabledPlayerRuneIds.Add(ids[i].Entry);
			}
		}

		nextCursor = cursor + wordCount;
		return true;
	}

	private static void AppendRunConfigurationSnapshot(List<int> payload, HextechRunConfigurationSnapshot snapshot)
	{
		payload.Add(RunConfigurationSnapshotVersion);
		payload.AddRange(HextechPlayerHexCountState.Normalize(snapshot.PlayerHexCountsByAct));
		payload.AddRange(HextechEnemyHexCountState.Normalize(snapshot.EnemyHexCountsByAct));
		payload.Add(HextechRuneConfiguration.ClampRerollLimit(snapshot.PlayerRuneRerollLimit));
		payload.Add(HextechRuneConfiguration.ClampRerollLimit(snapshot.MonsterHexRerollLimit));
		foreach (HextechRarityWeights weights in snapshot.RuneRarityWeightsByAct)
		{
			AppendRarityWeights(payload, weights);
		}
		payload.Add(snapshot.PreventConsecutiveSilverRunes ? 1 : 0);
		payload.Add(HextechRuneConfiguration.ClampGoldenRerollChancePercent(snapshot.GoldenRerollChancePercent));
		AppendForgeRarityWeights(payload, snapshot.ForgeRarityWeights);
		payload.Add(HextechRuneConfiguration.ClampRandomForgeShopPrice(snapshot.RandomForgeShopPrice));
		payload.Add(snapshot.RandomForgeDirectGrant ? 1 : 0);

		MonsterHexKind[] disabledMonsterHexes = snapshot.DisabledMonsterHexIds
			.Select(static id => Enum.TryParse(id, out MonsterHexKind kind) ? (MonsterHexKind?)kind : null)
			.Where(static kind => kind.HasValue)
			.Select(static kind => kind!.Value)
			.OrderBy(static kind => (int)kind)
			.ToArray();
		ValidateProtocolCount(disabledMonsterHexes.Length, MaxDisabledMonsterHexes, nameof(snapshot));
		payload.Add(disabledMonsterHexes.Length);
		payload.AddRange(disabledMonsterHexes.Select(static kind => (int)kind));

		HextechStableModelIdListCodec.Append(
			payload,
			snapshot.DisabledForgeIds
				.Select(static entry => new ModelId(ModInfo.Id, entry))
				.OrderBy(static id => id.Entry, StringComparer.Ordinal));

		// 模组总开关:作为尾部可选 int 追加,避免改 snapshot 版本号/定长计数。
		// 旧 payload 无此尾巴时解码端回退到 fallback(默认开启)。
		payload.Add(snapshot.ModEnabled ? 1 : 0);
		payload.Add(snapshot.ChaosRuneChancePercent);
	}

	private static bool TryDecodeRunConfigurationSnapshot(
		List<int> payload,
		int cursor,
		HextechRunConfigurationSnapshot fallback,
		out HextechRunConfigurationSnapshot snapshot)
	{
		snapshot = fallback;
		if (payload.Count <= cursor)
		{
			return true;
		}

		int snapshotVersion = payload[cursor];
		if (snapshotVersion != RunConfigurationSnapshotVersion
			&& snapshotVersion != PreviousSingleRarityRunConfigurationSnapshotVersion
			&& snapshotVersion != PreviousRunConfigurationSnapshotVersion
			&& snapshotVersion != LegacyRerollRunConfigurationSnapshotVersion
			&& snapshotVersion != LegacyRunConfigurationSnapshotVersion)
		{
			return true;
		}

		cursor++;
		int fixedIntCount = snapshotVersion switch
		{
			RunConfigurationSnapshotVersion => 3 + 3 + 2 + 9 + 1 + 1 + 3 + 1 + 1,
			PreviousSingleRarityRunConfigurationSnapshotVersion => 3 + 3 + 2 + 3 + 1 + 1 + 3 + 1 + 1,
			PreviousRunConfigurationSnapshotVersion => 3 + 3 + 2 + 3 + 1 + 3 + 1 + 1,
			LegacyRerollRunConfigurationSnapshotVersion => 3 + 3 + 2 + 3 + 3 + 3 + 3 + 1 + 1,
			_ => 3 + 3 + 3 + 3 + 3 + 3 + 1
		};
		if (!HasRemaining(payload, cursor, fixedIntCount))
		{
			return false;
		}

		int[] playerHexCounts = HextechPlayerHexCountState.Normalize(payload.Skip(cursor).Take(3).ToArray());
		cursor += 3;
		int[] enemyHexCounts = HextechEnemyHexCountState.Normalize(payload.Skip(cursor).Take(3).ToArray());
		cursor += 3;
		int playerRuneRerollLimit = fallback.PlayerRuneRerollLimit;
		int monsterHexRerollLimit = fallback.MonsterHexRerollLimit;
		if (snapshotVersion is RunConfigurationSnapshotVersion or PreviousSingleRarityRunConfigurationSnapshotVersion or PreviousRunConfigurationSnapshotVersion or LegacyRerollRunConfigurationSnapshotVersion)
		{
			playerRuneRerollLimit = HextechRuneConfiguration.ClampRerollLimit(payload[cursor++]);
			monsterHexRerollLimit = HextechRuneConfiguration.ClampRerollLimit(payload[cursor++]);
		}

		HextechRarityWeights[] runeWeightsByAct;
		bool preventConsecutiveSilverRunes;
		int goldenRerollChancePercent = HextechRuneConfiguration.GetDefaultGoldenRerollChancePercent();
		if (snapshotVersion == RunConfigurationSnapshotVersion)
		{
			runeWeightsByAct =
			[
				ReadRarityWeights(payload, ref cursor),
				ReadRarityWeights(payload, ref cursor),
				ReadRarityWeights(payload, ref cursor)
			];
			preventConsecutiveSilverRunes = payload[cursor++] != 0;
			goldenRerollChancePercent = HextechRuneConfiguration.ClampGoldenRerollChancePercent(payload[cursor++]);
		}
		else if (snapshotVersion is PreviousSingleRarityRunConfigurationSnapshotVersion or PreviousRunConfigurationSnapshotVersion)
		{
			HextechRarityWeights singleWeights = ReadRarityWeights(payload, ref cursor);
			runeWeightsByAct = [ singleWeights, singleWeights, singleWeights ];
			preventConsecutiveSilverRunes = payload[cursor++] != 0;
			if (snapshotVersion == PreviousSingleRarityRunConfigurationSnapshotVersion)
			{
				goldenRerollChancePercent = HextechRuneConfiguration.ClampGoldenRerollChancePercent(payload[cursor++]);
			}
		}
		else
		{
			_ = ReadRarityWeights(payload, ref cursor);
			HextechRarityWeights legacyNormalWeights = ReadRarityWeights(payload, ref cursor);
			_ = ReadRarityWeights(payload, ref cursor);
			runeWeightsByAct = [ legacyNormalWeights, legacyNormalWeights, legacyNormalWeights ];
			preventConsecutiveSilverRunes = HextechRuneConfiguration.GetDefaultPreventConsecutiveSilverRunes();
		}

		HextechForgeRarityWeights forgeWeights = ReadForgeRarityWeights(payload, ref cursor);
		int forgePrice = payload[cursor++];
		bool randomForgeDirectGrant = fallback.RandomForgeDirectGrant;
		if (snapshotVersion is RunConfigurationSnapshotVersion or PreviousSingleRarityRunConfigurationSnapshotVersion or PreviousRunConfigurationSnapshotVersion or LegacyRerollRunConfigurationSnapshotVersion)
		{
			randomForgeDirectGrant = payload[cursor++] != 0;
		}

		if (payload.Count <= cursor)
		{
			return false;
		}

		int disabledMonsterHexCount = payload[cursor++];
		if (disabledMonsterHexCount < 0
			|| disabledMonsterHexCount > MaxDisabledMonsterHexes
			|| !HasRemaining(payload, cursor, disabledMonsterHexCount))
		{
			return false;
		}

		HashSet<string> disabledMonsterHexIds = [];
		for (int i = 0; i < disabledMonsterHexCount; i++)
		{
			int value = payload[cursor + i];
			if (Enum.IsDefined(typeof(MonsterHexKind), value))
			{
				disabledMonsterHexIds.Add(((MonsterHexKind)value).ToString());
			}
		}

		cursor += disabledMonsterHexCount;
		if (!HextechStableModelIdListCodec.TryDecode(payload, cursor, out List<ModelId> disabledForgeIds, out int forgeListNextCursor))
		{
			return false;
		}

		// 模组总开关:尾部可选 int。旧 payload 没有这一项时回退到 fallback(默认开启)。
		bool modEnabled = fallback.ModEnabled;
		if (payload.Count > forgeListNextCursor)
		{
			modEnabled = payload[forgeListNextCursor] != 0;
		}

		snapshot = HextechRuneConfiguration.NormalizeSnapshot(new HextechRunConfigurationSnapshot(
			playerHexCounts,
			enemyHexCounts,
			playerRuneRerollLimit,
			monsterHexRerollLimit,
			fallback.DisabledPlayerRuneIds,
			disabledMonsterHexIds,
			disabledForgeIds.Select(static id => id.Entry).ToHashSet(StringComparer.Ordinal),
			runeWeightsByAct,
			preventConsecutiveSilverRunes,
			goldenRerollChancePercent,
			forgeWeights,
			forgePrice,
			randomForgeDirectGrant,
			modEnabled,
			payload.Count > forgeListNextCursor + 1 ? payload[forgeListNextCursor + 1] : 33));
		return true;
	}

	private static void AppendRarityWeights(List<int> payload, HextechRarityWeights weights)
	{
		payload.Add(HextechRuneConfiguration.ClampRarityWeight(weights.Silver));
		payload.Add(HextechRuneConfiguration.ClampRarityWeight(weights.Gold));
		payload.Add(HextechRuneConfiguration.ClampRarityWeight(weights.Prismatic));
	}

	private static void AppendForgeRarityWeights(List<int> payload, HextechForgeRarityWeights weights)
	{
		payload.Add(HextechRuneConfiguration.ClampRarityWeight(weights.Silver));
		payload.Add(HextechRuneConfiguration.ClampRarityWeight(weights.Gold));
		payload.Add(HextechRuneConfiguration.ClampRarityWeight(weights.Prismatic));
	}

	private static HextechRarityWeights ReadRarityWeights(List<int> payload, ref int cursor)
	{
		HextechRarityWeights weights = new(payload[cursor], payload[cursor + 1], payload[cursor + 2]);
		cursor += 3;
		return weights;
	}

	private static HextechForgeRarityWeights ReadForgeRarityWeights(List<int> payload, ref int cursor)
	{
		HextechForgeRarityWeights weights = new(payload[cursor], payload[cursor + 1], payload[cursor + 2]);
		cursor += 3;
		return weights;
	}
}
