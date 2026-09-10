using MegaCrit.Sts2.Core.GameActions;

namespace HextechRunes;

/// <summary>候选模型 ID 后的可选实例数据。限制长度并校验完整尾部，避免错误配方静默重推导。</summary>
internal static class HextechGeneratedRuneDataCodec
{
	private const int Version = -10;
	internal const int MaxDataLength = 512;

	public static void Append(List<int> payload, IReadOnlyList<RelicModel> options)
	{
		if (!options.Any(static option => option is IHextechGeneratedRune)) return;
		payload.Add(Version);
		payload.Add(options.Count);
		foreach (RelicModel option in options)
		{
			string data = (option as IHextechGeneratedRune)?.ExportSelectionData() ?? "";
			if (data.Length > MaxDataLength) throw new ArgumentException("Generated rune data exceeds protocol limit.");
			payload.Add(data.Length);
			foreach (char ch in data) payload.Add(ch);
		}
	}

	internal static bool TryDecode(IReadOnlyList<int> payload, int cursor, int count, out List<string> data)
	{
		data = [];
		if (cursor == payload.Count) return true;
		if (cursor < 0 || payload.Count - cursor < 2 || payload[cursor++] != Version || payload[cursor++] != count)
			return false;
		for (int i = 0; i < count; i++)
		{
			if (cursor >= payload.Count) return false;
			int length = payload[cursor++];
			if (length < 0 || length > MaxDataLength || length > payload.Count - cursor) return false;
			char[] chars = new char[length];
			for (int j = 0; j < length; j++)
			{
				int value = payload[cursor++];
				if (value < 0 || value > char.MaxValue) return false;
				chars[j] = (char)value;
			}
			data.Add(new string(chars));
		}
		return cursor == payload.Count;
	}

	internal static bool Restore(PlayerChoiceResult result, IReadOnlyList<RelicModel> options)
	{
		if (!HextechChoiceCodec.TryGetIndexPayload(result, out List<int> payload) || payload.Count < 6) return false;
		int rerolls = payload[5];
		if (rerolls < 0 || rerolls > payload.Count - 6) return false;
		if (!HextechStableModelIdListCodec.TryDecode(payload, 6 + rerolls, out List<ModelId> ids, out int cursor)
			|| ids.Count != options.Count || !HextechRuneWeightCodec.TryRead(payload, ref cursor, out _)
			|| !TryDecode(payload, cursor, ids.Count, out List<string> data)) return false;
		for (int i = 0; i < options.Count; i++)
		{
			string value = data.Count == 0 ? "" : data[i];
			if (options[i] is IHextechGeneratedRune generated)
			{
				if (value.Length == 0 || !generated.TryImportSelectionData(value)) return false;
			}
			else if (value.Length != 0) return false;
		}
		return true;
	}
}
