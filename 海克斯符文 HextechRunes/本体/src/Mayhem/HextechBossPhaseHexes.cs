using MegaCrit.Sts2.Core.Models.Monsters;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

/// <summary>
/// Boss 转阶段/延迟补发开局海克斯的判定与补发流程。由 <see cref="HextechMayhemModifier"/> 的分部转发进来。
/// Doormaker 延迟补发链目前是死分支（两个判定恒返回 false），但其 DoormakerRealStartApplied 字段在存档 JSON 形状里，
/// 原样保留，不做清理。
/// </summary>
internal static class HextechBossPhaseHexes
{
	private static readonly FieldInfo? TestSubjectRespawnsField = TryGetField(typeof(TestSubject), "_respawns");

	internal static async Task AfterOstyRevived(HextechMayhemModifier modifier, Creature osty)
	{
		if (osty.Side != CombatSide.Enemy
			|| !osty.IsAlive
			|| osty.CombatState?.RunState != modifier.ActiveRunState
			|| osty.Monster is not TestSubject testSubject
			|| osty.CombatId == null
			|| modifier.ActiveRunState.CurrentRoom is not CombatRoom room)
		{
			return;
		}

		int respawns = GetTestSubjectRespawns(testSubject);
		if (respawns <= 0)
		{
			return;
		}

		uint combatId = osty.CombatId.Value;
		int lastAppliedPhase = modifier.CombatTracking.TestSubjectPhaseStartApplied.GetValueOrDefault(combatId, 0);
		if (lastAppliedPhase >= respawns)
		{
			return;
		}

		modifier.CombatTracking.TestSubjectPhaseStartApplied[combatId] = respawns;
		HextechLog.Info($"[{ModInfo.Id}][Mayhem] Reapplying boss start hexes after TestSubject revive: combatId={combatId} respawns={respawns}");
		await modifier.ApplyBossStartHexesToEnemy(osty, room);
		HextechEnemyUi.Refresh(modifier);
	}

	internal static async Task ApplyDeferredBossStartHexes(HextechMayhemModifier modifier, HextechCombatState combatState)
	{
		if (modifier.ActiveRunState.CurrentRoom is not CombatRoom room)
		{
			return;
		}

		foreach (Creature enemy in HextechCombatCreatureHelper.GetAliveEnemies(combatState))
		{
			await TryApplyDeferredBossStartHexes(modifier, enemy, room);
		}
	}

	internal static async Task<bool> TryApplyDeferredBossStartHexes(HextechMayhemModifier modifier, Creature creature, CombatRoom room)
	{
		if (creature.Side != CombatSide.Enemy
			|| !creature.IsAlive
			|| creature.CombatState?.RunState != modifier.ActiveRunState
			|| !IsDoormakerReadyForDeferredStart(creature)
			|| creature.CombatId == null)
		{
			return false;
		}

		uint combatId = creature.CombatId.Value;
		if (!modifier.CombatTracking.DoormakerRealStartApplied.Add(combatId))
		{
			return false;
		}

		HextechLog.Info($"[{ModInfo.Id}][Mayhem] Applying deferred boss start hexes to Doormaker: combatId={combatId} maxHp={creature.MaxHp}");
		await modifier.ApplyBossStartHexesToEnemy(creature, room);
		return true;
	}

	internal static bool ShouldDeferInitialBossStartHexes(Creature creature)
	{
		return false;
	}

	private static bool IsDoormakerReadyForDeferredStart(Creature creature)
	{
		return false;
	}

	private static int GetTestSubjectRespawns(TestSubject testSubject)
	{
		return NormalizeTestSubjectRespawns(TestSubjectRespawnsField?.GetValue(testSubject));
	}

	internal static int NormalizeTestSubjectRespawns(object? fieldValue)
	{
		return fieldValue is int respawns ? Math.Max(0, respawns) : 0;
	}
}
