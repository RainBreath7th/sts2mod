namespace HextechRunes;

internal sealed class MoreTheMerrierEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.MoreTheMerrier;

	internal override decimal ModifyDamageMultiplicative(HextechEnemyHexContext context, Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		return GetMultiplier(context);
	}

	internal override decimal ModifyBlockMultiplicative(HextechEnemyHexContext context, Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
	{
		return GetMultiplier(context);
	}

	internal override decimal ModifyEnemyHealMultiplicative(HextechEnemyHexContext context, Creature creature, decimal amount)
	{
		return GetMultiplier(context);
	}

	private static decimal GetMultiplier(HextechEnemyHexContext context)
	{
		// 与我方同名符文一致，统计原版遗物集合；全队合计后按人数分组，不逐人取整。
		int relicCount = context.RunState.Players.Sum(static player => player.Relics.Count);
		return 1m + (relicCount / context.ScalingPlayerCount) * 0.01m;
	}
}
