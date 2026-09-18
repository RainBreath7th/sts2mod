namespace HextechRunes;

internal sealed class EnlightenmentEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.Enlightenment;

	internal override decimal ModifyEnergyCostInCombatLate(HextechEnemyHexContext context, CardModel card, decimal cost)
	{
		if (card.Owner?.Creature.Side != CombatSide.Player
			|| card.Owner.Creature.CombatState?.RunState != context.RunState
			|| card.EnergyCost.CostsX)
		{
			return cost;
		}

		// 在普通降费与本回合费用修改之后设下限，预览和实际支付共用原版费用 Hook。
		return Math.Max(1m, cost);
	}
}
