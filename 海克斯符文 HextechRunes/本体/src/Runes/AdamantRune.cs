namespace HextechRunes;

public sealed class AdamantRune : LimitedDebuffProcRelicBase
{
	protected override bool HasTurnLimit => false;

	protected override bool ListensToOwnerDebuffs => true;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new BlockVar(5m, ValueProp.Unpowered)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<BlockNextTurnPower>()
	];

	// 负面效果多半在敌方回合收到，当场给的格挡会在玩家回合开始时被清掉。
	// 统一记到原版的"下回合格挡"上：它在格挡清除之后发放（有壁垒时同样触发），自带显示、保存与联机同步。
	protected override Task OnEnemyDebuffApplied(Creature target)
	{
		return PowerCmd.Apply<BlockNextTurnPower>(Owner!.Creature, DynamicVars.Block.BaseValue, Owner!.Creature, null);
	}
}
