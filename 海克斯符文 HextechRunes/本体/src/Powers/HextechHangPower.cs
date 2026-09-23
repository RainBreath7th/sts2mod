namespace HextechRunes;

public sealed class HextechHangPower : HextechPowerBase
{
	public override PowerType Type => PowerType.Debuff;
	public override PowerStackType StackType => PowerStackType.Counter;

	// 与原版吊杀分别叠层；此乘区不限制来源、攻击属性或出牌者，直接失去生命不属于伤害。
	public override decimal ModifyDamageMultiplicativeCompat(Creature? target, decimal amount,
		ValueProp props, Creature? dealer, CardModel? cardSource) => target == Owner ? Amount : 1m;
}
