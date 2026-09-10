using System.Runtime.CompilerServices;
using HextechRunes;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;

namespace HextechRunes.Tests;

internal static partial class Program
{
	private static void FiveEnemyUpgradesHaveStableIdentityAndAutoPatrolDisabled()
	{
		(MonsterHexKind Kind, int Id, HextechRarityTier Rarity, Type Icon)[] expected =
		[
			(MonsterHexKind.LivingFog, 135, HextechRarityTier.Prismatic, typeof(LivingFogHex)),
			(MonsterHexKind.CeremonialBeast, 136, HextechRarityTier.Prismatic, typeof(CeremonialBeastHex)),
			(MonsterHexKind.SoulFysh, 137, HextechRarityTier.Gold, typeof(SoulFyshHex)),
			(MonsterHexKind.ThievingHopper, 138, HextechRarityTier.Gold, typeof(ThievingHopperHex)),
			(MonsterHexKind.HauntedShip, 139, HextechRarityTier.Silver, typeof(HauntedShipHex))
		];
		foreach (var row in expected)
		{
			Equal(row.Id, (int)row.Kind, "append-only enemy identity");
			var registration = HextechMonsterHexRegistry.Registrations.Single(r => r.Kind == row.Kind);
			Equal(row.Rarity, registration.Rarity, "enemy rarity");
			Equal(row.Icon, registration.IconRelicType, "enemy texture carrier");
			Expect(!registration.Disabled, "new enemy hex enabled");
			Expect(HextechContentRegistry.EnemyHexIconRelicTypes.Contains(row.Icon), "icon model registered");
			Expect(!HextechPlayerRuneRegistry.Registrations.Any(r => r.Type == row.Icon), "enemy hex cannot enter player pool");
		}
		var autoPatrol = HextechPlayerRuneRegistry.Registrations.Single(r => r.Type == typeof(AutoPatrolRune));
		Expect(autoPatrol.Flags.HasFlag(PlayerRuneFlags.Disabled), "Auto Patrol disabled by default");
		string autoPatrolId = ModelDb.GetId<AutoPatrolRune>().Entry;
		var (_, migrated) = HextechRuneConfiguration.MigrateDisabledIdsForTests(34, ["custom-rune"]);
		SetEqual(new[] { autoPatrolId, "custom-rune" }, migrated, "existing config gains only Auto Patrol default disable");
		var (_, reenabled) = HextechRuneConfiguration.MigrateDisabledIdsForTests(35, []);
		Expect(!reenabled.Contains(autoPatrolId), "manual reenable after migration survives reload");
	}

	private static void MonsterUpgradeIntentsPreserveAttacksAndDoNotAccumulate()
	{
		Creature owner = (Creature)RuntimeHelpers.GetUninitializedObject(typeof(Creature));
		var attack = new MultiAttackIntent(7, 3);
		var existingBuff = new BuffIntent();
		AbstractIntent[] original = [attack, existingBuff];
		var upgraded = HextechCombatHooks.ComposeMonsterUpgradeIntents(original, owner, 2, true);
		Equal(4, upgraded.Length, "append one strength and one theft intent");
		Expect(!HextechCombatHooks.AreJeweledGauntletIntentsRepeatable(upgraded), "theft action transitions to escape and must not advertise a repeated action");
		Expect(ReferenceEquals(attack, upgraded[0]) && ReferenceEquals(existingBuff, upgraded[1]), "native intents preserve identity and order");
		Equal(2, upgraded.OfType<CeremonialBeastStrengthIntent>().Single().Strength, "strength amount preview");
		var rerolled = HextechCombatHooks.ComposeMonsterUpgradeIntents(upgraded, owner, 3, false);
		Equal(3, rerolled.Length, "reroll replaces own intents rather than accumulating them");
		Expect(!rerolled.OfType<ThievingHopperTheftIntent>().Any(), "failed next roll clears stale theft intent");
		var nonAttack = HextechCombatHooks.ComposeMonsterUpgradeIntents([existingBuff], owner, 3, true);
		Expect(!nonAttack.OfType<CeremonialBeastStrengthIntent>().Any(), "non-attack does not grant strength");
		Expect(nonAttack.OfType<ThievingHopperTheftIntent>().Any(), "non-attack may still steal");
		Equal(2, original.Length, "composition does not mutate other consumers' original list");
	}

	private static void HopperEscapeSurvivesTheNextNativeMoveRoll()
	{
		int escaped = 0;
		MoveState old = new("OLD", _ => Task.CompletedTask, new SingleAttackIntent(7));
		old.FollowUpState = old;
		var machine = new MonsterMoveStateMachine([old], old);
		old.PerformMove(Array.Empty<Creature>()).GetAwaiter().GetResult();
		machine.OnMovePerformed(old);
		MoveState escape = ThievingHopperEnemyHex.CreateEscapeMove(() => { escaped++; return Task.CompletedTask; });
		machine.States[escape.Id] = escape;
		machine.ForceCurrentState(escape);
		Expect(!escape.CanTransitionAway, "escape cannot be skipped before it performs");
		Expect(ReferenceEquals(escape, machine.RollMove(Array.Empty<Creature>(), null!, null!)), "native roll retains escape instead of advancing or throwing for unregistered state");
		Equal(0, escaped, "intent preparation cannot perform the escape early");
		escape.PerformMove(Array.Empty<Creature>()).GetAwaiter().GetResult();
		Equal(1, escaped, "escape executes once when its turn arrives");
		Expect(escape.Intents.Single() is EscapeIntent, "escape replaces the attack and theft intents");
	}

	private static void EnemyUpgradeCountersRoundTripAndStayIndependent()
	{
		var first = CreateOrdinalTestPlayer(1);
		var second = CreateOrdinalTestPlayer(2);
		HextechMayhemCombatTrackingState state = new();
		for (int i = 0; i < LivingFogEnemyHex.SkillLimit; i++)
			Expect(HextechCombatProcTracker.TryConsumePlayerRuneProcThisTurn(state, first, LivingFogEnemyHex.ProcKey, LivingFogEnemyHex.SkillLimit), "first three skills allowed");
		Expect(!HextechCombatProcTracker.TryConsumePlayerRuneProcThisTurn(state, first, LivingFogEnemyHex.ProcKey, LivingFogEnemyHex.SkillLimit), "fourth skill blocked");
		Expect(HextechCombatProcTracker.TryConsumePlayerRuneProcThisTurn(state, second, LivingFogEnemyHex.ProcKey, LivingFogEnemyHex.SkillLimit), "players have independent skill budgets");
		state.GlobalProcsThisCombat[ThievingHopperEnemyHex.TheftKey(7)] = 1;
		HextechMayhemCombatTrackingState restored = new();
		restored.Restore(state.Serialize());
		Equal(3, HextechCombatProcTracker.GetPlayerRuneProcsThisTurn(restored, first, LivingFogEnemyHex.ProcKey), "skill usage survives tracking serialization");
		Equal(1, restored.GlobalProcsThisCombat[ThievingHopperEnemyHex.TheftKey(7)], "thief quota survives serialization");
		Equal(0, restored.GlobalProcsThisCombat.GetValueOrDefault(ThievingHopperEnemyHex.TheftKey(8)), "another enemy can still steal");
		restored.PreparePlayerSideTurnStart();
		Equal(0, HextechCombatProcTracker.GetPlayerRuneProcsThisTurn(restored, first, LivingFogEnemyHex.ProcKey), "next turn resets skill budget");
		Equal(1, restored.GlobalProcsThisCombat[ThievingHopperEnemyHex.TheftKey(7)], "next turn does not reset thief quota");
	}

	private static void HopperProtectsBossesAndUsesNativeTheftPriorities()
	{
		Expect(ThievingHopperEnemyHex.IsProtectedBoss(RoomType.Boss, true), "boss body must never flee");
		Expect(!ThievingHopperEnemyHex.IsProtectedBoss(RoomType.Boss, false), "boss minions are not boss bodies");
		Expect(!ThievingHopperEnemyHex.IsProtectedBoss(RoomType.Elite, true), "elite enemies remain eligible");
		Equal(2, ThievingHopperEnemyHex.StealPriority(CreateMutableTestModel<StrikeIronclad>()), "basic card priority matches native");
	}
}
