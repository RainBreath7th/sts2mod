namespace HextechRunes;

public sealed class RebootUpgradeRune : CardUpgradeRuneBase<Reboot>
{
	protected override bool IsAvailableForCharacter(Player player) => IsDefectPlayer(player);

	public override bool TryModifyKeywordsInCombat(CardModel card, ISet<CardKeyword> keywords)
	{
		return Owner != null && card.Owner == Owner && card is Reboot && keywords.Remove(CardKeyword.Exhaust);
	}
}
