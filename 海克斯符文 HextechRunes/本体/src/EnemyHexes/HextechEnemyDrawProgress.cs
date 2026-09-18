namespace HextechRunes;

internal static class HextechEnemyDrawProgress
{
	internal static int RecordDraw(Dictionary<ulong, int> counts, Player player, int threshold)
	{
		return RecordTotal(counts, player.NetId, counts.GetValueOrDefault(player.NetId) + 1, threshold);
	}

	internal static int RecordTotal(Dictionary<ulong, int> counts, ulong playerId, int drawnCards, int threshold)
	{
		int previous = counts.GetValueOrDefault(playerId);
		if (drawnCards <= previous)
		{
			return 0;
		}
		counts[playerId] = drawnCards;
		return drawnCards / threshold - previous / threshold;
	}

	internal static int ResolveFromHistory(Dictionary<ulong, int> counts, HextechCombatState combatState, int threshold)
	{
		// 联机在双方都会经过的结算点补记抽牌；重复结算不重复发奖。
		int pending = 0;
		foreach (Player player in combatState.Players.OrderBy(static player => player.NetId))
		{
			int drawn = CombatManager.Instance.History.Entries.OfType<CardDrawnEntry>()
				.Count(entry => entry.Card.Owner?.NetId == player.NetId);
			pending += RecordTotal(counts, player.NetId, drawn, threshold);
		}
		return pending;
	}
}
