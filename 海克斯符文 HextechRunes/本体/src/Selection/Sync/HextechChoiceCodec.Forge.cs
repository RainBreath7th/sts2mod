using MegaCrit.Sts2.Core.GameActions;

namespace HextechRunes;

internal static partial class HextechChoiceCodec
{
	public static PlayerChoiceResult CreateForgeSelection(
		int operationToken,
		int selectedIndex,
		IReadOnlyList<RelicModel> options)
	{
		List<int> payload = [ Magic, ChoiceKindForgeSelection, operationToken, selectedIndex ];
		HextechStableModelIdListCodec.Append(payload, options.Select(static relic => relic.CanonicalInstance?.Id ?? relic.Id));

		return PlayerChoiceResult.FromIndexes(payload);
	}

	public static bool IsForgeSelection(PlayerChoiceResult result, int expectedOperationToken)
	{
		return TryDecodeForgeSelection(result, expectedOperationToken, out _, out _);
	}

	public static bool IsForgeSelection(
		PlayerChoiceResult result,
		int expectedOperationToken,
		IReadOnlyList<RelicModel> expectedOptions)
	{
		if (!TryDecodeForgeSelection(result, expectedOperationToken, out _, out List<ModelId> optionIds)
			|| optionIds.Count != expectedOptions.Count)
		{
			return false;
		}

		for (int i = 0; i < expectedOptions.Count; i++)
		{
			ModelId expectedId = expectedOptions[i].CanonicalInstance?.Id ?? expectedOptions[i].Id;
			if (optionIds[i] != expectedId)
			{
				return false;
			}
		}

		return true;
	}

	public static bool IsMalformedForgeSelectionEnvelope(PlayerChoiceResult result, int expectedOperationToken)
	{
		return IsChoiceEnvelope(result, ChoiceKindForgeSelection)
			&& !TryDecodeForgeSelection(result, expectedOperationToken, out _, out _);
	}

	public static bool TryDecodeForgeSelection(
		PlayerChoiceResult result,
		int expectedOperationToken,
		out int selectedIndex,
		out List<ModelId> optionIds)
	{
		selectedIndex = -1;
		optionIds = [];
		if (!TryGetIndexPayload(result, out List<int> payload)
			|| payload.Count < 5
			|| payload[0] != Magic
			|| payload[1] != ChoiceKindForgeSelection
			|| payload[2] != expectedOperationToken
			|| payload[4] != HextechStableModelIdListCodec.Version)
		{
			return false;
		}

		selectedIndex = payload[3];
		return HextechStableModelIdListCodec.TryDecode(payload, 4, out optionIds, out _);
	}
}
