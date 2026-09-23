namespace HextechRunes;

public sealed class SlapRune : LimitedDebuffProcRelicBase
{
	protected override bool HasTurnLimit => false;

	protected override bool ListensToOwnerDebuffs => true;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<StrengthPower>(1m)
	];

	protected override Task OnEnemyDebuffApplied(Creature target)
	{
		return PowerCmd.Apply<StrengthPower>(Owner!.Creature, DynamicVars.Strength.BaseValue, Owner!.Creature, null);
	}
}
