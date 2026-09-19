namespace HextechRunes;

internal sealed class SingularityAIEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.SingularityAI;

	internal override async Task BeforePlayerSideTurnStart(HextechEnemyHexContext context, HextechCombatState combatState, IReadOnlyList<Creature> players)
	{
		if (players.Count == 0)
		{
			return;
		}

		foreach (Player player in players
			.Select(static creature => creature.Player)
			.OfType<Player>()
			.OrderBy(static player => player.NetId))
		{
			int statusCount = context.TierValue(Kind, 0, 1, 2);
			for (int i = 0; i < statusCount; i++)
			{
				int statusIndex = HextechStableRandom.Index(
					context.RunState,
					HextechEnemyStatusCards.Count,
					"singularity-ai-status",
					HextechStableRandom.PlayerKey(player),
					combatState.RoundNumber.ToString(),
					i.ToString());
				CardModel card = HextechEnemyStatusCards.Create(combatState, player, statusIndex);

				await HextechCardGeneration.AddGeneratedCardToCombat(
					card,
					PileType.Draw,
					addedByPlayer: false,
					position: CardPilePosition.Random);
			}
		}
	}

}
