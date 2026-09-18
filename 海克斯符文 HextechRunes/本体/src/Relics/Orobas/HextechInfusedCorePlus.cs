using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models.Orbs;
using MegaCrit.Sts2.Core.Models.Relics;

namespace HextechRunes;

public sealed class HextechInfusedCorePlus : OrobasPlusRelicBase
{
	protected override RelicModel OriginalRelic => ModelDb.Relic<InfusedCore>();

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("Lightning", 5m),
		new DynamicVar("ExtraDamage", 2m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.Static(StaticHoverTip.Channeling),
		HoverTipFactory.FromOrb<LightningOrb>()
	];

	public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
	{
		if (participants.Contains(Owner.Creature) && Owner.PlayerCombatState!.TurnNumber <= 1)
		{
			for (int i = 0; i < DynamicVars["Lightning"].BaseValue; i++)
			{
				await OrbCmd.Channel<LightningOrb>(new BlockingPlayerChoiceContext(), Owner);
			}
		}
	}

	public override decimal ModifyOrbValue(OrbModel orb, decimal value)
	{
		return orb.Owner == Owner && orb is LightningOrb ? value + DynamicVars["ExtraDamage"].BaseValue : value;
	}
}
