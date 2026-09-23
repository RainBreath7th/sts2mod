namespace HextechRunes;

public sealed class PiggyBankRune : HextechRelicBase, IHextechSharedCombatVictoryRune
{
	// 仅保留旧存档尚未领取的战后奖励；新的触发直接发放金币。
	private int _counter;
	private bool _grantingGold;

	[SavedProperty(SerializationCondition.SaveIfNotTypeDefault)]
	public int SavedCounter
	{
		get => _counter;
		set
		{
			_counter = Math.Max(0, value);
			InvokeDisplayAmountChanged();
		}
	}

	public override bool HasUponPickupEffect => true;

	public override bool ShowCounter => CombatManager.Instance?.IsInProgress == true && !IsCanonical && _counter > 0;

	public override int DisplayAmount => _counter;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new GoldVar(200),
		new DynamicVar("CounterGain", 20m)
	];

	public override Task AfterObtained()
	{
		return Owner == null ? Task.CompletedTask : GrantGold(DynamicVars.Gold.BaseValue);
	}

	public override Task BeforeCombatStart()
	{
		SavedCounter = 0;
		return Task.CompletedTask;
	}

	public override Task AfterDamageReceived(
		PlayerChoiceContext choiceContext,
		Creature target,
		DamageResult result,
		ValueProp props,
		Creature? dealer,
		CardModel? cardSource)
	{
		if (_grantingGold
			|| Owner == null
			|| target != Owner.Creature
			|| result.UnblockedDamage <= 0m)
		{
			return Task.CompletedTask;
		}

		Flash();
		return GrantGold(DynamicVars["CounterGain"].IntValue);
	}

	private async Task GrantGold(decimal amount)
	{
		// 鲜血神像会在获得金币时造成伤害；拾取奖励同样必须覆盖这条反馈链。
		_grantingGold = true;
		try
		{
			await PlayerCmd.GainGold(amount, Owner);
		}
		finally
		{
			_grantingGold = false;
		}
	}

	public override Task AfterCombatVictory(CombatRoom room)
	{
		if (IsNetworkMultiplayer())
		{
			return Task.CompletedTask;
		}

		return ApplySharedCombatVictory(room);
	}

	public Task ApplySharedCombatVictory(CombatRoom room)
	{
		if (Owner == null || _counter <= 0)
		{
			SavedCounter = 0;
			return Task.CompletedTask;
		}

		HextechGoldRewardHelper.AddFixedExtraGoldReward(room, Owner, _counter);
		Flash(Array.Empty<Creature>());
		SavedCounter = 0;
		return Task.CompletedTask;
	}
}
