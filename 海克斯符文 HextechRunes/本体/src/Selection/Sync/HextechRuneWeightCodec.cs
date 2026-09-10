using MegaCrit.Sts2.Core.GameActions;

namespace HextechRunes;

// 候选 ID 之后、实例配方之前的可选尾部。旧存档默认从 150 开始，新联机选择必须带最终权重。
internal static class HextechRuneWeightCodec
{
	private const int Version = -11;

	internal static void Append(List<int> payload, IReadOnlyList<RelicModel> options)
	{
		if (options is not HextechWeightedRuneOptions weighted) return;
		payload.Add(Version);
		payload.Add(weighted.CharacterWeightPercent);
	}

	internal static bool TryRead(IReadOnlyList<int> payload, ref int cursor, out int? weight)
	{
		weight = null;
		if (cursor >= payload.Count || payload[cursor] != Version) return true;
		if (payload.Count - cursor < 2) return false;
		int value = payload[cursor + 1];
		if (value < 0 || value % HextechWeightedRuneOptions.WeightStep != 0) return false;
		weight = value;
		cursor += 2;
		return true;
	}

	internal static bool TryRestore(PlayerChoiceResult result, IReadOnlyList<RelicModel> options,
		out List<RelicModel> weightedOptions)
	{
		weightedOptions = [];
		if (!HextechChoiceCodec.TryGetIndexPayload(result, out List<int> payload) || payload.Count < 6) return false;
		int rerolls = payload[5];
		if (rerolls < 0 || rerolls > payload.Count - 6) return false;
		if (!HextechStableModelIdListCodec.TryDecode(payload, 6 + rerolls, out List<ModelId> ids, out int cursor)
			|| ids.Count != options.Count || !TryRead(payload, ref cursor, out int? weight) || !weight.HasValue)
			return false;
		weightedOptions = new HextechWeightedRuneOptions(options, weight.Value);
		return true;
	}
}
