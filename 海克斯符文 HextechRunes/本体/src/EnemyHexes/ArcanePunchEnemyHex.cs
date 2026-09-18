namespace HextechRunes;

// 保留原敌方秘术冲拳的枚举/配置身份，旧局按升级：感染棱柱的新效果结算。
internal sealed class ArcanePunchEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.ArcanePunch;

	internal override Task ApplyCombatStartPlayerDebuffs(HextechEnemyHexContext context, CombatRoom room, IReadOnlyList<Creature> players)
	{
		return PowerCmd.Apply<HextechVitalSparkPower>(players, context.TierValue(Kind, 0, 1, 2), null, null);
	}
}
