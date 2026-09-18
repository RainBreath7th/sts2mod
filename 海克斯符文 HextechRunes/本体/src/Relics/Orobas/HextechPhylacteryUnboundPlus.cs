using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models.Relics;

namespace HextechRunes;

public sealed class HextechPhylacteryUnboundPlus : OrobasPlusRelicBase
{
	protected override RelicModel OriginalRelic => ModelDb.Relic<PhylacteryUnbound>();

	public override bool SpawnsPets => true;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new SummonVar("StartOfCombat", 10m),
		new SummonVar("StartOfTurn", 3m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.Static(StaticHoverTip.SummonStatic)];

	public override Task BeforeCombatStart()
	{
		return OstyCmd.Summon(new ThrowingPlayerChoiceContext(), Owner, DynamicVars["StartOfCombat"].BaseValue, this);
	}

	public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
	{
		if (participants.Contains(Owner.Creature))
		{
			await OstyCmd.Summon(new ThrowingPlayerChoiceContext(), Owner, DynamicVars["StartOfTurn"].BaseValue, this);
		}
	}
}
