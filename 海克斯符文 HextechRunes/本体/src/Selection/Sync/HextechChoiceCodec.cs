using MegaCrit.Sts2.Core.GameActions;
using System.Text;

namespace HextechRunes;

internal readonly record struct EnemyHexAdjustmentPayload(
	int ActIndex,
	int Sequence,
	IReadOnlyList<MonsterHexKind?> MonsterHexes,
	IReadOnlyList<int> RerollCounts,
	bool IsFinal);

internal static partial class HextechChoiceCodec
{
	private const int Magic = 0x48585452; // HXTR
	private const int ChoiceKindActRoll = 1;
	private const int ChoiceKindRuneSelection = 2;
	private const int ChoiceKindActSelectionApplied = 3;
	private const int ChoiceKindEnemyHexAdjustment = 4;
	private const int ChoiceKindForgeSelection = 5;
	private const int ChoiceKindRandomRuneGrant = 6;
	private const int ChoiceKindRelicOptionSelection = 7;
	private const int EnemyHexAdjustmentListVersion = -2;
	private const int PlayerRuneConfigBitsetVersion = -4;
	private const int LegacyRunConfigurationSnapshotVersion = -5;
	private const int LegacyRerollRunConfigurationSnapshotVersion = -6;
	private const int PreviousRunConfigurationSnapshotVersion = -7;
	private const int PreviousSingleRarityRunConfigurationSnapshotVersion = -8;
	private const int RunConfigurationSnapshotVersion = -9;
	private const int PlayerRuneConfigBitsPerWord = 30;
	private const int MaxPlayerRuneConfigBitsetWords = 64;
	private const int MaxDisabledMonsterHexes = 128;
	private const int MaxChoiceListCount = HextechStableModelIdListCodec.MaxCount;
	private const uint Fnv1aOffsetBasis = 2166136261U;
	private const uint Fnv1aPrime = 16777619U;

	public static int ComputeOperationToken(
		string operationKind,
		uint choiceId,
		ulong playerNetId,
		string context)
	{
		ArgumentNullException.ThrowIfNull(operationKind);
		ArgumentNullException.ThrowIfNull(context);

		uint hash = Fnv1aOffsetBasis;
		AppendFnvString(ref hash, operationKind);
		AppendFnvUInt32(ref hash, choiceId);
		AppendFnvUInt64(ref hash, playerNetId);
		AppendFnvString(ref hash, context);
		return unchecked((int)hash);
	}

	private static bool IsChoiceEnvelope(PlayerChoiceResult result, int choiceKind)
	{
		return TryGetIndexPayload(result, out List<int> payload)
			&& payload.Count >= 2
			&& payload[0] == Magic
			&& payload[1] == choiceKind;
	}

	public static bool TryGetIndexPayload(PlayerChoiceResult result, out List<int> payload)
	{
		payload = [];
		try
		{
			List<int>? indexes = result.AsIndexes();
			if (indexes == null)
			{
				return false;
			}

			payload = indexes;
			return true;
		}
		catch (InvalidOperationException)
		{
			return false;
		}
	}

	private static bool HasRemaining(IReadOnlyList<int> payload, int cursor, int count)
	{
		return cursor >= 0
			&& count >= 0
			&& cursor <= payload.Count
			&& count <= payload.Count - cursor;
	}

	private static void ValidateProtocolCount(int count, int maximum, string parameterName)
	{
		if (count < 0 || count > maximum)
		{
			throw new ArgumentOutOfRangeException(
				parameterName,
				count,
				$"Protocol list count must be between 0 and {maximum}.");
		}
	}

	private static void AppendFnvString(ref uint hash, string value)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(value);
		AppendFnvUInt32(ref hash, checked((uint)bytes.Length));
		foreach (byte valueByte in bytes)
		{
			AppendFnvByte(ref hash, valueByte);
		}
	}

	private static void AppendFnvUInt32(ref uint hash, uint value)
	{
		for (int shift = 0; shift < 32; shift += 8)
		{
			AppendFnvByte(ref hash, unchecked((byte)(value >> shift)));
		}
	}

	private static void AppendFnvUInt64(ref uint hash, ulong value)
	{
		for (int shift = 0; shift < 64; shift += 8)
		{
			AppendFnvByte(ref hash, unchecked((byte)(value >> shift)));
		}
	}

	private static void AppendFnvByte(ref uint hash, byte value)
	{
		hash = unchecked((hash ^ value) * Fnv1aPrime);
	}
}
