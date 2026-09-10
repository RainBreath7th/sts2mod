using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Models;

namespace HextechRunes.Tests;

internal static partial class Program
{
	private static void CharacterWeightUsesSequentialAdditiveSteps()
	{
		RelicModel[] candidates = [new BerserkRune(), new JudicatorRune()];
		Dictionary<string, int> tags = [];
		int weight = HextechWeightedRuneOptions.InitialCharacterWeightPercent;
		List<int> weights = HextechRunePoolBuilder.BuildSelectionWeights(candidates, tags, false, PlayerRuneCharacterPool.Ironclad, weight, out int total);
		SequenceEqual(new[] {15000, 10000}, weights, "initial relative weights are 150% and 100%");
		Equal(25000, total, "weighted pool total");
		Equal(0, HextechRunePoolBuilder.SelectWeightedIndex(weights, 14999), "character interval end");
		Equal(1, HextechRunePoolBuilder.SelectWeightedIndex(weights, 15000), "generic remains possible in the first slot");
		weight = HextechWeightedRuneOptions.Advance(weight, false);
		Equal(160, weight, "generic adds ten percentage points, not ten percent multiplication");
		weights = HextechRunePoolBuilder.BuildSelectionWeights(candidates, tags, false, PlayerRuneCharacterPool.Ironclad, weight, out _);
		SequenceEqual(new[] {16000, 10000}, weights, "next draw immediately uses increased weight");
		weight = HextechWeightedRuneOptions.Advance(weight, true);
		Equal(150, weight, "character subtracts ten without resetting");
		Equal(140, HextechWeightedRuneOptions.Advance(weight, true), "success can reduce weight below initial value");
		for (int i = 0; i < 20; i++) weight = HextechWeightedRuneOptions.Advance(weight, true);
		Equal(0, weight, "lower bound prevents negative weights");
		weights = HextechRunePoolBuilder.BuildSelectionWeights(candidates, tags, false, PlayerRuneCharacterPool.Ironclad, weight, out total);
		SequenceEqual(new[] {0, 10000}, weights, "zero weight skips characters when generic candidates exist");
		Equal(1, HextechRunePoolBuilder.SelectWeightedIndex(weights, 0), "zero weight cannot win");
		Equal(10, HextechWeightedRuneOptions.Advance(weight, false), "generic recovers from zero");
		HextechRunePoolBuilder.BuildSelectionWeights([candidates[0]], tags, false, PlayerRuneCharacterPool.Ironclad, 0, out total);
		Expect(total > 0, "character-only configurations cannot deadlock at zero");
		weights = HextechRunePoolBuilder.BuildSelectionWeights(candidates, tags, false, null, 9990, out _);
		SequenceEqual(new[] {10000, 10000}, weights, "unmapped characters use generic weights");
		tags[HextechCatalog.GetPlayerRuneTagKey(candidates[0])] = 1;
		weights = HextechRunePoolBuilder.BuildSelectionWeights([candidates[0]], tags, false, PlayerRuneCharacterPool.Ironclad, 150, out _);
		Equal(18750, weights[0], "existing tag bonus multiplies without truncating half points");
	}

	private static void CharacterWeightSaveAndReplayArePerPlayer()
	{
		HextechRuneSelectionJournalState host = new();
		Equal(150, host.GetCharacterWeight(11), "new run default");
		host.CommitCharacterWeight(22, 190);
		host.CommitCharacterWeight(11, 120);
		host.CommitCharacterWeight(11, 120);
		HextechRuneSelectionJournalState client = new();
		client.CommitCharacterWeight(11, 120);
		client.CommitCharacterWeight(22, 190);
		Equal(host.Serialize(), client.Serialize(), "player completion order and repeated commits do not affect shared serialization");
		HextechRuneSelectionJournalState restored = new();
		restored.Restore(host.Serialize());
		Equal(120, restored.GetCharacterWeight(11), "load retains first player progress");
		Equal(190, restored.GetCharacterWeight(22), "second player is independent");
		restored.Reset(preserveCharacterWeights: true);
		Equal(120, restored.GetCharacterWeight(11), "endless loop retains run progress");
		restored.Reset();
		Equal(150, restored.GetCharacterWeight(11), "new run resets weight");
		restored.Restore("{\"version\":1,\"entries\":[]}");
		Equal(150, restored.GetCharacterWeight(11), "old journal has no weight and uses initial value");
	}

	private static void CharacterWeightProtocolPreservesRerollProgressAndRecipes()
	{
		RelicModel[] models = CreateRuneSelectionTestOptions(3);
		HextechWeightedRuneOptions offered = new(models, 170);
		List<RelicModel> uiCopy = HextechWeightedRuneOptions.Copy(offered);
		Equal(170, HextechWeightedRuneOptions.GetWeight(uiCopy), "screen copy retains generation progress");
		HextechWeightedRuneOptions rerolled = new(uiCopy, 160);
		Equal(170, offered.CharacterWeightPercent, "reroll cannot mutate prior snapshot or saved state");
		PlayerChoiceResult result = HextechChoiceCodec.CreateRuneSelection(1, 2, 0, [0, 0, 1], rerolled);
		Expect(HextechChoiceCodec.TryDecodeRuneSelection(result, 1, 2, out _, out _, out _), "weighted selection envelope decodes");
		Expect(HextechRuneWeightCodec.TryRestore(result, models, out List<RelicModel> remote), "remote restores weight including replaced candidates");
		Equal(160, HextechWeightedRuneOptions.GetWeight(remote), "final options alone are insufficient; transmitted progress wins");
		Expect(HextechGeneratedRuneDataCodec.Restore(result, remote), "ordinary choices with weight have no recipe requirement");
		List<int> truncated = result.AsIndexes().ToList();
		truncated.RemoveAt(truncated.Count - 1);
		Expect(!HextechChoiceCodec.TryDecodeRuneSelection(PlayerChoiceResult.FromIndexes(truncated), 1, 2, out _, out _, out _), "truncated weight rejected");
		List<int> negative = result.AsIndexes().ToList();
		negative[^1] = -10;
		Expect(!HextechChoiceCodec.TryDecodeRuneSelection(PlayerChoiceResult.FromIndexes(negative), 1, 2, out _, out _, out _), "negative weight rejected");
		RelicModel[] generated = [new GeneratedTestRelic {Data = "recipe:weighted"}];
		result = HextechChoiceCodec.CreateRuneSelection(1, 2, 0, [], new HextechWeightedRuneOptions(generated, 180));
		RelicModel[] restored = [new GeneratedTestRelic()];
		Expect(HextechChoiceCodec.TryDecodeRuneSelection(result, 1, 2, out _, out _, out _), "weight and generated recipe coexist");
		Expect(HextechGeneratedRuneDataCodec.Restore(result, restored), "weight prefix does not consume recipe bytes");
		Equal("recipe:weighted", ((GeneratedTestRelic)restored[0]).Data, "generated recipe survives sync");
	}
}
