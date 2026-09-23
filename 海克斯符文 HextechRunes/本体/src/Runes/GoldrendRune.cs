namespace HextechRunes;

public sealed class GoldrendRune : HextechRelicBase
{
	// 仅保留旧存档尚未领取的战后奖励；新的触发直接发放金币。
	private int _countThisCombat;
	private bool _grantingGold;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("CountPerHit", 10m)
	];

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

	public override Task BeforeCombatStart()
	{
		_countThisCombat = 0;
		InvokeDisplayAmountChanged();
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		if (Owner != null && _countThisCombat > 0)
		{
			HextechGoldRewardHelper.AddFixedExtraGoldReward(room, Owner, _countThisCombat);
		}

		_countThisCombat = 0;
		InvokeDisplayAmountChanged();
		return Task.CompletedTask;
	}

	public override async Task AfterDamageGiven(PlayerChoiceContext choiceContext, Creature? dealer, DamageResult result, ValueProp props, Creature target, CardModel? cardSource)
	{
		if (_grantingGold || target.Side != CombatSide.Enemy || result.TotalDamage <= 0 || !IsDamageFromOwner(dealer, cardSource))
		{
			return;
		}

		// 金币可经鲜血神像触发受伤及反击，不能让这条反馈链再次触发自身。
		_grantingGold = true;
		try
		{
			await PlayerCmd.GainGold(DynamicVars["CountPerHit"].IntValue, Owner);
		}
		finally
		{
			_grantingGold = false;
		}
	}

}
