namespace HextechRunes;

internal sealed class SlapEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.Slap;

	internal override async Task AfterEnemyDebuffReceived(HextechEnemyHexContext context, Creature target)
	{
		if (HextechCombatProcTracker.TryConsumeLimitedProc(context.Tracking.SlapProcsThisTurn, target, 3))
		{
			await PowerCmd.Apply<HextechSlapTemporaryStrengthPower>(target, 1m, target, null);
		}
	}
}
