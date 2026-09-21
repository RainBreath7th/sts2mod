using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace HextechRunes;

internal sealed class MysteryEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.Mystery;

	internal override async Task BeforePlayerSideTurnStart(HextechEnemyHexContext context, HextechCombatState combatState, IReadOnlyList<Creature> players)
	{
		// 第一轮牌堆已建立、尚未抽起手牌；按战斗记账，额外回合或后续重入不会再变化一次。
		if (combatState.RoundNumber != 1
			|| HextechCombatProcTracker.ConsumeGlobalProcInCombat(context.Tracking, "enemy-mystery-opening-transform") > 0)
		{
			return;
		}

		int count = context.TierValue(Kind, 2, 4, 6);
		foreach (Player player in players.Where(c => !c.IsDead).Select(c => c.Player).OfType<Player>().OrderBy(p => p.NetId))
		{
			if (player.PlayerCombatState == null)
			{
				continue;
			}

			List<CardModel> chosen = ChooseCards(context, player, count);
			if (chosen.Count == 0)
			{
				continue;
			}

			List<CardTransformation> transformations = new(chosen.Count);
			for (int i = 0; i < chosen.Count; i++)
			{
				transformations.Add(CardTransformUpgradeHelper.CreateStableOptionTransformation(
					chosen[i],
					CardFactory.GetDefaultTransformationOptions(chosen[i], true),
					(RunState)context.RunState,
					"enemy-mystery-transform-replacement",
					i,
					HextechStableRandom.PlayerKey(player)));
			}

			// 一次批量变化、一个预览界面；替换牌已确定，不需要 rng。
			await CardCmd.Transform(transformations, null, CardPreviewStyle.GridLayout);
		}
	}

	private static List<CardModel> ChooseCards(HextechEnemyHexContext context, Player player, int count)
	{
		List<CardModel> candidates = player.PlayerCombatState!.AllCards
			.Where(card => card.Owner == player && CardTransformUpgradeHelper.CanTransformToRandomCardInCombatPiles(card))
			.OrderBy(HextechStableRandom.CardKey, StringComparer.Ordinal)
			.ToList();

		List<CardModel> chosen = new(Math.Min(count, candidates.Count));
		// 逐档取牌：先非基础牌，再打击/防御以外的基础牌，最后才轮到基础打击和防御。
		foreach (IGrouping<int, CardModel> tier in candidates.GroupBy(GetPriorityTier).OrderBy(static group => group.Key))
		{
			List<CardModel> remaining = tier.ToList();
			while (chosen.Count < count && remaining.Count > 0)
			{
				CardModel pick = HextechStableRandom.Pick(
					remaining,
					(RunState)context.RunState,
					HextechStableRandom.CardKey,
					"enemy-mystery-transform",
					HextechStableRandom.PlayerKey(player),
					tier.Key.ToString(),
					chosen.Count.ToString(),
					HextechStableRandom.CardPileKey(remaining));
				chosen.Add(pick);
				remaining.Remove(pick);
			}

			if (chosen.Count >= count)
			{
				break;
			}
		}

		return chosen;
	}

	internal static int GetPriorityTier(CardModel card)
	{
		if (card.IsBasicStrikeOrDefend)
		{
			return 2;
		}

		return card.Rarity == CardRarity.Basic ? 1 : 0;
	}
}
