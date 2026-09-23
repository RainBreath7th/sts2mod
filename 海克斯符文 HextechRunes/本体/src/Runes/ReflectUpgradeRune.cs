namespace HextechRunes;

public sealed class ReflectUpgradeRune : CardUpgradeRuneBase<Reflect>
{
	protected override bool IsAvailableForCharacter(Player player) => IsRegentPlayer(player);

	[HarmonyPatch(typeof(ReflectPower), nameof(ReflectPower.AfterSideTurnStart))]
	[HextechPatch("rune.reflect.persistent", "升级倒映", Rune = typeof(ReflectUpgradeRune))]
	private static class PersistentReflectPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		internal static bool Prefix(ReflectPower __instance, ref Task __result)
		{
			if (__instance.Owner.Player?.GetRelic<ReflectUpgradeRune>() == null) return true;
			__result = Task.CompletedTask;
			return false;
		}
	}
}
