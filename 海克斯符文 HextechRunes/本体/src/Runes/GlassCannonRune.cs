namespace HextechRunes;

public sealed class GlassCannonRune : HextechRelicBase
{
	private const decimal DamageMultiplierValue = 1.5m;
	private const decimal DamageBonusPercentValue = (DamageMultiplierValue - 1m) * 100m;
	private const decimal HealCapPercentValue = 0.7m;
	private const decimal HealCapDisplayPercentValue = HealCapPercentValue * 100m;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("DamageMultiplier", DamageMultiplierValue),
		new DynamicVar("HealCapPercent", HealCapPercentValue),
		new DynamicVar("DamageBonusPercent", DamageBonusPercentValue),
		new DynamicVar("HealCapDisplayPercent", HealCapDisplayPercentValue)
	];

	public decimal HealCapPercent => DynamicVars["HealCapPercent"].BaseValue;

	public override async Task AfterObtained()
	{
		if (Owner?.Creature == null)
		{
			return;
		}

		int hpCap = Math.Max(1, FloorToInt(Owner.Creature.MaxHp * HealCapPercent));
		if (Owner.Creature.CurrentHp > hpCap)
		{
			await CreatureCmd.SetCurrentHp(Owner.Creature, hpCap);
		}
	}

	public override decimal ModifyDamageMultiplicativeCompat(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (!IsDamageFromOwnerToEnemyOrPreview(target, dealer, cardSource))
		{
			return 1m;
		}

		return DynamicVars["DamageMultiplier"].BaseValue;
	}
}
