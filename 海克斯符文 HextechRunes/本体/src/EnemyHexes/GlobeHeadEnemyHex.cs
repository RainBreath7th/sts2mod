namespace HextechRunes;

internal sealed class GlobeHeadEnemyHex : HextechEnemyHexEffect
{
	internal override MonsterHexKind Kind => MonsterHexKind.GlobeHead;

	internal override async Task ApplyCombatStartPlayerDebuffs(HextechEnemyHexContext context, CombatRoom room, IReadOnlyList<Creature> players)
	{
		// 用本模组的玩家侧流电(计为可清除的 Debuff、只感染持有者自己的能力牌),非原版 GalvanicPower。
		await PowerCmd.Apply<HextechGalvanicPower>(players, context.TierValue(Kind, 2, 4, 6), null, null);
	}
}
