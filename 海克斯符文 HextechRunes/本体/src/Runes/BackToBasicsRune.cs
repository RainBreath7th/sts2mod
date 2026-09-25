namespace HextechRunes;

public sealed class BackToBasicsRune : HextechRelicBase
{
	// 只限制手动出牌。自动打出(形态开局自动打出、原版自动打出效果等)放行,与敌方同名海克斯、卡卡同口径;
	// 否则同时持有"升级:XX形态"时,3 费形态牌会在开局被拦下直接进结果堆。
	public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType)
	{
		return autoPlayType != AutoPlayType.None
			|| card.Owner != Owner
			|| card.EnergyCost.CostsX
			|| HextechCombatHooks.GetEnergyCostForCurrentCardPlay(card) < 3m;
	}

	public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
	{
		return target == Owner?.Creature ? 1.4m : 1m;
	}

	public override decimal ModifyDamageMultiplicativeCompat(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		return IsDamageFromOwnerToEnemyOrPreview(target, dealer, cardSource) ? 1.4m : 1m;
	}
}
