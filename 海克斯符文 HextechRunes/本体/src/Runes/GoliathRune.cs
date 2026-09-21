namespace HextechRunes;

public sealed class GoliathRune : HextechRelicBase, IHextechMaxHpScalingRune
{
	private const decimal ScaleValue = 1.35m;
	private const decimal MaxHpBonusPercentValue = (ScaleValue - 1m) * 100m;
	private const decimal StatMultiplierValue = 1.2m;
	private const decimal SustainBonusPercentValue = (StatMultiplierValue - 1m) * 100m;

	private int _baseMaxHp;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedBaseMaxHp
	{
		get => _baseMaxHp;
		set => _baseMaxHp = Math.Max(0, value);
	}

	public int BaseMaxHp
	{
		get => _baseMaxHp;
		set => _baseMaxHp = Math.Max(1, value);
	}

	public override bool HasUponPickupEffect => true;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("HpGainPercent", 0.35m),
		new DynamicVar("DamageMultiplier", StatMultiplierValue),
		new DynamicVar("SustainMultiplier", StatMultiplierValue),
		new DynamicVar("Scale", ScaleValue),
		new DynamicVar("MaxHpBonusPercent", MaxHpBonusPercentValue),
		new DynamicVar("SustainBonusPercent", SustainBonusPercentValue)
	];

	public decimal MaxHpScale => DynamicVars["Scale"].BaseValue;

	internal float BodyScaleDelta => (float)DynamicVars["Scale"].BaseValue - 1f;

	public override async Task AfterObtained()
	{
		if (Owner == null)
		{
			return;
		}

		// 主符文可能是先获得的另一个系数参与者(星界躯体/百分比锻造器),此时基础值已在它那里,按新乘积重算。
		IHextechMaxHpBaseHolder primary = HextechMaxHpScaling.GetPrimary(Owner) ?? this;
		HextechMaxHpScaling.EnsureBaseInitialized(Owner, primary, assumeAlreadyScaled: false);
		await CreatureCmdCompat.SetMaxHp(Owner.Creature, primary.BaseMaxHp);
		await CreatureCmd.Heal(Owner.Creature, Owner.Creature.MaxHp - Owner.Creature.CurrentHp);
		Grow();
	}

	public override Task AfterRoomEntered(AbstractRoom room)
	{
		if (Owner != null && HextechMaxHpScaling.GetPrimary(Owner) is { } primary)
		{
			HextechMaxHpScaling.EnsureBaseInitialized(Owner, primary, assumeAlreadyScaled: true);
		}

		Grow();
		return Task.CompletedTask;
	}

	public override decimal ModifyDamageMultiplicativeCompat(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		return IsDamageFromOwnerToEnemyOrPreview(target, dealer, cardSource) ? DynamicVars["DamageMultiplier"].BaseValue : 1m;
	}

	public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
	{
		return target == Owner?.Creature ? DynamicVars["SustainMultiplier"].BaseValue : 1m;
	}

	private void Grow()
	{
		HextechPlayerBodyScaleHelper.Update(Owner);
	}

}
