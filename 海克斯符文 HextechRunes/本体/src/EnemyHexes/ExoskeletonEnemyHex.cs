namespace HextechRunes;

internal sealed class ExoskeletonEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.Exoskeleton;

	internal override Task ApplyCombatStartToEnemy(HextechEnemyHexContext context, Creature enemy, CombatRoom room)
	{
		if (enemy.GetPowerAmount<HardToKillPower>() > 0m)
		{
			return Task.CompletedTask;
		}

		int hardToKill = ResolveHardToKill(enemy.MaxHp, context.GetStrengthTier(Kind));
		return PowerCmd.Apply<HardToKillPower>(enemy, hardToKill, enemy, null);
	}

	internal static int ResolveHardToKill(int maxHp, int tier)
		=> Math.Max(6, (int)Math.Floor(maxHp * (tier <= 1 ? 0.20m : tier == 2 ? 0.15m : 0.10m)));
}
