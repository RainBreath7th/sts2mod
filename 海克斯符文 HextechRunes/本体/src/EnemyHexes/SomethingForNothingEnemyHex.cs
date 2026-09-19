namespace HextechRunes;

internal sealed class SomethingForNothingEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.SomethingForNothing;

	internal override (PileType, CardPilePosition)? ModifyCardPlayResultPileTypeAndPosition(
		HextechEnemyHexContext context, CardModel card, bool isAutoPlay, ResourceInfo resources,
		PileType pileType, CardPilePosition position)
	{
		if (card.Owner?.Creature.Side != CombatSide.Player
			|| card.Owner.Creature.CombatState?.RunState != context.RunState)
		{
			return null;
		}

		// 与我方无本万利使用同一出牌费用口径；自动打出不等于牌本身为零费。
		decimal energyCost = HextechCombatHooks.TryGetActivePlayEnergyValue(card, out decimal captured)
			? captured
			: resources.EnergyValue;
		return SomethingForNothingRune.IsZeroCostPlay(energyCost) ? (PileType.Exhaust, position) : null;
	}
}
