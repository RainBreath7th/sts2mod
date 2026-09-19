namespace HextechRunes;

internal sealed class NightstalkingEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.Nightstalking;
	internal const int CardsPerSlippery = 12;

	internal override async Task AfterCardDrawn(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
	{
		if (card.Owner?.Creature.Side != CombatSide.Player
			|| card.Owner.Creature.CombatState?.RunState != context.RunState
			|| HextechPlayerContextHelper.IsNetworkMultiplayerRun())
		{
			return;
		}

		Player owner = card.Owner;
		int cardsPerSlippery = CardsPerSlippery;
		if (HextechEnemyDrawProgress.RecordDraw(context.Tracking.NightstalkingPlayerCardsDrawnThisCombat, owner, cardsPerSlippery) == 0)
		{
			return;
		}

		HextechCombatState combatState = owner.Creature.CombatState;
		foreach (Creature enemy in context.GetAliveEnemies(combatState))
		{
			await HextechEnemyPowerScalingHooks.ApplyExact<SlipperyPower>(enemy, 1m, enemy, null);
		}
	}

	internal override Task AfterCardPlayedLate(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		return HextechPlayerContextHelper.IsNetworkMultiplayerRun() && cardPlay.Card.Owner?.Creature.CombatState is HextechCombatState combatState
			? ResolveDrawProgressFromHistory(context, combatState)
			: Task.CompletedTask;
	}

	internal override Task AfterPlayerTurnStartLate(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, Player player)
	{
		return HextechPlayerContextHelper.IsNetworkMultiplayerRun() && player.Creature.CombatState is HextechCombatState combatState
			? ResolveDrawProgressFromHistory(context, combatState)
			: Task.CompletedTask;
	}


	internal override Task BeforeTurnEnd(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, CombatSide side, CombatRoom? combatRoom)
	{
		return side == CombatSide.Player && combatRoom != null && HextechPlayerContextHelper.IsNetworkMultiplayerRun()
			? ResolveDrawProgressFromHistory(context, combatRoom.CombatState)
			: Task.CompletedTask;
	}

	private static async Task ResolveDrawProgressFromHistory(HextechEnemyHexContext context, HextechCombatState combatState)
	{
		if (combatState.RunState != context.RunState)
		{
			return;
		}

		int cardsPerSlippery = CardsPerSlippery;
		int pendingSlippery = HextechEnemyDrawProgress.ResolveFromHistory(context.Tracking.NightstalkingPlayerCardsDrawnThisCombat, combatState, cardsPerSlippery);

		if (pendingSlippery <= 0)
		{
			return;
		}

		foreach (Creature enemy in context.GetAliveEnemies(combatState))
		{
			await HextechEnemyPowerScalingHooks.ApplyExact<SlipperyPower>(enemy, pendingSlippery, enemy, null);
		}
	}

}
