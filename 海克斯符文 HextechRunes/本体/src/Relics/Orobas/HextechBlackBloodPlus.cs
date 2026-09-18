using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Rooms;

namespace HextechRunes;

public sealed class HextechBlackBloodPlus : OrobasPlusRelicBase
{
	protected override RelicModel OriginalRelic => ModelDb.Relic<BlackBlood>();

	protected override IEnumerable<DynamicVar> CanonicalVars => [new HealVar(18m)];

	public override async Task AfterCombatVictory(CombatRoom room)
	{
		if (!Owner.Creature.IsDead)
		{
			Flash();
			await CreatureCmd.Heal(Owner.Creature, DynamicVars.Heal.BaseValue);
		}
	}
}
