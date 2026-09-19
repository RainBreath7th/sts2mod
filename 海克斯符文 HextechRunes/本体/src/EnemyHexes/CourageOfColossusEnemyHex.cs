namespace HextechRunes;

internal sealed class CourageOfColossusEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.CourageOfColossus;

	internal override async Task AfterEnemyDebuffReceived(HextechEnemyHexContext context, Creature target)
	{
		if (HextechCombatProcTracker.TryConsumeLimitedProc(context.Tracking.CourageProcsThisTurn, target, 2))
		{
			int plating = ResolvePlating(target.MaxHp, context.GetStrengthTier(Kind));
			await HextechEnemyPowerScalingHooks.Apply<PlatingPower>(target, plating, target, null);
		}
	}

	internal static int ResolvePlating(int maxHp, int tier)
		=> Math.Max(1, (int)Math.Floor(maxHp * (tier <= 1 ? 0.01m : tier == 2 ? 0.02m : 0.03m)));
}
