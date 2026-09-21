using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using HextechRunes;
using FormVfxKind = HextechRunes.HextechFormVfxSafetyHooks.FormVfxKind;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using System.Text.Json;

namespace HextechRunes.Tests;

internal static partial class Program
{
	private static void ActRollRoundTripKeepsHostSnapshot()
	{
		ModelId disabledRune = HextechCatalog.GetConfigurablePlayerRuneIds()
			.OrderBy(static id => id.Entry, StringComparer.Ordinal)
			.First();
		HashSet<string> disabledIds = [ disabledRune.Entry ];
		string disabledForgeId = HextechCatalog.GetAllForgeTypes()
			.Select(ModelDb.GetId)
			.OrderBy(static id => id.Entry, StringComparer.Ordinal)
			.First()
			.Entry;
		HextechRunConfigurationSnapshot snapshot = HextechRuneConfiguration.GetDefaultSnapshot() with
		{
			PlayerHexCountsByAct = [ 2, 0, 8 ],
			EnemyHexCountsByAct = [ -1, 7, 3 ],
			DisabledPlayerRuneIds = disabledIds,
			DisabledMonsterHexIds = [ MonsterHexKind.FrostWraith.ToString() ],
			DisabledForgeIds = [ disabledForgeId ],
			RuneRarityWeightsByAct =
			[
				new HextechRarityWeights(4, 5, 6),
				new HextechRarityWeights(7, 8, 9),
				new HextechRarityWeights(10, 11, 12)
			],
			PreventConsecutiveSilverRunes = false,
			GoldenRerollChancePercent = 37,
			ForgeRarityWeights = new HextechForgeRarityWeights(9, 10, 11),
			RandomForgeShopPrice = 123,
			PlayerRuneRerollLimit = 8,
			MonsterHexRerollLimit = HextechRuneConfiguration.InfiniteRerollLimit
		};

		PlayerChoiceResult result = HextechChoiceCodec.CreateActRoll(
			actIndex: 1,
			rarity: HextechRarityTier.Gold,
			monsterHex: MonsterHexKind.ShrinkRay,
			hostUsesBetterMultiplayerScaling: true,
			enemyHexCountsByAct: [ -1, 7, 3 ],
			disabledPlayerRuneIds: disabledIds,
			runConfigurationSnapshot: snapshot);

		Expect(HextechChoiceCodec.TryDecodeActRoll(
			result,
			expectedActIndex: 1,
			out HextechRarityTier rarity,
			out MonsterHexKind? monsterHex,
			out bool hostUsesBetterMultiplayerScaling,
			out int[] enemyHexCountsByAct,
			out HashSet<string> decodedDisabledIds,
			out HextechRunConfigurationSnapshot decodedSnapshot), "act roll should decode");

		Equal(HextechRarityTier.Gold, rarity, "rarity");
		Equal(MonsterHexKind.ShrinkRay, monsterHex, "monster hex");
		Equal(true, hostUsesBetterMultiplayerScaling, "host scaling flag");
		SequenceEqual(new[] { 0, 6, 3 }, enemyHexCountsByAct, "enemy count snapshot");
		Expect(decodedDisabledIds.Contains(disabledRune.Entry), "disabled player rune id should round-trip");
		SequenceEqual(new[] { 2, 0, 6 }, decodedSnapshot.PlayerHexCountsByAct, "player count snapshot");
		SetEqual([ MonsterHexKind.FrostWraith.ToString() ], decodedSnapshot.DisabledMonsterHexIds, "disabled monster hex ids");
		SetEqual([ disabledForgeId ], decodedSnapshot.DisabledForgeIds, "disabled forge ids");
		Equal(123, decodedSnapshot.RandomForgeShopPrice, "forge shop price");
		Equal(8, decodedSnapshot.PlayerRuneRerollLimit, "player reroll limit");
		Equal(HextechRuneConfiguration.InfiniteRerollLimit, decodedSnapshot.MonsterHexRerollLimit, "monster reroll limit");
		SequenceEqual(
			new[]
			{
				new HextechRarityWeights(4, 5, 6),
				new HextechRarityWeights(7, 8, 9),
				new HextechRarityWeights(10, 11, 12)
			},
			decodedSnapshot.RuneRarityWeightsByAct,
			"rune rarity weights by act");
		Equal(false, decodedSnapshot.PreventConsecutiveSilverRunes, "prevent consecutive Silver toggle");
		Equal(37, decodedSnapshot.GoldenRerollChancePercent, "golden reroll chance");
		Equal(10, decodedSnapshot.ForgeRarityWeights.Gold, "forge rarity weight");
		Expect(!HextechChoiceCodec.TryDecodeActRoll(result, 0, out _, out _, out _, out _, out _), "wrong act should be rejected");
	}

	private static void RuneSelectionRoundTripRequiresMatchingActAndOrdinal()
	{
		RelicModel[] finalOptions = CreateRuneSelectionTestOptions(3);
		ModelId[] finalOptionIds = finalOptions
			.Select(static relic => relic.CanonicalInstance?.Id ?? relic.Id)
			.ToArray();
		PlayerChoiceResult result = HextechChoiceCodec.CreateRuneSelection(
			actIndex: 1,
			choiceOrdinal: 2,
			selectedIndex: 1,
			rerollHistory: [ 2, 0 ],
			finalOptions);

		Expect(HextechChoiceCodec.IsRuneSelection(result), "rune selection kind predicate should decode");
		Expect(HextechChoiceCodec.IsRuneSelection(result, 1, 2), "matching rune selection act and ordinal should decode");
		Expect(HextechChoiceCodec.TryDecodeRuneSelection(result, 1, 2, out int selectedIndex, out List<int> rerollHistory, out List<ModelId> decodedFinalOptionIds), "matching rune selection should decode");
		Equal(1, selectedIndex, "selected index");
		SequenceEqual(new[] { 2, 0 }, rerollHistory, "reroll history");
		SequenceEqual(finalOptionIds, decodedFinalOptionIds, "final option ids");
	}

	private static void RuneSelectionRejectsWrongActOrOrdinal()
	{
		PlayerChoiceResult result = HextechChoiceCodec.CreateRuneSelection(
			actIndex: 1,
			choiceOrdinal: 2,
			selectedIndex: 0,
			rerollHistory: [],
			CreateRuneSelectionTestOptions(3));

		Expect(!HextechChoiceCodec.TryDecodeRuneSelection(result, 0, 2, out _, out _, out _), "wrong rune selection act should be rejected");
		Expect(!HextechChoiceCodec.TryDecodeRuneSelection(result, 1, 1, out _, out _, out _), "wrong rune selection ordinal should be rejected");

		PlayerChoiceResult malformed = PlayerChoiceResult.FromIndexes(new List<int> { Magic, ChoiceKindRuneSelection, 1, 2, 0, 2, 0 });
		Expect(!HextechChoiceCodec.TryDecodeRuneSelection(malformed, 1, 2, out _, out _, out _), "malformed rune selection should be rejected");
	}

	private static void ActSelectionAppliedRejectsWrongActOrOrdinal()
	{
		PlayerChoiceResult result = HextechChoiceCodec.CreateActSelectionApplied(2, 3);

		Expect(HextechChoiceCodec.TryDecodeActSelectionApplied(result, 2, 3), "matching act and ordinal should decode");
		Expect(!HextechChoiceCodec.TryDecodeActSelectionApplied(result, 1, 3), "wrong act should be rejected");
		Expect(!HextechChoiceCodec.TryDecodeActSelectionApplied(result, 2, 2), "wrong ordinal should be rejected");

		PlayerChoiceResult malformed = PlayerChoiceResult.FromIndexes(new List<int> { Magic, ChoiceKindActSelectionApplied, 2, 3, 0 });
		Expect(!HextechChoiceCodec.TryDecodeActSelectionApplied(malformed, 2, 3), "missing applied flag should be rejected");
	}

	private static void EnemyHexAdjustmentRoundTripKeepsAllSlots()
	{
		const int OperationToken = 112233;
		EnemyHexAdjustmentPayload source = new(
			ActIndex: 0,
			Sequence: 12,
			MonsterHexes:
			[
				MonsterHexKind.FrostWraith,
				null,
				MonsterHexKind.PandorasBox
			],
			RerollCounts: [ 2, -3 ],
			IsFinal: true);

		PlayerChoiceResult result = HextechChoiceCodec.CreateEnemyHexAdjustment(OperationToken, source);

		Expect(HextechChoiceCodec.TryDecodeEnemyHexAdjustment(result, OperationToken, 0, 12, out EnemyHexAdjustmentPayload decoded), "enemy adjustment should decode");
		Equal(0, decoded.ActIndex, "act");
		Equal(12, decoded.Sequence, "sequence");
		Equal(true, decoded.IsFinal, "final flag");
		SequenceEqual(source.MonsterHexes, decoded.MonsterHexes, "monster hex slots");
		SequenceEqual(new[] { 2, 0, 0 }, decoded.RerollCounts, "reroll counts");
		Expect(!HextechChoiceCodec.TryDecodeEnemyHexAdjustment(result, OperationToken, 1, 12, out _), "wrong act should be rejected");
	}

	private static void EnemyHexAdjustmentRejectsInvalidHex()
	{
		const int OperationToken = 223344;
		PlayerChoiceResult result = PlayerChoiceResult.FromIndexes(new List<int>
		{
			Magic,
			ChoiceKindEnemyHexAdjustment,
			0,
			1,
			OperationToken,
			EnemyHexAdjustmentListVersion,
			0,
			1,
			int.MaxValue,
			0
		});

		Expect(!HextechChoiceCodec.TryDecodeEnemyHexAdjustment(result, OperationToken, 0, 1, out _), "invalid monster hex enum should be rejected");
	}

	private static void EnemyHexAdjustmentRejectsUnexpectedSequence()
	{
		const int OperationToken = 334455;
		EnemyHexAdjustmentPayload source = new(
			ActIndex: 1,
			Sequence: 3,
			MonsterHexes: [ MonsterHexKind.FrostWraith ],
			RerollCounts: [ 0 ],
			IsFinal: false);
		PlayerChoiceResult result = HextechChoiceCodec.CreateEnemyHexAdjustment(OperationToken, source);

		Expect(
			HextechChoiceCodec.TryDecodeEnemyHexAdjustment(result, OperationToken, 1, 3, out _),
			"exact enemy adjustment sequence should decode");
		Expect(
			!HextechChoiceCodec.TryDecodeEnemyHexAdjustment(result, OperationToken, 1, 2, out _),
			"stale enemy adjustment sequence should be rejected");
		Expect(
			!HextechChoiceCodec.TryDecodeEnemyHexAdjustment(result, OperationToken, 1, 4, out _),
			"future enemy adjustment sequence should be rejected");
	}

	private static void EnemyHexAdjustmentRejectsExtremeCounts()
	{
		const int OperationToken = 445566;
		PlayerChoiceResult extremeHexCount = PlayerChoiceResult.FromIndexes(
		[
			Magic,
			ChoiceKindEnemyHexAdjustment,
			0,
			0,
			OperationToken,
			EnemyHexAdjustmentListVersion,
			0,
			int.MaxValue
		]);
		Expect(
			!HextechChoiceCodec.TryDecodeEnemyHexAdjustment(extremeHexCount, OperationToken, 0, 0, out _),
			"extreme enemy hex count should be rejected without allocation");

		PlayerChoiceResult extremeRerollCount = PlayerChoiceResult.FromIndexes(
		[
			Magic,
			ChoiceKindEnemyHexAdjustment,
			0,
			0,
			OperationToken,
			EnemyHexAdjustmentListVersion,
			0,
			0,
			int.MaxValue
		]);
		Expect(
			!HextechChoiceCodec.TryDecodeEnemyHexAdjustment(extremeRerollCount, OperationToken, 0, 0, out _),
			"extreme enemy reroll count should be rejected without allocation");
	}

	private static void LegacyEnemyHexAdjustmentIsRejected()
	{
		PlayerChoiceResult result = PlayerChoiceResult.FromIndexes(new List<int>
		{
			Magic,
			ChoiceKindEnemyHexAdjustment,
			1,
			9,
			0,
			(int)MonsterHexKind.FrostWraith,
			2,
			1
		});

		Expect(
			!HextechChoiceCodec.TryDecodeEnemyHexAdjustment(result, 556677, 1, 9, out _),
			"legacy enemy adjustment payload should be rejected after the protocol gate");
	}

	private static void RandomRuneGrantRoundTripKeepsStableModelIds()
	{
		const int OperationToken = 667788;
		ModelId[] source =
		[
			new("HEXTECH_TEST", "FIRST_RUNE"),
			new("HEXTECH_TEST", "SECOND_RUNE")
		];

		PlayerChoiceResult result = HextechChoiceCodec.CreateRandomRuneGrant(OperationToken, source);

		Expect(HextechChoiceCodec.TryDecodeRandomRuneGrant(result, OperationToken, out List<ModelId> decoded), "random grant should decode");
		SequenceEqual(source, decoded, "stable model ids");
		Expect(HextechChoiceCodec.IsRandomRuneGrant(result, OperationToken), "random grant predicate");
	}

	private static void RandomRuneGrantRejectsMalformedStableModelIdList()
	{
		const int OperationToken = 778899;
		PlayerChoiceResult tooManyIds = PlayerChoiceResult.FromIndexes(new List<int>
		{
			Magic,
			ChoiceKindRandomRuneGrant,
			OperationToken,
			StableModelIdListVersion,
			65
		});

		Expect(!HextechChoiceCodec.TryDecodeRandomRuneGrant(tooManyIds, OperationToken, out _), "oversized stable id list should be rejected");

		PlayerChoiceResult badSerializedId = PlayerChoiceResult.FromIndexes(new List<int>
		{
			Magic,
			ChoiceKindRandomRuneGrant,
			OperationToken,
			StableModelIdListVersion,
			1,
			3,
			'B',
			'A',
			'D'
		});

		Expect(!HextechChoiceCodec.TryDecodeRandomRuneGrant(badSerializedId, OperationToken, out _), "malformed model id should be rejected");

		PlayerChoiceResult runeSelectionWithOutOfRangeLegacyOrdinal = PlayerChoiceResult.FromIndexes(new List<int>
		{
			Magic,
			ChoiceKindRuneSelection,
			1,
			2,
			0,
			0,
			1,
			int.MaxValue
		});
		Expect(
			!HextechChoiceCodec.TryDecodeRuneSelection(runeSelectionWithOutOfRangeLegacyOrdinal, 1, 2, out _, out _, out _),
			"out-of-range legacy rune selection ordinal should be rejected");

		PlayerChoiceResult forgeSelectionWithOutOfRangeLegacyOrdinal = PlayerChoiceResult.FromIndexes(new List<int>
		{
			Magic,
			ChoiceKindForgeSelection,
			OperationToken,
			0,
			1,
			int.MaxValue
		});
		Expect(
			!HextechChoiceCodec.TryDecodeForgeSelection(forgeSelectionWithOutOfRangeLegacyOrdinal, OperationToken, out _, out _),
			"out-of-range legacy forge selection ordinal should be rejected");
		Expect(
			HextechChoiceCodec.IsMalformedForgeSelectionEnvelope(forgeSelectionWithOutOfRangeLegacyOrdinal, OperationToken),
			"malformed forge selection envelope should remain identifiable");

		PlayerChoiceResult malformedRelicOptionSelection = PlayerChoiceResult.FromIndexes(new List<int>
		{
			Magic,
			ChoiceKindRelicOptionSelection,
			OperationToken,
			0,
			StableModelIdListVersion
		});
		Expect(
			HextechChoiceCodec.IsMalformedRelicOptionSelectionEnvelope(malformedRelicOptionSelection, OperationToken),
			"malformed relic option envelope should remain identifiable");

		PlayerChoiceResult randomGrantWithOutOfRangeLegacyOrdinal = PlayerChoiceResult.FromIndexes(new List<int>
		{
			Magic,
			ChoiceKindRandomRuneGrant,
			OperationToken,
			1,
			int.MaxValue
		});
		Expect(
			!HextechChoiceCodec.TryDecodeRandomRuneGrant(randomGrantWithOutOfRangeLegacyOrdinal, OperationToken, out _),
			"out-of-range legacy random grant ordinal should be rejected");
	}

	private static void RelicOptionSelectionRoundTripRequiresMatchingOptions()
	{
		const int OperationToken = 889900;
		RelicModel[] options = CreateRuneSelectionTestOptions(2);
		ModelId[] optionIds = options
			.Select(static relic => relic.CanonicalInstance?.Id ?? relic.Id)
			.ToArray();
		PlayerChoiceResult result = HextechChoiceCodec.CreateRelicOptionSelection(OperationToken, 1, options);

		Expect(HextechChoiceCodec.IsRelicOptionSelection(result, OperationToken, options), "matching relic option selection should be expected");
		Expect(HextechChoiceCodec.TryDecodeRelicOptionSelection(result, OperationToken, out int selectedIndex, out List<ModelId> decodedOptionIds), "relic option selection should decode");
		Equal(1, selectedIndex, "selected relic option index");
		SequenceEqual(optionIds, decodedOptionIds, "relic option ids");
		Expect(!HextechChoiceCodec.IsRelicOptionSelection(result, OperationToken, options.Reverse().ToArray()), "reordered relic options should not be expected");
		Expect(!HextechChoiceCodec.IsRelicOptionSelection(result, OperationToken, CreateRuneSelectionTestOptions(3)), "different relic option count should not be expected");
	}

	private static void GeneratedRuneSelectionPreservesInstanceDataAndRejectsTruncation()
	{
		RelicModel[] options = [new GeneratedTestRelic { Data = "recipe:first" }, new GeneratedTestRelic { Data = "recipe:second" }];
		Expect(!HextechSelectionHelpers.SameRuneCandidate(options[0], options[1]), "same carrier with new recipe is a real reroll");
		Expect(HextechSelectionHelpers.SameRuneCandidate(options[0], new GeneratedTestRelic { Data = "recipe:first" }), "same recipe reconstruction is unchanged");
		PlayerChoiceResult choice = HextechChoiceCodec.CreateRuneSelection(1, 2, 1, [0, 1], options);
		RelicModel[] restored = [new GeneratedTestRelic(), new GeneratedTestRelic()];
		Expect(HextechGeneratedRuneDataCodec.Restore(choice, restored), "generated data restores");
		Equal("recipe:second", ((GeneratedTestRelic)restored[1]).Data, "same model ID keeps separate recipes");
		List<int> truncated = choice.AsIndexes().ToList();
		truncated.RemoveAt(truncated.Count - 1);
		Expect(!HextechChoiceCodec.TryDecodeRuneSelection(PlayerChoiceResult.FromIndexes(truncated), 1, 2, out _, out _, out _), "truncated recipe must fail");
		List<int> missing = [];
		HextechStableModelIdListCodec.Append(missing, options.Select(static option => option.Id));
		PlayerChoiceResult old = PlayerChoiceResult.FromIndexes(new[] { Magic, 2, 1, 2, 1, 0 }.Concat(missing).ToList());
		Expect(!HextechGeneratedRuneDataCodec.Restore(old, restored), "missing generated data must not reroll");
		Expect(HextechGeneratedRuneDataCodec.Restore(HextechChoiceCodec.CreateRuneSelection(1, 2, 0, [], CreateRuneSelectionTestOptions(2)), CreateRuneSelectionTestOptions(2)), "ordinary choices keep working");

		HextechRuneSelectionJournalState journal = new();
		journal.RecordSelected(1, 2, 7, options[1].Id, "recipe:second");
		HextechRuneSelectionJournalState recovered = new();
		recovered.Restore(journal.Serialize());
		Expect(recovered.TryGet(1, 2, 7, out HextechRuneSelectionJournalEntry entry), "journal entry restored");
		Equal("recipe:second", entry.SelectionData, "pending checkpoint keeps instance data");
		Expect(!entry.Applied, "pending recipe still needs obtain");
	}

	private static void OperationTokensRejectCrossedPayloads()
	{
		const uint ChoiceId = 42;
		const ulong PlayerNetId = 9001;
		int forgeToken = HextechChoiceCodec.ComputeOperationToken(
			"forge-selection",
			ChoiceId,
			PlayerNetId,
			"source:0");
		int sameForgeToken = HextechChoiceCodec.ComputeOperationToken(
			"forge-selection",
			ChoiceId,
			PlayerNetId,
			"source:0");
		int crossedForgeToken = HextechChoiceCodec.ComputeOperationToken(
			"forge-selection",
			ChoiceId,
			PlayerNetId,
			"source:1");
		Equal(forgeToken, sameForgeToken, "operation token must be stable");
		Expect(forgeToken != crossedForgeToken, "different stable contexts should produce different operation tokens");

		RelicModel[] options = CreateRuneSelectionTestOptions(2);
		PlayerChoiceResult forge = HextechChoiceCodec.CreateForgeSelection(forgeToken, 0, options);
		Expect(HextechChoiceCodec.TryDecodeForgeSelection(forge, forgeToken, out _, out _), "matching forge operation should decode");
		Expect(!HextechChoiceCodec.TryDecodeForgeSelection(forge, crossedForgeToken, out _, out _), "crossed forge operation should be rejected");

		int relicToken = HextechChoiceCodec.ComputeOperationToken(
			"relic-option-selection",
			ChoiceId,
			PlayerNetId,
			"relic-source");
		PlayerChoiceResult relic = HextechChoiceCodec.CreateRelicOptionSelection(relicToken, 1, options);
		Expect(!HextechChoiceCodec.TryDecodeRelicOptionSelection(relic, relicToken + 1, out _, out _), "crossed relic operation should be rejected");

		int randomToken = HextechChoiceCodec.ComputeOperationToken(
			"random-rune-grant",
			ChoiceId,
			PlayerNetId,
			"consume:HEXTECH_TEST:RUNE");
		PlayerChoiceResult random = HextechChoiceCodec.CreateRandomRuneGrant(
			randomToken,
			[ new ModelId("HEXTECH_TEST", "RUNE") ]);
		Expect(!HextechChoiceCodec.TryDecodeRandomRuneGrant(random, randomToken + 1, out _), "crossed random grant operation should be rejected");

		int enemyToken = HextechChoiceCodec.ComputeOperationToken(
			"enemy-hex-adjustment",
			ChoiceId,
			PlayerNetId,
			"act=1;sequence=2");
		EnemyHexAdjustmentPayload enemyPayload = new(
			ActIndex: 1,
			Sequence: 2,
			MonsterHexes: [ MonsterHexKind.FrostWraith ],
			RerollCounts: [ 0 ],
			IsFinal: true);
		PlayerChoiceResult enemy = HextechChoiceCodec.CreateEnemyHexAdjustment(enemyToken, enemyPayload);
		Expect(
			!HextechChoiceCodec.TryDecodeEnemyHexAdjustment(enemy, enemyToken + 1, 1, 2, out _),
			"crossed enemy adjustment operation should be rejected");
	}

	private static void NetworkChoiceTimeoutUsesNominalWallClockSeconds()
	{
		Equal(TimeSpan.Zero, HextechRuneSelectionCoordinator.GetNetworkChoiceTimeoutDuration(0), "zero timeout");
		Equal(TimeSpan.FromSeconds(10), HextechRuneSelectionCoordinator.GetNetworkChoiceTimeoutDuration(600), "ack timeout");
		Equal(TimeSpan.FromMinutes(10), HextechRuneSelectionCoordinator.GetNetworkChoiceTimeoutDuration(36000), "selection timeout");
	}

	private static void StableModelIdListCodecRoundTripsFromNonzeroCursor()
	{
		ModelId[] source =
		[
			new("HEXTECH_TEST", "FIRST"),
			new("HEXTECH_TEST", "SECOND")
		];
		List<int> payload = [ 17, 23 ];

		HextechStableModelIdListCodec.Append(payload, source);

		Expect(HextechStableModelIdListCodec.TryDecode(payload, 2, out List<ModelId> decoded, out int nextCursor), "stable model id list should decode");
		SequenceEqual(source, decoded, "stable model id helper round-trip");
		Equal(payload.Count, nextCursor, "stable model id helper next cursor");
	}

	private static void StableModelIdListCodecRejectsMalformedLength()
	{
		List<int> payload =
		[
			HextechStableModelIdListCodec.Version,
			1,
			129
		];

		Expect(!HextechStableModelIdListCodec.TryDecode(payload, 0, out List<ModelId> decoded, out int nextCursor), "oversized stable model id length should be rejected");
		Expect(decoded.Count == 0, "malformed stable model id list should not keep partial ids");
		Equal(0, nextCursor, "failed stable model id decode should keep original cursor");
	}

	private static void StableModelIdListCodecRejectsEncoderOverflow()
	{
		ModelId id = new("HEXTECH_TEST", "ENTRY");
		ExpectThrows<ArgumentOutOfRangeException>(
			() => HextechStableModelIdListCodec.Append(
				[],
				Enumerable.Repeat(id, HextechStableModelIdListCodec.MaxCount + 1)),
			"stable ModelId encoder should reject more than 64 items");

		ModelId oversized = new(
			new string('C', 64),
			new string('E', HextechStableModelIdListCodec.MaxSerializedLength));
		ExpectThrows<ArgumentException>(
			() => HextechStableModelIdListCodec.Append([], [ oversized ]),
			"stable ModelId encoder should reject a serialized ID longer than 128 characters");

		Expect(
			!HextechStableModelIdListCodec.TryDecode(
				[ StableModelIdListVersion, 0 ],
				-1,
				out _,
				out _),
			"stable ModelId decoder should reject a negative cursor");
	}
}
