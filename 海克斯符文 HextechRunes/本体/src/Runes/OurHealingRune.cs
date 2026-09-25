namespace HextechRunes;

/// <summary>
/// 我们的治疗(仅联机):持有者获得生命回复时,每个存活队友获得等量回复。
/// 由 HextechCombatHooks.Healing 的 CreatureCmd.Heal postfix 统一驱动,
/// 战斗内外通吃;同一异步响应链内阻断再触发,防止双持互相回血无限递归。
/// </summary>
public sealed class OurHealingRune : HextechRelicBase
{
	private static readonly HextechScopedDepthGuard ShareHealGuard = new();

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsNetworkMultiplayer();
	}

	internal static async Task ShareHolderHeal(Creature healed, decimal amount)
	{
		if (ShareHealGuard.IsActive || amount <= 0m)
		{
			return;
		}

		Player? healedPlayer = healed.Player;
		if (healedPlayer == null
			|| healed != healedPlayer.Creature
			|| healedPlayer.RunState is not RunState runState
			|| healedPlayer.GetRelic<OurHealingRune>() is not OurHealingRune rune)
		{
			return;
		}

		List<Creature> teammates = runState.Players
			.Where(player => player != healedPlayer && !player.Creature.IsDead)
			.Select(static player => player.Creature)
			.ToList();
		if (teammates.Count == 0)
		{
			return;
		}

		await ShareHealGuard.RunAsync(async () =>
		{
			rune.Flash();
			foreach (Creature teammate in teammates)
			{
				if (teammate.IsDead)
				{
					continue;
				}

				await CreatureCmd.Heal(teammate, amount);
			}
		});
	}
}
