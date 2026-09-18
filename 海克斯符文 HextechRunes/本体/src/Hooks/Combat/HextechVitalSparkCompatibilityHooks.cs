using HarmonyLib;

namespace HextechRunes;

internal static class HextechVitalSparkCompatibilityHooks
{
	// 原版会覆盖污染层数，移除时还会清除全队污染牌；模型 Hook 的遍历顺序不能保证谁最后写入。
	// 等原版命令完成后，只为持有玩家版活力火花的人重算总量，不替换原版施加污染的流程。
	internal static async Task RefreshAfterNative(Task original, Creature owner)
	{
		await original;
		if (owner.CombatState is not { } combatState)
		{
			return;
		}
		foreach (Player player in combatState.Players)
		{
			if (player.Creature.GetPower<HextechVitalSparkPower>() is { } power)
			{
				await power.AfflictOwnerSkillCards();
			}
		}
	}

	[HarmonyPatch(typeof(VitalSparkPower), nameof(VitalSparkPower.BeforeCombatStart))]
	[HextechPatch("power.vital-spark.combat-start", "活力火花污染叠加")]
	private static class CombatStartPatch
	{
		[HarmonyPostfix]
		private static void Postfix(VitalSparkPower __instance, ref Task __result) =>
			__result = RefreshAfterNative(__result, __instance.Owner);
	}

	[HarmonyPatch(typeof(VitalSparkPower), nameof(VitalSparkPower.AfterPowerAmountChanged))]
	[HextechPatch("power.vital-spark.amount", "活力火花污染叠加")]
	private static class AmountPatch
	{
		[HarmonyPostfix]
		private static void Postfix(VitalSparkPower __instance, PowerModel power, ref Task __result)
		{
			if (power == __instance)
			{
				__result = RefreshAfterNative(__result, __instance.Owner);
			}
		}
	}

	[HarmonyPatch(typeof(VitalSparkPower), nameof(VitalSparkPower.AfterRemoved))]
	[HextechPatch("power.vital-spark.removed", "活力火花污染叠加")]
	private static class RemovedPatch
	{
		[HarmonyPostfix]
		private static void Postfix(Creature oldOwner, ref Task __result) =>
			__result = RefreshAfterNative(__result, oldOwner);
	}
}
