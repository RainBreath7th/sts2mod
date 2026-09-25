namespace HextechRunes;

internal sealed class CorruptHeartEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.CorruptHeart;

	internal override async Task AfterCardPlayed(HextechEnemyHexContext context, PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		// 与其它"玩家每打出 1 张牌"的敌方海克斯同口径：只计手动打出，同一张牌的重放不重复计。
		if (!cardPlay.IsFirstInSeries
			|| cardPlay.IsAutoPlay
			|| cardPlay.Card.Owner is not Player player
			|| player.Creature.Side != CombatSide.Player
			|| player.Creature.IsDead
			|| player.Creature.CombatState is not HextechCombatState combatState
			|| combatState.RunState != context.RunState)
		{
			return;
		}

		// 可被格挡、不吃力量与易伤；没有伤害来源，不触发荆棘一类的反伤。
		await HextechGameApiCompat.Damage(
			choiceContext,
			player.Creature,
			context.TierValue(Kind, 1, 1, 2),
			ValueProp.Unpowered,
			null,
			null);
	}
}
