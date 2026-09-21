namespace HextechRunes;

public sealed class QuantumComputingRune : HextechRelicBase
{
	public override bool HasUponPickupEffect => true;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new CardsVar(1)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<QuantumComputingCard>()
	];

	public override Task AfterObtained()
	{
		return AddCardCopiesToDeckOrHand<QuantumComputingCard>(DynamicVars.Cards.IntValue);
	}
}
