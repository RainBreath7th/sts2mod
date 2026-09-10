namespace HextechRunes;

public sealed class BloodDebtRune : HextechRelicBase
{
	private Dictionary<CardModel, decimal>? _damageBonuses;

	public override bool IsAvailableForPlayer(Player player) => IsIroncladPlayer(player);

	protected override void AfterCloned()
	{
		base.AfterCloned();
		_damageBonuses = _damageBonuses == null ? null : new Dictionary<CardModel, decimal>(_damageBonuses);
	}

	public override Task BeforeCombatStart()
	{
		_damageBonuses = null;
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room) => BeforeCombatStart();

	public override Task AfterCurrentHpChanged(Creature creature, decimal delta)
	{
		if (Owner?.PlayerCombatState == null || creature != Owner.Creature
			|| delta >= 0m || creature.IsDead)
		{
			return Task.CompletedTask;
		}

		GrowAttacks(PileType.Hand.GetPile(Owner).Cards.ToArray(), -delta);
		Flash();
		return Task.CompletedTask;
	}

	internal void GrowAttacks(IEnumerable<CardModel> cards, decimal amount)
	{
		if (Owner == null || amount <= 0m) return;
		foreach (CardModel card in cards.Distinct())
		{
			if (card.Owner != Owner || card.Type != CardType.Attack) continue;
			_damageBonuses ??= [];
			_damageBonuses[card] = _damageBonuses.GetValueOrDefault(card) + amount;
		}
	}

	public override decimal ModifyDamageAdditiveCompat(Creature? target, decimal amount, ValueProp props,
		Creature? dealer, CardModel? cardSource)
	{
		// 用卡牌实例记录战斗内成长，换牌堆不清零；统一伤害 Hook 同时支持多段、动态伤害和预览。
		return cardSource?.Owner == Owner && cardSource?.Type == CardType.Attack
			&& HextechSts2Compat.IsPoweredAttack(props)
			&& IsDamageFromOwnerToEnemyOrPreview(target, dealer, cardSource)
			? _damageBonuses?.GetValueOrDefault(cardSource) ?? 0m : 0m;
	}
}
