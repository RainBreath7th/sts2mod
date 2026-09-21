namespace HextechRunes;

internal sealed class BadTasteEnemyHex : HextechEnemyHexEffect
{
	internal const decimal HealPercent = 1m;

	internal override MonsterHexKind Kind => MonsterHexKind.BadTaste;

	internal override async Task AfterEnemyDebuffReceived(HextechEnemyHexContext context, Creature target)
	{
		if (target.IsDead)
		{
			return;
		}

		int heal = HealAmountFor(target.MaxHp);
		if (heal > 0)
		{
			await CreatureCmd.Heal(target, heal);
		}
	}

	// 至少回复 1 点，小怪的 1% 不会被取整成 0。
	internal static int HealAmountFor(int maxHp)
	{
		return maxHp <= 0 ? 0 : Math.Max(1, (int)Math.Floor(maxHp * HealPercent / 100m));
	}
}
