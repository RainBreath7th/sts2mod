namespace HextechRunes;

public sealed class SacrificeRune : HextechRelicBase
{
	private const decimal SustainMultiplierValue = 1.1m;
	private const decimal SustainBonusPercentValue = (SustainMultiplierValue - 1m) * 100m;

	// 仅保留旧存档尚未领取的战后奖励；新的触发直接发放金币。
	private int _countThisCombat;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("CountPerEnemy", 5m),
		new DynamicVar("SustainMultiplier", SustainMultiplierValue),
		new DynamicVar("SustainBonusPercent", SustainBonusPercentValue)
	];

	public decimal SustainMultiplier => DynamicVars["SustainMultiplier"].BaseValue;

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

	public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player != Owner || player.Creature.CombatState == null)
		{
			return Task.CompletedTask;
		}

		int gold = player.Creature.CombatState.Enemies.Count(static enemy => enemy.IsAlive && enemy.Side == CombatSide.Enemy) * DynamicVars["CountPerEnemy"].IntValue;
		return gold > 0 ? PlayerCmd.GainGold(gold, player) : Task.CompletedTask;
	}

	public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
	{
		return target == Owner?.Creature ? SustainMultiplier : 1m;
	}

}
