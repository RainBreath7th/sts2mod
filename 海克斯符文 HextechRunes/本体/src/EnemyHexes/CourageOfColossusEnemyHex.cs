namespace HextechRunes;

internal sealed class CourageOfColossusEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.CourageOfColossus;

	internal override async Task AfterCourageTrigger(HextechEnemyHexContext context, Creature source)
	{
		if (HextechCombatProcTracker.TryConsumeLimitedProc(context.Tracking.CourageProcsThisTurn, source, 1))
		{
			int plating = ResolvePlating(source.MaxHp, context.GetStrengthTier(Kind));
			await HextechEnemyPowerScalingHooks.Apply<PlatingPower>(source, plating, source, null);
		}
	}

	internal static int ResolvePlating(int maxHp, int tier)
		=> Math.Max(1, (int)Math.Floor(maxHp * (tier <= 1 ? 0.03m : tier == 2 ? 0.04m : 0.05m)));
}
