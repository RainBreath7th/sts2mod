namespace HextechRunes;

internal sealed partial class HextechMayhemModifier
{
	public override Task AfterOstyRevived(Creature osty)
	{
		return HextechBossPhaseHexes.AfterOstyRevived(this, osty);
	}

	private Task ApplyDeferredBossStartHexes(HextechCombatState combatState)
	{
		return HextechBossPhaseHexes.ApplyDeferredBossStartHexes(this, combatState);
	}

	private Task<bool> TryApplyDeferredBossStartHexes(Creature creature, CombatRoom room)
	{
		return HextechBossPhaseHexes.TryApplyDeferredBossStartHexes(this, creature, room);
	}

	// ApplyPersistentMonsterHexes 与 ApplyMonsterCombatStartHexesToEnemy 都是别的分部里的 private 成员,
	// 这条桥接留在类内, 只对同程序集的 HextechBossPhaseHexes 开放。
	internal async Task ApplyBossStartHexesToEnemy(Creature creature, CombatRoom room)
	{
		// 转阶段补发的是"开局类"海克斯增益,薄暮法衣不应镜像(等同战斗开始时的增益)。
		using (TwilightVeilRune.BeginMirrorSuppression())
		{
			await ApplyPersistentMonsterHexes(creature, replayOneShotPowers: true);
			await HextechEnemyHexDispatcher.ForEachActive(
				this,
				(effect, context) => effect.ApplyOpeningCombatStartToEnemy(
					context,
					creature,
					room,
					replayOneShotPowers: true));
			await ApplyMonsterCombatStartHexesToEnemy(creature, room);
		}
	}

	private static bool ShouldDeferInitialBossStartHexes(Creature creature)
	{
		return HextechBossPhaseHexes.ShouldDeferInitialBossStartHexes(creature);
	}

	internal static int NormalizeTestSubjectRespawns(object? fieldValue)
	{
		return HextechBossPhaseHexes.NormalizeTestSubjectRespawns(fieldValue);
	}
}
