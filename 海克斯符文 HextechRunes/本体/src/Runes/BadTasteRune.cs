namespace HextechRunes;

public sealed class BadTasteRune : LimitedDebuffProcRelicBase
{
	protected override bool HasTurnLimit => false;

	protected override bool ListensToOwnerDebuffs => true;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new HealVar(2m)
	];

	protected override Task OnEnemyDebuffApplied(Creature target)
	{
		return CreatureCmd.Heal(Owner!.Creature, DynamicVars.Heal.BaseValue);
	}
}
