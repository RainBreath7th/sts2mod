namespace HextechRunes;

// 权重属于一次候选生成事务；UI 重掷只生成新快照，确认选择后才提交到本局状态。
// 复制候选列表时必须保留快照，不能从最后三个候选反推已经被替换的抽取结果。
internal sealed class HextechWeightedRuneOptions(IEnumerable<RelicModel> options, int characterWeightPercent)
	: List<RelicModel>(options)
{
	internal const int InitialCharacterWeightPercent = 150;
	internal const int WeightStep = 10;
	public int CharacterWeightPercent { get; } = characterWeightPercent;

	internal static int Advance(int current, bool isCharacterRune)
	{
		return isCharacterRune ? Math.Max(0, current - WeightStep) : checked(current + WeightStep);
	}

	internal static List<RelicModel> Copy(IReadOnlyList<RelicModel> options)
	{
		return options is HextechWeightedRuneOptions weighted
			? new HextechWeightedRuneOptions(options, weighted.CharacterWeightPercent)
			: options.ToList();
	}

	internal static int GetWeight(IReadOnlyList<RelicModel> options)
	{
		return options is HextechWeightedRuneOptions weighted
			? weighted.CharacterWeightPercent : InitialCharacterWeightPercent;
	}
}
