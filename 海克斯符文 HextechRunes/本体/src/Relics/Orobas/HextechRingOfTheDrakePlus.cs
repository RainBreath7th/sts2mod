using MegaCrit.Sts2.Core.Models.Relics;

namespace HextechRunes;

public sealed class HextechRingOfTheDrakePlus : OrobasPlusRelicBase
{
	protected override RelicModel OriginalRelic => ModelDb.Relic<RingOfTheDrake>();

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new CardsVar(2),
		new DynamicVar("Turns", 5m)
	];

	public override decimal ModifyHandDraw(Player player, decimal count)
	{
		return player == Owner && player.PlayerCombatState!.TurnNumber <= DynamicVars["Turns"].BaseValue
			? count + DynamicVars.Cards.BaseValue
			: count;
	}
}
