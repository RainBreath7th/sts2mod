namespace HextechRunes;

internal sealed class HundredRefinementsEnemyHex : HextechEnemyHexEffect
{
	// 每 N 次未被格挡伤害(N=联机人数)失去一次临时缓慢;描述侧由 MonsterHexCatalog 的 HitsNeeded 阈值同步显示。
	internal const int HitsPerTriggerPerPlayer = 1;

	internal override MonsterHexKind Kind => MonsterHexKind.HundredRefinements;

	internal override Task AfterEnemyDamageReceivedAny(
		HextechEnemyHexContext context,
		Creature target,
		DamageResult result,
		Creature? dealer,
		CardModel? cardSource)
	{
		if (!target.IsAlive
			|| target.Side != CombatSide.Enemy
			|| target.CombatState?.RunState != context.RunState
			|| result.UnblockedDamage <= 0m
			|| target.CombatId is not uint combatId
			|| !ReachesHitThreshold(context.Tracking.EnemyHundredRefinementsUnblockedHitsThisCombat, combatId, HitsPerTriggerPerPlayer * context.ScalingPlayerCount))
		{
			return Task.CompletedTask;
		}

		return HextechPowerCmdCompat.Apply<HextechTemporarySlowPower>(
			target,
			ResolveSlowReduction(context.GetStrengthTier(Kind)),
			dealer,
			cardSource,
			silent: true);
	}

	internal static int ResolveSlowReduction(int strengthTier)
	{
		return strengthTier switch
		{
			<= 1 => -2,
			2 => -4,
			_ => -6
		};
	}
}
