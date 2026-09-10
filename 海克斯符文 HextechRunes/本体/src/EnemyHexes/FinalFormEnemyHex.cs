namespace HextechRunes;

internal sealed class FinalFormEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.FinalForm;

	internal override async Task AfterEnemyDamageGivenPlayerHit(HextechEnemyHexContext context, Creature dealer, Creature target)
	{
		if (dealer.IsAlive
			&& dealer.CombatId != null
			&& context.Tracking.FinalFormTriggeredThisTurn.Add(dealer.CombatId.Value))
		{
			int plating = ResolvePlating(dealer.MaxHp, context.GetStrengthTier(Kind));
			await HextechEnemyPowerScalingHooks.Apply<PlatingPower>(dealer, plating, dealer, null);
		}
	}

	internal static int ResolvePlating(int maxHp, int tier)
		=> Math.Max(1, (int)Math.Floor(maxHp * (tier <= 1 ? 0.03m : tier == 2 ? 0.04m : 0.05m)));
}
