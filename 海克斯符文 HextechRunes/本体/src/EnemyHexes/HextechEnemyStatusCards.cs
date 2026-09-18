namespace HextechRunes;

internal static class HextechEnemyStatusCards
{
	internal const int Count = 5;

	// 固定顺序沿用奇点 AI，保证相同随机索引仍对应相同状态牌。
	internal static CardModel Create(HextechCombatState combatState, Player player, int index)
	{
		return index switch
		{
			0 => combatState.CreateCard<Burn>(player),
			1 => combatState.CreateCard<Dazed>(player),
			2 => combatState.CreateCard<Slimed>(player),
			3 => combatState.CreateCard<Wound>(player),
			4 => combatState.CreateCard<MegaCrit.Sts2.Core.Models.Cards.Void>(player),
			_ => throw new ArgumentOutOfRangeException(nameof(index))
		};
	}
}
