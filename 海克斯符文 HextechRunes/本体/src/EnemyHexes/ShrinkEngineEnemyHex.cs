namespace HextechRunes;

internal sealed class ShrinkEngineEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.ShrinkEngine;

	internal override async Task BeforePlayerSideTurnStart(HextechEnemyHexContext context, HextechCombatState combatState, IReadOnlyList<Creature> players)
	{
		foreach (Creature enemy in context.GetAliveEnemies(combatState))
		{
			if (enemy.GetPowerAmount<SlipperyPower>() <= 0m)
			{
				await HextechEnemyPowerScalingHooks.ApplyExact<SlipperyPower>(enemy, context.TierValue(Kind, 0, 1, 2), enemy, null);
			}
		}
	}

	internal override Task BeforeEnemySideTurnStart(HextechEnemyHexContext context, HextechCombatState combatState, IReadOnlyList<Creature> players, IReadOnlyList<Creature> enemies)
	{
		foreach (Creature enemy in enemies)
		{
			if (enemy.CombatId == null)
			{
				continue;
			}

			uint combatId = enemy.CombatId.Value;
			context.Tracking.ShrinkEngineStacks[combatId] = context.Tracking.ShrinkEngineStacks.GetValueOrDefault(combatId, 0) + 1;
			context.UpdateEnemyScale(enemy);
		}

		return Task.CompletedTask;
	}
}
