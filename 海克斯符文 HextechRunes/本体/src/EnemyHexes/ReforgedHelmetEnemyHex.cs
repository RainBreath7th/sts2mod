namespace HextechRunes;

internal sealed class ReforgedHelmetEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.ReforgedHelmet;

	internal override decimal ModifyPowerAmountReceived(HextechEnemyHexContext context, PowerModel canonicalPower, Creature target, decimal amount, Creature? applier)
	{
		return target.Side == CombatSide.Enemy
			&& target.CombatState?.RunState == context.RunState
			&& canonicalPower is StrengthPower
			&& amount < 0m
			? 0m
			: amount;
	}
}
