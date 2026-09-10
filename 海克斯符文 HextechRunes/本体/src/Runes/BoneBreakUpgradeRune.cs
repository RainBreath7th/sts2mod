namespace HextechRunes;

public sealed class BoneBreakUpgradeRune : CardUpgradeRuneBase<BoneShards>
{
	private int _pendingOstyHp;

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<BoneShards>()
	];

	protected override bool IsAvailableForCharacter(Player player)
	{
		return IsNecrobinderPlayer(player);
	}

	public override Task BeforeCardPlayed(CardPlay cardPlay)
	{
		_pendingOstyHp = 0;
		if (Owner != null
			&& cardPlay.Card.Owner == Owner
			&& cardPlay.Card is BoneShards
			&& Owner.IsOstyAlive
			&& Owner.Osty is { } osty)
		{
			// 碎骨会在结算中牺牲奥斯提，必须在打出前快照最大生命值。
			_pendingOstyHp = Math.Max(0, osty.MaxHp);
		}

		return Task.CompletedTask;
	}

	public override async Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (Owner == null
			|| Owner.Creature.IsDead
			|| cardPlay.Card.Owner != Owner
			|| cardPlay.Card is not BoneShards
			|| _pendingOstyHp <= 0
			|| Owner.Creature.CombatState is not { } combatState)
		{
			_pendingOstyHp = 0;
			return;
		}

		int bonus = _pendingOstyHp;
		_pendingOstyHp = 0;
		List<Creature> enemies = combatState.HittableEnemies.ToList();

		Flash(enemies.Append(Owner.Creature));
		if (enemies.Count > 0)
		{
			await HextechGameApiCompat.Damage(context, enemies, bonus, ValueProp.Unpowered, Owner.Creature, cardPlay.Card);
		}

		await CreatureCmd.GainBlock(Owner.Creature, bonus, ValueProp.Unpowered, cardPlay);
	}
}
