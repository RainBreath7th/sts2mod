using HarmonyLib;
using HextechRunes;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace HextechRunes.Tests;

internal static partial class Program
{
	private static void EnemyDebuffTriggersRejectOutgoingBuffsAndExpiry()
	{
		var (_, player, _) = CreatePrismaticEnemyFixture();
		Creature enemy = CreatePrismaticTestCreature(CombatSide.Enemy, (CombatState)player.Creature.CombatState!);
		T Power<T>(Creature owner) where T : PowerModel, new()
		{
			T power = CreateMutableTestModel<T>();
			AccessTools.Property(typeof(PowerModel), nameof(PowerModel.Owner)).SetValue(power, owner);
			return power;
		}
		var weak = Power<WeakPower>(enemy);
		Expect(HextechEnemyPowerTriggerHelper.IsEnemyDebuffReceived(weak, 1, player.Creature, null), "receiving Weak triggers");
		Expect(!HextechEnemyPowerTriggerHelper.IsEnemyDebuffReceived(weak, -1, player.Creature, null), "removing Weak does not trigger");
		Expect(!HextechEnemyPowerTriggerHelper.IsEnemyDebuffReceived(Power<WeakPower>(player.Creature), 1, enemy, null), "outgoing player debuff no longer triggers");
		var strength = Power<StrengthPower>(enemy);
		Expect(!HextechEnemyPowerTriggerHelper.IsEnemyDebuffReceived(strength, 1, enemy, null), "self buff no longer triggers");
		Expect(HextechEnemyPowerTriggerHelper.IsEnemyDebuffReceived(strength, -1, player.Creature, null), "external Strength loss triggers");
		Expect(!HextechEnemyPowerTriggerHelper.IsEnemyDebuffReceived(strength, -1, enemy, null), "temporary Strength expiry must not re-arm the effects");
		Expect(!HextechEnemyPowerTriggerHelper.IsEnemyDebuffReceived(Power<HextechTemporaryStrengthLossPower>(enemy), 1, player.Creature, null), "temporary wrapper does not double-count its underlying Strength change");
		Expect(typeof(TemporaryStrengthPower).IsAssignableFrom(typeof(HextechSlapTemporaryStrengthPower)), "Slap uses native temporary Strength cleanup");
	}

	private static void NightstalkingDrawProgressIsIndependentAndSurvivesReload()
	{
		HextechMayhemCombatTrackingState state = new();
		var counts = state.NightstalkingPlayerCardsDrawnThisCombat;
		int threshold = NightstalkingEnemyHex.CardsPerSlippery;
		Equal(0, HextechEnemyDrawProgress.RecordTotal(counts, 1, 11, threshold), "eleven draws do not trigger");
		Equal(0, HextechEnemyDrawProgress.RecordTotal(counts, 2, 11, threshold), "teammates do not pool incomplete groups");
		Equal(1, HextechEnemyDrawProgress.RecordTotal(counts, 1, 12, threshold), "twelfth draw grants one proc");
		Equal(0, HextechEnemyDrawProgress.RecordTotal(counts, 1, 12, threshold), "repeated multiplayer settlement does not repeat rewards");
		Equal(0, state.PlayerCardsDrawnThisCombat.Count, "Warmog counter remains separate");
		HextechMayhemCombatTrackingState restored = new();
		HextechMayhemCombatTrackingSerializer.Restore(restored, HextechMayhemCombatTrackingSerializer.Serialize(state));
		Equal(1, HextechEnemyDrawProgress.RecordTotal(restored.NightstalkingPlayerCardsDrawnThisCombat, 2, 12, threshold), "teammate carries eleven draws through save/load");
		Equal(2, HextechEnemyDrawProgress.RecordTotal(restored.NightstalkingPlayerCardsDrawnThisCombat, 1, 36, threshold), "batched draw history grants each crossed threshold once");
		restored.PreparePlayerSideTurnStart();
		Equal(36, restored.NightstalkingPlayerCardsDrawnThisCombat[1], "draw counter spans turns");
		restored.Reset();
		Equal(0, restored.NightstalkingPlayerCardsDrawnThisCombat.Count, "next combat resets draws");
	}

	private static void GetExcitedDefaultsMigrateOnceAndRemainConfigurable()
	{
		string enemyId = MonsterHexKind.GetExcited.ToString();
		Expect(HextechRuneConfiguration.GetDefaultDisabledPlayerRuneIds().Contains(ModelDb.GetId<GetExcitedRune>().Entry), "player default is disabled");
		Expect(HextechRuneConfiguration.GetDefaultDisabledMonsterHexIds().Contains(enemyId), "enemy default is disabled");
		var migrated = HextechRuneConfiguration.MigrateDisabledMonsterHexIdsForTests(35, [MonsterHexKind.FrostWraith.ToString()]);
		Expect(migrated.DisabledMonsterHexIds.SetEquals(new[] { enemyId, MonsterHexKind.FrostWraith.ToString() }), "migration adds only Get Excited");
		var custom = HextechRuneConfiguration.MigrateDisabledMonsterHexIdsForTests(migrated.ConfigVersion, []);
		Equal(0, custom.DisabledMonsterHexIds.Count, "manual re-enable persists after migration");
		Expect(!HextechMonsterHexRegistry.Registrations.Single(row => row.Kind == MonsterHexKind.GetExcited).Disabled, "config default does not hard-remove content");
	}
}
