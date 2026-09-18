namespace HextechRunes;

internal sealed class HandOfBaronEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.HandOfBaron;

	internal override decimal ModifyDamageMultiplicative(HextechEnemyHexContext context, Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		return 1m + context.TierValue(Kind, 0.05m, 0.10m, 0.15m);
	}

	internal override async Task BeforePlayerSideTurnStart(HextechEnemyHexContext context, HextechCombatState combatState, IReadOnlyList<Creature> players)
	{
		if (players.Count == 0)
		{
			return;
		}

		await context.RunGroupedPlayerDebuffBurst(async () =>
		{
			await PowerCmd.Apply<ShrinkPower>(players, 2m, null, null);
		});
	}
}
