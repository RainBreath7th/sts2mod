namespace HextechRunes;

internal sealed class WarmogsSpiritEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.WarmogsSpirit;

	internal override async Task AfterCardDrawn(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
	{
		if (card.Owner?.Creature.Side != CombatSide.Player
			|| card.Owner.Creature.CombatState?.RunState != context.RunState
			|| HextechPlayerContextHelper.IsNetworkMultiplayerRun())
		{
			return;
		}

		Player owner = card.Owner;
		int cardsPerPlating = context.TierValue(Kind, 8, 6, 4);
		if (HextechEnemyDrawProgress.RecordDraw(context.Tracking.PlayerCardsDrawnThisCombat, owner, cardsPerPlating) == 0)
		{
			return;
		}

		HextechCombatState combatState = owner.Creature.CombatState;
		foreach (Creature enemy in context.GetAliveEnemies(combatState))
		{
			await HextechEnemyPowerScalingHooks.Apply<PlatingPower>(enemy, 1m, enemy, null);
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

		int cardsPerPlating = context.TierValue(MonsterHexKind.WarmogsSpirit, 8, 6, 4);
		int pendingPlating = HextechEnemyDrawProgress.ResolveFromHistory(context.Tracking.PlayerCardsDrawnThisCombat, combatState, cardsPerPlating);

		if (pendingPlating <= 0)
		{
			return;
		}

		foreach (Creature enemy in context.GetAliveEnemies(combatState))
		{
			await HextechEnemyPowerScalingHooks.Apply<PlatingPower>(enemy, pendingPlating, enemy, null);
		}
	}

}
