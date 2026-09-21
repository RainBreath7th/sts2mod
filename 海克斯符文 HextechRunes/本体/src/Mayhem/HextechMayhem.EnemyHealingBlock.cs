namespace HextechRunes;

internal sealed partial class HextechMayhemModifier
{
	internal bool QueueEnemyHealingBlock(Creature creature, decimal amount)
	{
		return HextechEnemyHealingBlockQueue.Queue(this, creature, amount);
	}

	private Task ApplyDelayedEnemyHealingBlocks(HextechCombatState combatState)
	{
		return HextechEnemyHealingBlockQueue.ApplyDelayed(this, combatState);
	}
}
