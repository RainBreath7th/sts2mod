namespace HextechRunes;

internal sealed class HauntedShipEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.HauntedShip;

	internal override async Task BeforePlayerSideTurnStart(HextechEnemyHexContext context, HextechCombatState combatState, IReadOnlyList<Creature> players)
	{
		// 第一轮牌堆已建立；按战斗记账，避免额外回合或后续召唤再次污染弃牌堆。
		if (combatState.RoundNumber != 1
			|| HextechCombatProcTracker.ConsumeGlobalProcInCombat(context.Tracking, "enemy-haunted-ship-opening") > 0)
		{
			return;
		}
		foreach (Player player in players.Where(c => !c.IsDead).Select(c => c.Player).OfType<Player>().OrderBy(p => p.NetId))
		{
			for (int i = 0; i < context.TierValue(Kind, 2, 3, 4); i++)
			{
				await HextechCardGeneration.AddGeneratedCardToCombat(
					combatState.CreateCard<Dazed>(player), PileType.Discard,
					addedByPlayer: false, CardPilePosition.Top);
			}
		}
	}
}
