namespace HextechRunes;

internal static class HextechEnemyHealingBlockQueue
{
	public static bool Queue(HextechMayhemModifier modifier, Creature creature, decimal amount)
	{
		if (creature.Side != CombatSide.Enemy || creature.CombatId == null || amount <= 0m)
		{
			return false;
		}

		int block = (int)Math.Floor(amount);
		if (block <= 0)
		{
			return false;
		}

		uint combatId = creature.CombatId.Value;
		modifier.CombatTracking.DelayedEnemyHealingBlock[combatId] =
			modifier.CombatTracking.DelayedEnemyHealingBlock.GetValueOrDefault(combatId, 0) + block;
		return true;
	}

	public static async Task ApplyDelayed(HextechMayhemModifier modifier, HextechCombatState combatState)
	{
		foreach ((uint combatId, int block) in modifier.CombatTracking.DelayedEnemyHealingBlock.ToList())
		{
			modifier.CombatTracking.DelayedEnemyHealingBlock.Remove(combatId);
			Creature? creature = combatState.GetCreature(combatId);
			if (creature == null || !creature.IsAlive || block <= 0)
			{
				continue;
			}

			await CreatureCmd.GainBlock(creature, block, ValueProp.Unpowered, null);
		}
	}
}
