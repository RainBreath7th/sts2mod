using HarmonyLib;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace HextechRunes;

internal static partial class HextechCombatHooks
{
	private static HextechMayhemModifier? GetEnemyUpgradeModifier(Creature creature)
	{
		return creature.Side == CombatSide.Enemy && !creature.IsDead
			&& creature.CombatState?.RunState is RunState run
			? HextechMayhemModifier.FindIn(run) : null;
	}

	private static void AddMonsterUpgradeIntents(MonsterModel monster, MoveState move, bool rollTheft)
	{
		HextechMayhemModifier? modifier = GetEnemyUpgradeModifier(monster.Creature);
		if (modifier == null || MoveStateIntentsField == null) return;
		HextechEnemyHexContext context = new(modifier);
		bool strength = context.IsActive(MonsterHexKind.CeremonialBeast);
		bool theft = context.IsActive(MonsterHexKind.ThievingHopper);
		if (!strength && !theft) return;
		// 沿用集中解析的 MoveState.Intents 字段（0.107.1/0.110.0/0.111.0）。
		// 原版没有改写意图的 Hook；保留行动对象及原委托，避免破坏怪物状态机的引用判定。
		bool planTheft = rollTheft && theft && ThievingHopperEnemyHex.CanPlanTheft(monster.Creature, context)
			&& ThievingHopperEnemyHex.RollTheft(context, monster.Creature);
		MoveStateIntentsField.SetValue(move, ComposeMonsterUpgradeIntents(move.Intents, monster.Creature,
			strength ? context.TierValue(MonsterHexKind.CeremonialBeast, 1, 2, 3) : 0, planTheft));
	}

	internal static AbstractIntent[] ComposeMonsterUpgradeIntents(IReadOnlyList<AbstractIntent> original,
		Creature source, int strength, bool theft)
	{
		List<AbstractIntent> intents = original.Where(i => i is not CeremonialBeastStrengthIntent
			&& i is not ThievingHopperTheftIntent).ToList();
		if (strength > 0 && intents.Any(i => i is AttackIntent))
			intents.Add(new CeremonialBeastStrengthIntent(source, strength));
		if (theft) intents.Add(new ThievingHopperTheftIntent(source));
		return intents.ToArray();
	}

	internal static async Task CompleteMonsterUpgradeMove(Task original, CeremonialBeastStrengthIntent? strength, ThievingHopperTheftIntent? theft)
	{
		await original;
		if (strength != null && GetEnemyUpgradeModifier(strength.Source) is { } strengthModifier
			&& strengthModifier.HasActiveMonsterHex(MonsterHexKind.CeremonialBeast))
			await PowerCmd.Apply<StrengthPower>(strength.Source, strength.Strength, strength.Source, null);
		if (theft != null && GetEnemyUpgradeModifier(theft.Source) is { } theftModifier
			&& theftModifier.HasActiveMonsterHex(MonsterHexKind.ThievingHopper))
			await ThievingHopperEnemyHex.StealAndPlanEscape(new(theftModifier), theft.Source);
	}

	[HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.RollMove), typeof(IEnumerable<Creature>))]
	[HextechPatch("combat.monster-upgrades.roll-intents", "升级：仪式兽、升级：偷窃草蜢")]
	private static class MonsterUpgradeRollIntentsPatch
	{
		[HarmonyPostfix]
		private static void Postfix(MonsterModel __instance) => AddMonsterUpgradeIntents(__instance, __instance.NextMove, rollTheft: true);
	}

	[HarmonyPatch(typeof(MonsterModel), nameof(MonsterModel.SetMoveImmediate), typeof(MoveState), typeof(bool))]
	[HextechPatch("combat.monster-upgrades.immediate-intents", "升级：仪式兽")]
	private static class MonsterUpgradeImmediateIntentsPatch
	{
		[HarmonyPrefix]
		private static void Prefix(MonsterModel __instance, MoveState state) => AddMonsterUpgradeIntents(__instance, state, rollTheft: false);
	}

	[HarmonyPatch(typeof(MoveState), nameof(MoveState.PerformMove), typeof(IEnumerable<Creature>))]
	[HextechPatch("combat.monster-upgrades.perform-move", "升级：仪式兽、升级：偷窃草蜢")]
	private static class MonsterUpgradePerformMovePatch
	{
		[HarmonyPostfix]
		private static void Postfix(MoveState __instance, ref Task __result)
		{
			var strength = __instance.Intents.OfType<CeremonialBeastStrengthIntent>().FirstOrDefault();
			var theft = __instance.Intents.OfType<ThievingHopperTheftIntent>().FirstOrDefault();
			if (strength != null || theft != null)
				__result = CompleteMonsterUpgradeMove(__result, strength, theft);
		}
	}
}
