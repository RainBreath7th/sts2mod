namespace HextechRunes;

internal sealed class SingularityAIEnemyHex : HextechEnemyHexEffect
{
	private enum StatusKind
	{
		Burn,
		Dazed,
		Slimed,
		Wound,
		Void
	}

	private static readonly IReadOnlyList<StatusKind> StatusPool =
	[
		StatusKind.Burn,
		StatusKind.Dazed,
		StatusKind.Slimed,
		StatusKind.Wound,
		StatusKind.Void
	];

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
					StatusPool.Count,
					"singularity-ai-status",
					HextechStableRandom.PlayerKey(player),
					combatState.RoundNumber.ToString(),
					i.ToString());
				CardModel card = CreateStatusCard(combatState, player, StatusPool[statusIndex]);

				await HextechCardGeneration.AddGeneratedCardToCombat(
					card,
					PileType.Draw,
					addedByPlayer: false,
					position: CardPilePosition.Random);
			}
		}
	}

	private static CardModel CreateStatusCard(HextechCombatState combatState, Player player, StatusKind kind)
	{
		return kind switch
		{
			StatusKind.Burn => combatState.CreateCard<Burn>(player),
			StatusKind.Dazed => combatState.CreateCard<Dazed>(player),
			StatusKind.Slimed => combatState.CreateCard<Slimed>(player),
			StatusKind.Wound => combatState.CreateCard<Wound>(player),
			_ => combatState.CreateCard<MegaCrit.Sts2.Core.Models.Cards.Void>(player)
		};
	}
}
