using MegaCrit.Sts2.Core.Models.Afflictions;

namespace HextechRunes;

// 原版 VitalSpark 属于敌人的增益，会处理全队技能牌；玩家侧版本只处理持有者。
// Tainted 附着本身不结算污染，由此 Power 在打牌后通过原版命令施加 TaintedPower。
public sealed class HextechVitalSparkPower : HextechPowerBase
{
	public override PowerType Type => PowerType.Debuff;

	public override PowerStackType StackType => PowerStackType.Counter;

	protected override IEnumerable<IHoverTip> ExtraHoverTips => HoverTipFactory.FromAffliction<Tainted>(Amount);

	public override Task AfterApplied(Creature? applier, CardModel? cardSource) => AfflictOwnerSkillCards();

	public override Task BeforeCombatStart() => AfflictOwnerSkillCards();

	public override async Task AfterCardEnteredCombat(CardModel card)
	{
		if (Amount > 0 && card.Owner?.Creature == Owner && card.Type == CardType.Skill)
		{
			await SyncCard(card, Amount + NativeAmount(Owner));
		}
	}

	public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (Amount > 0 && cardPlay.Card.Owner?.Creature == Owner && cardPlay.Card.Affliction is Tainted)
		{
			await PowerCmd.Apply<TaintedPower>(choiceContext, Owner, Amount, null, null);
		}
	}

	public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		return power == this ? AfflictOwnerSkillCards() : Task.CompletedTask;
	}

	public override Task AfterRemoved(Creature oldOwner) => SyncOwnerCards(oldOwner, NativeAmount(oldOwner));

	internal Task AfflictOwnerSkillCards()
	{
		if (Amount <= 0)
		{
			return Task.CompletedTask;
		}
		return SyncOwnerCards(Owner, Amount + NativeAmount(Owner));
	}

	// 原版每个 VitalSparkPower 独立结算污染。牌上展示合计，打出时本模型只贡献自身层数。
	private static int NativeAmount(Creature owner) => owner.CombatState?.Creatures
		.SelectMany(static creature => creature.Powers).OfType<VitalSparkPower>()
		.Sum(static power => power.Amount) ?? 0;

	private static async Task SyncOwnerCards(Creature owner, int amount)
	{
		foreach (CardModel card in OwnerSkillCards(owner))
		{
			await SyncCard(card, amount);
		}
	}

	private static async Task SyncCard(CardModel card, int amount)
	{
		if (card.Affliction is Tainted tainted)
		{
			if (amount > 0)
			{
				tainted.Amount = amount;
			}
			else
			{
				CardCmd.ClearAffliction(card);
			}
		}
		else if (amount > 0 && card.Affliction == null)
		{
			await CardCmd.Afflict<Tainted>(card, amount);
		}
	}

	private static IEnumerable<CardModel> OwnerSkillCards(Creature owner) =>
		owner.Player?.PlayerCombatState?.AllCards
			.Where(static card => card.Type == CardType.Skill).ToList() ?? [];
}
