namespace HextechRunes;

public sealed class ClawUpgradeRune : CardUpgradeRuneBase<Claw>
{
	protected override bool IsAvailableForCharacter(Player player) => IsDefectPlayer(player);

	public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (Owner == null || cardPlay.Card.Owner != Owner || cardPlay.Card is not Claw) return Task.CompletedTask;
		GrowClaws(Owner.Deck.Cards.Concat(Owner.PlayerCombatState!.AllCards));
		Flash();
		return Task.CompletedTask;
	}

	internal void GrowClaws(IEnumerable<CardModel> cards)
	{
		// 每个牌库本体只保存一次 +1；战斗克隆同步得到 +1，不把原版的本场成长写回牌库。
		foreach (var group in cards.OfType<Claw>().Where(card => card.Owner == Owner).Distinct()
			.GroupBy(card => card.DeckVersion ?? card))
		{
			CardModel representative = group.FirstOrDefault(card => card != group.Key) ?? group.Key;
			HextechSelfUpgradeCardStore.AddDamageOnPlay(representative, 1);
			foreach (Claw copy in group.Where(card => card != representative && card != group.Key))
				copy.DynamicVars.Damage.BaseValue += 1m;
		}
	}
}
