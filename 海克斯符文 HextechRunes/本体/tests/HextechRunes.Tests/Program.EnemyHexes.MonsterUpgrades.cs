using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Models.Powers;
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
		SetEqual(new[] { autoPatrolId, ModelDb.GetId<SomethingForNothingRune>().Entry, ModelDb.GetId<SoulCallingRune>().Entry, ModelDb.GetId<GhostFormRune>().Entry, ModelDb.GetId<DieForYouRune>().Entry, "custom-rune" }, migrated, "existing config gains Auto Patrol and the later default disables, keeps custom selections");
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
		Expect(escape.CanTransitionAway, "native death, revival and stun moves must be allowed to interrupt escape");
		Expect(ReferenceEquals(escape, machine.RollMove(Array.Empty<Creature>(), null!, null!)), "native roll retains escape instead of advancing or throwing for unregistered state");
		Equal(0, escaped, "intent preparation cannot perform the escape early");
		escape.PerformMove(Array.Empty<Creature>()).GetAwaiter().GetResult();
		Equal(1, escaped, "escape executes once when its turn arrives");
		Expect(escape.Intents.Single() is EscapeIntent, "escape replaces the attack and theft intents");

		var segment = CreateMutableTestModel<DecimillipedeSegmentFront>();
		segment.Creature = (Creature)RuntimeHelpers.GetUninitializedObject(typeof(Creature));
		typeof(MonsterModel).GetField("_moveStateMachine", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
			.SetValue(segment, machine);
		MoveState pendingEscape = ThievingHopperEnemyHex.CreateEscapeMove(() => Task.CompletedTask);
		MoveState dead = new("DEAD", _ => Task.CompletedTask, new StunIntent());
		var testMode = typeof(MegaCrit.Sts2.Core.TestSupport.TestMode).GetProperty("IsOn")!;
		bool wasTestMode = (bool)testMode.GetValue(null)!;
		try
		{
			// 原版测试模式跳过 GetCreatureNode，CLI 只验证状态切换，不访问 Godot 原生节点。
			testMode.SetValue(null, true);
			segment.SetMoveImmediate(pendingEscape, forceTransition: true);
			segment.SetMoveImmediate(dead);
			Expect(ReferenceEquals(dead, segment.NextMove), "Reattach's ordinary SetMoveImmediate can replace an unperformed escape");
		}
		finally
		{
			testMode.SetValue(null, wasTestMode);
		}
	}

	private static void CorruptHeartAndEnemyBadTasteHaveStableIdentityAndVakuIsConfigurableDefaultOff()
	{
		Equal(146, (int)MonsterHexKind.CorruptHeart, "append-only enemy identity");
		Equal(147, (int)MonsterHexKind.BadTaste, "append-only enemy identity");
		var heart = HextechMonsterHexRegistry.Registrations.Single(r => r.Kind == MonsterHexKind.CorruptHeart);
		Expect(heart.Rarity == HextechRarityTier.Prismatic && !heart.Disabled && heart.IconRelicType == typeof(CorruptHeartHex), "prismatic with its own icon carrier");
		Expect(!HextechPlayerRuneRegistry.Registrations.Any(r => r.Type == typeof(CorruptHeartHex)), "enemy icon carrier cannot enter player pool");
		var badTaste = HextechMonsterHexRegistry.Registrations.Single(r => r.Kind == MonsterHexKind.BadTaste);
		Expect(badTaste.Rarity == HextechRarityTier.Silver && !badTaste.Disabled && badTaste.IconRelicType == typeof(BadTasteRune), "silver, reuses the player rune icon");
		Equal(1, BadTasteEnemyHex.HealAmountFor(40), "one percent never rounds a small enemy down to zero");
		Equal(10, BadTasteEnemyHex.HealAmountFor(1000), "one percent of max HP");
		Equal(0, BadTasteEnemyHex.HealAmountFor(0), "no max HP, no heal");

		string vaku = MonsterHexKind.ShoulderVaku.ToString();
		Expect(HextechRuneConfiguration.GetDefaultDisabledMonsterHexIds().Contains(vaku), "enemy Vaku is off by default");
		Expect(!HextechMonsterHexRegistry.Registrations.Single(r => r.Kind == MonsterHexKind.ShoulderVaku).Disabled, "default-off stays configurable, not hard-removed");
		var migrated = HextechRuneConfiguration.MigrateDisabledMonsterHexIdsForTests(37, []);
		Expect(migrated.DisabledMonsterHexIds.Contains(vaku), "existing configs gain the default disable once");
		var reenabled = HextechRuneConfiguration.MigrateDisabledMonsterHexIdsForTests(migrated.ConfigVersion, []);
		Expect(!reenabled.DisabledMonsterHexIds.Contains(vaku), "manual re-enable survives reload");

		var mockery = HextechPlayerRuneRegistry.Registrations.Single(r => r.Type == typeof(VakuuMockeryRune));
		Expect(mockery.Rarity == HextechRarityTier.Gold && !mockery.Flags.HasFlag(PlayerRuneFlags.Disabled), "gold and enabled");
	}

	private static void HopperSkipsSleepingEnemiesAndMinions()
	{
		Expect(ThievingHopperEnemyHex.HasTheftBlockingPower([new AsleepPower()]), "sleeping matriarch cannot steal");
		Expect(ThievingHopperEnemyHex.HasTheftBlockingPower([new SlumberPower()]), "slumbering enemies cannot steal");
		Expect(ThievingHopperEnemyHex.HasTheftBlockingPower([new MinionPower()]), "queen's minion cannot escape its encounter script");
		Expect(!ThievingHopperEnemyHex.HasTheftBlockingPower([]), "awake independent enemies can steal");
		Expect(!ThievingHopperEnemyHex.HasTheftBlockingPower([new ReattachPower()]), "reviving segments remain eligible without locking their state machine");
	}

	private static void BloodIdolNonCombatLossLeavesOneHp()
	{
		Equal(1, BloodIdolEnemyHex.NonCombatHpAfterGold(1), "collecting gold at one HP cannot kill a player outside combat");
		Equal(1, BloodIdolEnemyHex.NonCombatHpAfterGold(2), "two HP still pays one HP");
		Equal(39, BloodIdolEnemyHex.NonCombatHpAfterGold(40), "ordinary gold collections still cost one HP");
	}

	private static void EnemyOnlyRunsStillRequireEnemyConfirmation()
	{
		Expect(HextechRuneSelectionCoordinator.NeedsEnemyOnlySelection(0, 2, false), "enemy-only mode must not bypass the reroll screen");
		Expect(!HextechRuneSelectionCoordinator.NeedsEnemyOnlySelection(1, 2, false), "normal rune selection already includes enemy controls");
		Expect(!HextechRuneSelectionCoordinator.NeedsEnemyOnlySelection(0, 0, false), "no additions require no empty confirmation screen");
		Expect(!HextechRuneSelectionCoordinator.NeedsEnemyOnlySelection(0, 2, true), "preset challenges keep their fixed enemies");
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
