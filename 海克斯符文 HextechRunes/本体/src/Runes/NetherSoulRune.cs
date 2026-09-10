namespace HextechRunes;

public sealed class NetherSoulRune : HextechRelicBase
{
	private bool _playingExhausted;

	public override bool IsAvailableForPlayer(Player player) => IsNecrobinderPlayer(player);

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromKeyword(CardKeyword.Ethereal),
		HoverTipFactory.FromKeyword(CardKeyword.Exhaust)
	];

	public override async Task AfterSideTurnEndLate(PlayerChoiceContext choiceContext, CombatSide side,
		IEnumerable<Creature> participants)
	{
		if (_playingExhausted || Owner?.PlayerCombatState == null || side != Owner.Creature.Side
			|| !participants.Contains(Owner.Creature) || Owner.Creature.IsDead
			|| Owner.Creature.CombatState == null || CombatManager.Instance.IsOverOrEnding) return;

		// 此阶段在弃牌与虚无消耗后。快照只执行一遍，牌自身再次消耗不会重新入队。
		CardModel[] cards = SnapshotEtherealCards(Owner, PileType.Exhaust.GetPile(Owner).Cards);
		if (cards.Length == 0) return;
		_playingExhausted = true;
		try
		{
			Flash();
			foreach (CardModel card in cards)
			{
				if (CombatManager.Instance.IsOverOrEnding || Owner.Creature.IsDead
					|| Owner.Creature.CombatState == null) break;
				if (card.Owner != Owner || card.Pile?.Type != PileType.Exhaust
					|| !card.Keywords.Contains(CardKeyword.Ethereal)) continue;

				Creature? target = card.TargetType == TargetType.AnyEnemy
					? Owner.Creature.CombatState.HittableEnemies
						.OrderBy(static enemy => enemy.CombatId ?? uint.MaxValue).FirstOrDefault()
					: null;
				if (card.TargetType == TargetType.AnyEnemy && target == null) break;
				await HextechAutoPlayHelper.AutoPlayOrMoveToResultPile(choiceContext, card, target);
			}
		}
		finally
		{
			_playingExhausted = false;
		}
	}

	internal static CardModel[] SnapshotEtherealCards(Player owner, IEnumerable<CardModel> cards) =>
		cards.Where(card => card.Owner == owner && card.Keywords.Contains(CardKeyword.Ethereal))
			.Distinct().ToArray();
}
