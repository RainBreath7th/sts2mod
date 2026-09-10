namespace HextechRunes;

internal sealed class SoulFyshEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.SoulFysh;

	internal override async Task AfterShuffle(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, Player shuffler)
	{
		if (shuffler.Creature.Side != CombatSide.Player || shuffler.Creature.IsDead
			|| shuffler.Creature.CombatState is not HextechCombatState combatState
			|| combatState.RunState != context.RunState)
		{
			return;
		}
		int count = context.TierValue(Kind, 1, 2, 3);
		for (int i = 0; i < count; i++)
		{
			await HextechCardGeneration.AddGeneratedCardToCombat(
				combatState.CreateCard<Beckon>(shuffler), PileType.Draw,
				addedByPlayer: false, CardPilePosition.Random);
		}
	}
}
