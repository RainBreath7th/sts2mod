namespace HextechRunes;

internal sealed class EndlessRotationEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.EndlessRotation;

	internal override Task AfterShuffle(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, Player shuffler)
	{
		if (shuffler.Creature.Side != CombatSide.Player || shuffler.Creature.IsDead
			|| shuffler.Creature.CombatState?.RunState != context.RunState)
		{
			return Task.CompletedTask;
		}

		foreach (CardModel card in PileType.Hand.GetPile(shuffler).Cards)
		{
			if (!card.EnergyCost.CostsX)
			{
				// 整回合叠加，打出再返回手牌不清除；由原版回合结束清理。
				card.EnergyCost.AddThisTurn(1);
				try
				{
					card.InvokeEnergyCostChanged();
				}
				catch (Exception ex)
				{
					Log.Warn($"[{ModInfo.Id}][EndlessRotation] Cost visual refresh failed: {ex.Message}");
				}
			}
		}
		return Task.CompletedTask;
	}
}
