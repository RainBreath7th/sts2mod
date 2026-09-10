namespace HextechRunes;

internal sealed class LivingFogEnemyHex : HextechEnemyHexEffect
{
	internal const int SkillLimit = 3;
	internal const string ProcKey = "enemy-living-fog-skills";
	internal override MonsterHexKind Kind => MonsterHexKind.LivingFog;

	internal override bool ShouldPlay(HextechEnemyHexContext context, CardModel card, AutoPlayType autoPlayType)
	{
		return card.Type != CardType.Skill
			|| card.Owner?.Creature.Side != CombatSide.Player
			|| card.Owner.Creature.CombatState?.RunState != context.RunState
			|| HextechCombatProcTracker.GetPlayerRuneProcsThisTurn(context.Tracking, card.Owner, ProcKey) < SkillLimit;
	}

	internal override Task BeforeCardPlayed(HextechEnemyHexContext context, CardPlay cardPlay)
	{
		CardModel card = cardPlay.Card;
		if (cardPlay.IsFirstInSeries && card.Type == CardType.Skill
			&& card.Owner?.Creature.Side == CombatSide.Player
			&& card.Owner.Creature.CombatState?.RunState == context.RunState)
		{
			// 在 OnPlay 前占用次数，技能内部自动打出的其他技能也必须遵守上限。
			// 同一张牌的重放沿用原版出牌系列，不重复占用一次出牌名额。
			HextechCombatProcTracker.TryConsumePlayerRuneProcThisTurn(context.Tracking, card.Owner, ProcKey, SkillLimit);
		}
		return Task.CompletedTask;
	}
}
