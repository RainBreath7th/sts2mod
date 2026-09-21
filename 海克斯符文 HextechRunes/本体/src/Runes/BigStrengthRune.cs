namespace HextechRunes;

public sealed class BigStrengthRune : HextechRelicBase
{
	private const decimal DamageMultiplierValue = 1.2m;
	private const decimal DamageBonusPercentValue = (DamageMultiplierValue - 1m) * 100m;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("DamageMultiplier", DamageMultiplierValue),
		new DynamicVar("DamageBonusPercent", DamageBonusPercentValue)
	];

	public override decimal ModifyDamageMultiplicativeCompat(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		return IsDamageFromOwnerToEnemyOrPreview(target, dealer, cardSource) ? DynamicVars["DamageMultiplier"].BaseValue : 1m;
	}
}
