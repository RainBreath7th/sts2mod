namespace HextechRunes;

public sealed class EndlessRotationRune : HextechRelicBase
{
	private HashSet<CardModel>? _freeCards;

	protected override void AfterCloned()
	{
		base.AfterCloned();
		_freeCards = _freeCards == null ? null : new HashSet<CardModel>(_freeCards);
	}

	public override Task BeforeCombatStart()
	{
		_freeCards = null;
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room) => BeforeCombatStart();

	public override Task AfterSideTurnEndLate(PlayerChoiceContext choiceContext, CombatSide side,
		IEnumerable<Creature> participants)
	{
		if (Owner != null && side == Owner.Creature.Side && participants.Contains(Owner.Creature))
		{
			_freeCards = null;
		}
		return Task.CompletedTask;
	}

	public override bool TryModifyStarCost(CardModel card, decimal originalCost, out decimal modifiedCost)
	{
		bool free = Owner != null && card.Owner == Owner && _freeCards?.Contains(card) == true;
		modifiedCost = free ? 0m : originalCost;
		return free;
	}

	public override Task AfterShuffle(PlayerChoiceContext choiceContext, Player shuffler)
	{
		if (Owner?.PlayerCombatState == null || shuffler != Owner || Owner.Creature.IsDead)
		{
			return Task.CompletedTask;
		}

		foreach (CardModel card in PileType.Hand.GetPile(Owner).Cards.ToArray())
		{
			MakeFreeForTurn(card);
		}
		Flash();
		return Task.CompletedTask;
	}

	internal void MakeFreeForTurn(CardModel card)
	{
		// 原版两种“本回合免费”都在打出后清除。能量有整回合 API，辉星以模型 Hook 延续到回合末。
		(_freeCards ??= []).Add(card);
		card.EnergyCost.SetThisTurn(0);
		card.SetStarCostThisTurn(0);
	}
}
