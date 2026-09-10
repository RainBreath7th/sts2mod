namespace HextechRunes;

public sealed class BloodPactRune : HextechRelicBase
{
	// 保留旧版字段身份；歃血已改为即时力量，不再恢复待结算的临时力量。
	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	private int SavedPendingTemporaryStrength
	{
		get => 0;
		set { }
	}

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<StrengthPower>(1m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<StrengthPower>()
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsIroncladPlayer(player);
	}

	public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (Owner == null
			|| target != Owner.Creature
			|| Owner.Creature.IsDead
			|| !ShouldGainStrength(dealer?.Side, result.UnblockedDamage, props))
		{
			return Task.CompletedTask;
		}

		Flash();
		return PowerCmd.Apply<StrengthPower>(Owner.Creature, DynamicVars.Strength.BaseValue, Owner.Creature, null);
	}

	internal static bool ShouldGainStrength(CombatSide? dealerSide, decimal hpLost, ValueProp props)
		=> dealerSide == CombatSide.Enemy && hpLost > 0m && HextechSts2Compat.IsPoweredAttack(props);
}
