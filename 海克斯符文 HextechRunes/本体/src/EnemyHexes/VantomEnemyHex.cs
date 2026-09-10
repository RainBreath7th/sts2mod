namespace HextechRunes;

internal sealed class VantomEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.Vantom;

	internal const decimal MaxHpPerStack = 30m;

	internal override Task ApplyCombatStartToEnemy(HextechEnemyHexContext context, Creature enemy, CombatRoom room)
	{
		// 按联机缩放后的最终血量结算。注意裸 PowerCmd.Apply 会被原版按 ShouldScaleInMultiplayer
		// 再 ×玩家数(玩家实测 304 血 25 层),必须走 ApplyExact 按原值应用。
		int stacks = (int)Math.Floor(enemy.MaxHp / MaxHpPerStack);
		return stacks > 0
			? HextechEnemyPowerScalingHooks.ApplyExact<SlipperyPower>(enemy, stacks, enemy, null)
			: Task.CompletedTask;
	}
}
