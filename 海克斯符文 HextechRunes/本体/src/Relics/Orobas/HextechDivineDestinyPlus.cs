using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models.Relics;

namespace HextechRunes;

public sealed class HextechDivineDestinyPlus : OrobasPlusRelicBase
{
	protected override RelicModel OriginalRelic => ModelDb.Relic<DivineDestiny>();

	// 再加一次原版升级的增量：0.107.1 为 6+(6-3)=9，0.110.0/0.111.0 为 7+(7-3)=11。
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new StarsVar((int)(2m * ModelDb.Relic<DivineDestiny>().DynamicVars.Stars.BaseValue
			- ModelDb.Relic<DivineRight>().DynamicVars.Stars.BaseValue))
	];

	public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
	{
		if (participants.Contains(Owner.Creature) && Owner.PlayerCombatState!.TurnNumber <= 1)
		{
			await PlayerCmd.GainStars(DynamicVars.Stars.BaseValue, Owner);
		}
	}
}
