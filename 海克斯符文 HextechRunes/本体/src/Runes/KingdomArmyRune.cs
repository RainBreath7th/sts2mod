namespace HextechRunes;

public sealed class KingdomArmyRune : HextechRelicBase
{
	private int _generatedMinionsThisCombat;

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<MinionStrike>(),
		HoverTipFactory.FromCard<MinionDiveBomb>(),
		HoverTipFactory.FromCard<MinionSacrifice>()
	];

	public override bool IsAvailableForPlayer(Player player) => IsRegentPlayer(player);

	public override Task BeforeCombatStart()
	{
		_generatedMinionsThisCombat = 0;
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		_generatedMinionsThisCombat = 0;
		return Task.CompletedTask;
	}

	public override async Task AfterForge(decimal amount, Player forger, AbstractModel? source)
	{
		if (Owner == null || forger != Owner || amount <= 0m || Owner.Creature.IsDead
			|| Owner.PlayerCombatState == null || Owner.Creature.CombatState is not HextechCombatState combatState)
		{
			return;
		}

		// 按铸造事件取一次同步序号，与铸造数值或场上的君王之剑数量无关。
		int ordinal = ConsumeCombatProcOrdinal(nameof(KingdomArmyRune), ref _generatedMinionsThisCombat);
		CardModel card = HextechStableRandom.CreateMinionCard(combatState, Owner, "kingdom-army", ordinal);
		Flash();
		await HextechCardGeneration.AddGeneratedCardToCombat(card, PileType.Hand, addedByPlayer: true);
	}
}
