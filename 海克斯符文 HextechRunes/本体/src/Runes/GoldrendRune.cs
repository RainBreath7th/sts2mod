using HarmonyLib;

namespace HextechRunes;

public sealed class GoldrendRune : HextechRelicBase
{
	// 原版 PlayerCmd.GainGold 对本机玩家直接播放 event:/sfx/ui/gold/gold_1..3,没有静音参数。
	private const string GoldSfxPrefix = "event:/sfx/ui/gold/";

	// 夺金每次命中都发钱,多段攻击时金币音效连响;只在本次发钱的异步作用域内静音,别处的金币音效不受影响。
	private static readonly AsyncLocal<bool> SuppressGoldSfx = new();

	// 仅保留旧存档尚未领取的战后奖励；新的触发直接发放金币。
	private int _countThisCombat;
	private bool _grantingGold;
	// 本回合是否已经响过一次金币音效;纯本地表现,不进存档、不参与联机。
	private bool _goldSfxPlayedThisTurn;

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
		_goldSfxPlayedThisTurn = false;
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

		// 纯本地表现:延后一帧播放、自带异常隔离,不影响下面的共享结算。
		HextechCombatVfx.CoinBurst(target);

		// 每回合只响第一次金币音效。
		EnsureTurnScopedStateCurrent(() => _goldSfxPlayedThisTurn = false);
		bool muteGoldSfx = _goldSfxPlayedThisTurn;
		_goldSfxPlayedThisTurn = true;

		// 金币可经鲜血神像触发受伤及反击，不能让这条反馈链再次触发自身。
		_grantingGold = true;
		bool previousSuppress = SuppressGoldSfx.Value;
		SuppressGoldSfx.Value = muteGoldSfx;
		try
		{
			await PlayerCmd.GainGold(DynamicVars["CountPerHit"].IntValue, Owner);
		}
		finally
		{
			SuppressGoldSfx.Value = previousSuppress;
			_grantingGold = false;
		}
	}

	internal static bool ShouldSuppressSfx(string? sfx)
	{
		return SuppressGoldSfx.Value && sfx != null && sfx.StartsWith(GoldSfxPrefix, StringComparison.Ordinal);
	}

	// 只在夺金发钱的作用域里、且是金币音效时跳过;其余音效与其他来源的金币音效照常播放。
	[HarmonyPatch(typeof(SfxCmd), nameof(SfxCmd.Play), typeof(string), typeof(float))]
	[HextechPatch("rune.goldrend.gold-sfx", "夺金")]
	private static class GoldSfxPatch
	{
		[HarmonyPrefix]
		private static bool Prefix(string sfx)
		{
			return !ShouldSuppressSfx(sfx);
		}
	}
}
