using MegaCrit.Sts2.Core.GameActions;

namespace HextechRunes;

internal static partial class HextechChoiceCodec
{
	public static PlayerChoiceResult CreateRandomRuneGrant(int operationToken, IReadOnlyList<ModelId> runeIds)
	{
		List<int> payload = [ Magic, ChoiceKindRandomRuneGrant, operationToken ];
		HextechStableModelIdListCodec.Append(payload, runeIds);
		return PlayerChoiceResult.FromIndexes(payload);
	}

	public static bool IsRandomRuneGrant(PlayerChoiceResult result, int expectedOperationToken)
	{
		return TryDecodeRandomRuneGrant(result, expectedOperationToken, out _);
	}

	public static bool TryDecodeRandomRuneGrant(
		PlayerChoiceResult result,
		int expectedOperationToken,
		out List<ModelId> runeIds)
	{
		runeIds = [];
		if (!TryGetIndexPayload(result, out List<int> payload)
			|| payload.Count < 4
			|| payload[0] != Magic
			|| payload[1] != ChoiceKindRandomRuneGrant
			|| payload[2] != expectedOperationToken)
		{
			return false;
		}

		if (payload[3] == HextechStableModelIdListCodec.Version)
		{
			return HextechStableModelIdListCodec.TryDecode(payload, 3, out runeIds, out _);
		}

		return false;
	}
}
