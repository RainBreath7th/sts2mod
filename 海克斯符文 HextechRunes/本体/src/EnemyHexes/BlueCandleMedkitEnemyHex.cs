namespace HextechRunes;

internal sealed class BlueCandleMedkitEnemyHex : HextechEnemyHexEffect
{
	internal const int CostIncrease = 1;

	private static readonly FieldInfo BaseCostField = HextechHookReflection.RequireField(typeof(CardEnergyCost), "_base");
	private static readonly FieldInfo LocalModifiersField = HextechHookReflection.RequireField(typeof(CardEnergyCost), "_localModifiers");

	internal override MonsterHexKind Kind => MonsterHexKind.BlueCandleMedkit;

	// 玩家的状态牌与诅咒牌耗能 +1,优先级最低:视同加在基础费用上,轮转不息一类的本回合定费会覆盖它;
	// 我方蓝烛药箱的 0 费与敌方开悟的 1 费下限都在 Late 阶段,晚于这里生效。
	// 原版无法打出的状态/诅咒基础费用是 -1(无费用标记),原版费用 Hook 对负费用直接跳过,这里也不处理。
	internal override int GetBaseEnergyCostIncrease(HextechEnemyHexContext context, CardModel card)
	{
		if (card.Type is not (CardType.Status or CardType.Curse)
			|| card.Owner?.Creature.Side != CombatSide.Player
			|| card.Owner.Creature.CombatState?.RunState != context.RunState
			|| card.EnergyCost.CostsX)
		{
			return 0;
		}

		return GetIncreaseSurvivingLocalModifiers(card.EnergyCost, CostIncrease);
	}

	/// <summary>
	/// 把增量加到基础费用上再走一遍卡牌自身的临时修正,返回最终还剩多少:
	/// 绝对定费(如本回合 0 费)吃掉增量,相对加减费照常叠加。临时修正列表原版未公开,只读不写。
	/// </summary>
	internal static int GetIncreaseSurvivingLocalModifiers(CardEnergyCost energyCost, int increase)
	{
		int baseCost = (int)BaseCostField.GetValue(energyCost)!;
		if (baseCost < 0)
		{
			return 0;
		}

		int withIncrease = baseCost + increase;
		int withoutIncrease = baseCost;
		if (LocalModifiersField.GetValue(energyCost) is IEnumerable<LocalCostModifier> modifiers)
		{
			foreach (LocalCostModifier modifier in modifiers)
			{
				withIncrease = modifier.Modify(withIncrease);
				withoutIncrease = modifier.Modify(withoutIncrease);
			}
		}

		return withIncrease - withoutIncrease;
	}
}
