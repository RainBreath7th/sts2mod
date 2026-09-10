namespace HextechRunes;

internal sealed class SoulEaterEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.SoulEater;

	internal override async Task AfterDeath(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, Creature target, HextechCombatState combatState)
	{
		// 使用死者的最大生命，先快照，避免幸存者的生命增长影响后续计算。
		if (target.Side != CombatSide.Enemy)
		{
			return;
		}

		int maxHpGain = ResolveMaxHpGain(SoulEaterRune.GetRewardMaxHpForDeath(target));
		foreach (Creature enemy in context.GetAliveEnemies(combatState))
		{
			if (enemy == target || !enemy.IsAlive)
			{
				continue;
			}

			await CreatureCmd.GainMaxHp(enemy, maxHpGain);
		}
	}

	internal static int ResolveMaxHpGain(int deadEnemyMaxHp) => Math.Max(1, (int)Math.Floor(deadEnemyMaxHp * 0.25m));
}
