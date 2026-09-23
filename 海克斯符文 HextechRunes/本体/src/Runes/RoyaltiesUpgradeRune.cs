namespace HextechRunes;

public sealed class RoyaltiesUpgradeRune : CardUpgradeRuneBase<Royalties>
{
	// 兼容已有开发版本存档的未领取奖励；新的回合收益立即发放。
	private int _countThisCombat;

	protected override bool IsAvailableForCharacter(Player player) => IsRegentPlayer(player);

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
		SavedCountThisCombat = 0;
		return Task.CompletedTask;
	}

	public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
	{
		if (player == Owner && player.Creature.CombatState != null)
		{
			// 不改王国资产层数，额外金币不参与复利；同步回调由各端分别等待原版命令。
			int gold = player.Creature.GetPowerAmount<RoyaltiesPower>();
			if (gold > 0)
				return PlayerCmd.GainGold(gold, player);
		}
		return Task.CompletedTask;
	}

	public override Task AfterCombatEnd(CombatRoom room)
	{
		if (Owner != null && _countThisCombat > 0)
			HextechGoldRewardHelper.AddFixedExtraGoldReward(room, Owner, _countThisCombat);
		SavedCountThisCombat = 0;
		return Task.CompletedTask;
	}
}
