namespace HextechRunes;

public sealed class SmokestackUpgradeRune : CardUpgradeRuneBase<Smokestack>
{
	protected override bool IsAvailableForCharacter(Player player) => IsDefectPlayer(player);

	public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
	{
		if (Owner == null || card.Owner != Owner || card.Type != CardType.Status) return;
		SmokestackPower? power = Owner.Creature.GetPower<SmokestackPower>();
		if (power == null) return;
		// 抽到状态牌并不生成新牌，直接补一次烟囱效果，保留原本生成状态牌时的触发。
		Flash();
		await CreatureCmd.Damage(choiceContext, power.CombatState!.HittableEnemies, power.Amount, ValueProp.Unpowered, power.Owner);
	}
}
