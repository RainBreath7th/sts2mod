using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Relics;
using System.Text.Json;

namespace HextechRunes.Tests;

internal static partial class Program
{
	private sealed class GeneratedTestRelic : RelicModel, IHextechGeneratedRune
	{
		public override RelicRarity Rarity => RelicRarity.Event;
		public string Data { get; set; } = "";
		public string ExportSelectionData() => Data;
		public bool TryImportSelectionData(string data) { Data = data; return data.StartsWith("recipe:", StringComparison.Ordinal); }
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

	private static void ChaosChanceConfigurationRoundTripsAndDefaults()
	{
		HextechRunConfigurationSnapshot defaults = HextechRuneConfiguration.GetDefaultSnapshot();
		Equal(33, defaults.ChaosRuneChancePercent, "default chance");
		HextechRunConfigurationSnapshot snapshot = defaults with { ChaosRuneChancePercent = 73 };
		PlayerChoiceResult roll = HextechChoiceCodec.CreateActRoll(0, HextechRarityTier.Gold, null, false,
			snapshot.EnemyHexCountsByAct, snapshot.DisabledPlayerRuneIds, snapshot);
		Expect(HextechChoiceCodec.TryDecodeActRoll(roll, 0, out _, out _, out _, out _, out _, out HextechRunConfigurationSnapshot decoded), "snapshot decodes");
		Equal(73, decoded.ChaosRuneChancePercent, "host chance wins");
		Equal(73, HextechConfigShareCodec.TryParseForTests(HextechConfigShareCodec.Export(snapshot), defaults)!.Snapshot.ChaosRuneChancePercent, "share code preserves chance");
		Equal(0, HextechRuneConfiguration.NormalizeSnapshot(snapshot with { ChaosRuneChancePercent = -1 }).ChaosRuneChancePercent, "lower bound");
		Equal(100, HextechRuneConfiguration.NormalizeSnapshot(snapshot with { ChaosRuneChancePercent = 101 }).ChaosRuneChancePercent, "upper bound");
		string json = JsonSerializer.Serialize(snapshot);
		Equal(73, JsonSerializer.Deserialize<HextechRunConfigurationSnapshot>(json)!.ChaosRuneChancePercent, "save JSON preserves chance");
		json = json.Replace(",\"ChaosRuneChancePercent\":73", "");
		Equal(33, JsonSerializer.Deserialize<HextechRunConfigurationSnapshot>(json)!.ChaosRuneChancePercent, "old JSON default");
	}
}
