namespace HextechRunes;

/// <summary>
/// 全心为你(仅联机):所有玩家获得的治疗量和格挡量 +25%,多人持有时乘算叠加。
/// 格挡走原版全局 Hook,持有者的遗物对每个玩家目标都返回倍率;治疗走 HextechPlayerCoefficientHelper,
/// 按全队存活持有者计数。持有者倒下后停止生效,与原版停用死亡玩家钩子的时机一致。
/// </summary>
public sealed class AllForYouRune : HextechRelicBase
{
	internal const decimal SustainMultiplier = 1.25m;

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("SustainPercent", 25m)
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsNetworkMultiplayer();
	}

	public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
	{
		return Owner != null && target.IsPlayer ? SustainMultiplier : 1m;
	}

	/// <summary>全队存活持有者带给 <paramref name="player"/> 的治疗倍率。</summary>
	internal static decimal GetTeamHealingMultiplier(Player player)
	{
		return Pow(CountAliveHolders(player, excludeSelf: false));
	}

	/// <summary>
	/// 属性悬浮只累加玩家自己遗物的格挡倍率;这里补上其他队友持有者的份额,实际格挡已由各自遗物的 Hook 结算。
	/// </summary>
	internal static decimal GetBlockMultiplierFromTeammates(Player player)
	{
		return Pow(CountAliveHolders(player, excludeSelf: true));
	}

	private static int CountAliveHolders(Player player, bool excludeSelf)
	{
		int count = 0;
		foreach (Player member in player.RunState.Players)
		{
			if ((excludeSelf && member == player) || !member.Creature.IsAlive)
			{
				continue;
			}

			if (member.GetRelic<AllForYouRune>() != null)
			{
				count++;
			}
		}

		return count;
	}

	private static decimal Pow(int count)
	{
		decimal multiplier = 1m;
		for (int i = 0; i < count; i++)
		{
			multiplier *= SustainMultiplier;
		}

		return multiplier;
	}
}
