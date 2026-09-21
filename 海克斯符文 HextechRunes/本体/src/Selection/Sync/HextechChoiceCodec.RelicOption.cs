using MegaCrit.Sts2.Core.GameActions;

namespace HextechRunes;

internal static partial class HextechChoiceCodec
{
	public static PlayerChoiceResult CreateRelicOptionSelection(
		int operationToken,
		int selectedIndex,
		IReadOnlyList<RelicModel> options)
	{
		List<int> payload = [ Magic, ChoiceKindRelicOptionSelection, operationToken, selectedIndex ];
		HextechStableModelIdListCodec.Append(payload, options.Select(static relic => relic.CanonicalInstance?.Id ?? relic.Id));

		return PlayerChoiceResult.FromIndexes(payload);
	}

	public static bool IsRelicOptionSelection(
		PlayerChoiceResult result,
		int expectedOperationToken,
		IReadOnlyList<RelicModel> expectedOptions)
	{
		if (!TryDecodeRelicOptionSelection(result, expectedOperationToken, out _, out List<ModelId> optionIds)
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

	public static bool IsMalformedRelicOptionSelectionEnvelope(PlayerChoiceResult result, int expectedOperationToken)
	{
		return IsChoiceEnvelope(result, ChoiceKindRelicOptionSelection)
			&& !TryDecodeRelicOptionSelection(result, expectedOperationToken, out _, out _);
	}

	public static bool TryDecodeRelicOptionSelection(
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
			|| payload[1] != ChoiceKindRelicOptionSelection
			|| payload[2] != expectedOperationToken
			|| payload[4] != HextechStableModelIdListCodec.Version)
		{
			return false;
		}

		selectedIndex = payload[3];
		return HextechStableModelIdListCodec.TryDecode(payload, 4, out optionIds, out _);
	}
}
