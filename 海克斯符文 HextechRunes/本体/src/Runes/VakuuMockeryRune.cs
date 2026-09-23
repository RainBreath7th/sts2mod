using MegaCrit.Sts2.Core.Saves;

namespace HextechRunes;

public sealed class VakuuMockeryRune : HextechRelicBase
{
	public override bool HasUponPickupEffect => true;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("Relics", 2m)
	];

	public override async Task AfterObtained()
	{
		if (Owner == null)
		{
			return;
		}

		Flash();
		for (int i = 0; i < DynamicVars["Relics"].IntValue; i++)
		{
			// 逐件抽取并入手：第二件抽取时第一件已在身上，不会抽到同一件。
			RelicModel relic = HextechAncientRelicHelper.CreateRandomVakuuRelic(Owner, "vakuu-mockery");
			SaveManager.Instance.MarkRelicAsSeen(relic);
			await RelicCmd.Obtain(relic, Owner);
		}
	}
}
