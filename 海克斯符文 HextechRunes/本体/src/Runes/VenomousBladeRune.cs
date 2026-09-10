namespace HextechRunes;

public sealed class VenomousBladeRune : HextechRelicBase
{
	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<Shiv>(),
		HoverTipFactory.FromPower<PoisonPower>()
	];

	public override bool IsAvailableForPlayer(Player player) => IsSilentPlayer(player);

	public override decimal ModifyDamageAdditiveCompat(Creature? target, decimal amount, ValueProp props,
		Creature? dealer, CardModel? cardSource)
	{
		if (Owner == null || target == null || !HextechSts2Compat.IsPoweredAttack(props)
			|| !IsDamageFromOwnerToEnemyOrPreview(target, dealer, cardSource)
			|| !HextechKnifeHelper.IsShivLike(cardSource, Owner))
		{
			return 0m;
		}

		// 合入本次小刀攻击，预览和实际伤害读取同一目标；不另造一次伤害事件触发递归。
		return Math.Max(0m, target.GetPowerAmount<PoisonPower>());
	}
}
