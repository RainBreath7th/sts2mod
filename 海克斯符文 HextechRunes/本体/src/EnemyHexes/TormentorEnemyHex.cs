namespace HextechRunes;

internal sealed class TormentorEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.Tormentor;

	internal override async Task AfterEnemyDebuffReceived(HextechEnemyHexContext context, Creature target)
	{
		if (context.Tracking.HandlingMonsterTormentorBurn
			|| !HextechCombatProcTracker.TryConsumeLimitedProc(context.Tracking.TormentorProcsThisTurn, target, 3))
		{
			return;
		}

		try
		{
			context.Tracking.HandlingMonsterTormentorBurn = true;
			foreach (Player player in target.CombatState!.Players)
			{
				if (player.Creature.IsAlive)
				{
					await PowerCmd.Apply<HextechBurnPower>(player.Creature, 1m, target, null);
				}
			}
		}
		finally
		{
			context.Tracking.HandlingMonsterTormentorBurn = false;
		}
	}
}
