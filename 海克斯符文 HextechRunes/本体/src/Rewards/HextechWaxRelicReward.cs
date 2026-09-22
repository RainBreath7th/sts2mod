namespace HextechRunes;

internal sealed class HextechWaxRelicReward : RelicReward
{
	public HextechWaxRelicReward(RelicModel relic, Player player)
		: base(EnsureWax(relic), player)
	{
	}

	/// <summary>
	/// 蜡制标记:原版 Relic 分支不读 CustomDescriptionEncounterSourceId,把它写成蜡制来源符文的 ModelId
	/// 即可明确归属(与色彩发现奖励同一手法)。旧存档只有 WasGoldStolenBack=true 这一个借用标记,
	/// 恢复时仍接受,但仅限来源字段为空的旧存档,避免误认第三方同样借用该布尔的奖励。
	/// </summary>
	private static ModelId WaxSourceId => ModelDb.GetId<TezcatarasMercyRune>();

	internal static bool IsWaxSave(SerializableReward save)
	{
		if (save.CustomDescriptionEncounterSourceId != ModelId.none)
		{
			return save.CustomDescriptionEncounterSourceId == WaxSourceId;
		}

		return save.WasGoldStolenBack;
	}

	public override SerializableReward ToSerializable()
	{
		SerializableReward save = base.ToSerializable();
		save.CustomDescriptionEncounterSourceId = WaxSourceId;
		// 兼容 0.9.5 及更早版本读取本存档(它们只认这个借用标记)。
		save.WasGoldStolenBack = true;
		return save;
	}

	private static RelicModel EnsureWax(RelicModel relic)
	{
		relic.IsWax = true;
		return relic;
	}
}
