namespace HextechRunes;

/// <summary>
/// 炽燃利息:攻击牌附带 1 层灼烧;敌人每受到 1 点灼烧伤害立即获得 2 金币。
/// 灼烧伤害的识别走 HextechBurnPower.IsResolvingDamage
/// (结算时 dealer/cardSource 均为 null,无法用来源判断)。
/// </summary>
public sealed class BurningInterestRune : HextechRelicBase
{
	// 仅保留旧存档尚未领取的战后奖励；新的触发直接发放金币。
	private int _countThisCombat;
	private bool _grantingGold;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedCountThisCombat
	{
		get => _countThisCombat;
		set
		{
			_countThisCombat = Math.Max(0, value);
			InvokeDisplayAmountChanged();
		}
	}

	public override bool ShowCounter => CombatManager.Instance?.IsInProgress == true && !IsCanonical && _countThisCombat > 0;

	public override int DisplayAmount => !IsCanonical ? _countThisCombat : 0;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new PowerVar<HextechBurnPower>(1m),
		new DynamicVar("CountPerDamage", 2m)
	];

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromPower<HextechBurnPower>()
	];

	public override Task BeforeCombatStart()
	{
		ResetCount();
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		if (Owner != null && _countThisCombat > 0)
		{
			HextechGoldRewardHelper.AddFixedExtraGoldReward(room, Owner, _countThisCombat);
		}

		ResetCount();
		return Task.CompletedTask;
	}

	public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
	{
		if (Owner == null || target.Side != CombatSide.Enemy || !IsAttackDamageForRuneEffects(props, cardSource) || !IsDamageFromOwner(dealer, cardSource))
		{
			return;
		}

		await PowerCmd.Apply<HextechBurnPower>(target, DynamicVars["HextechBurnPower"].BaseValue, Owner.Creature, cardSource);
	}

	// 演出用"无限血"怪的判定阈值:瀑布兽终幕演出为 999,999,999 血,正常怪(含联机/无尽)远低于此。
	// 灼烧按当前生命百分比结算,对这类怪一跳就是千万级伤害,计息会瞬间爆炸,直接排除。
	private const decimal InfiniteHpThreshold = 10_000_000m;

	public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
	{
		if (_grantingGold
			|| Owner == null
			|| target.Side != CombatSide.Enemy
			|| !HextechBurnPower.IsResolvingDamage
			|| result.TotalDamage <= 0m
			|| target.MaxHp >= InfiniteHpThreshold)
		{
			return;
		}

		// GainGold 的受伤反馈仍处于灼烧作用域内，不能递归计息。
		_grantingGold = true;
		try
		{
			await PlayerCmd.GainGold((int)result.TotalDamage * DynamicVars["CountPerDamage"].IntValue, Owner);
			Flash();
		}
		finally
		{
			_grantingGold = false;
		}
	}

	private void ResetCount()
	{
		_countThisCombat = 0;
		InvokeDisplayAmountChanged();
	}
}
