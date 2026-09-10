using MegaCrit.Sts2.Core.Models.RelicPools;
using MegaCrit.Sts2.Core.Unlocks;

namespace HextechRunes;

internal static class HextechNaturalRelicPoolHooks
{
	// 注册池同时服务模型身份和百科，不能因菜单开关撤销注册。符文/锻造器只由
	// 海克斯自己的选择入口发放，不应进入原版自然生成候选。尤其共享 GrabBag 的
	// Populate(IEnumerable, Rng) 不过滤 Starter，会先打乱这些条目并推进 Boss 共用的
	// UpFront RNG；生成后再 Remove 已经太晚。此过滤不依赖尚未同步的本地配置。
	internal static IEnumerable<RelicModel> FilterNaturalRelics(IEnumerable<RelicModel> relics)
	{
		return relics.Where(static relic => relic is not HextechRelicBase);
	}

	[HarmonyPatch(typeof(SharedRelicPool), nameof(SharedRelicPool.GetUnlockedRelics), typeof(UnlockState))]
	[HextechPatch("run.natural-relic-pool", "原版遗物生成池隔离")]
	private static class SharedRelicPoolPatch
	{
		[HarmonyPostfix]
		private static void Postfix(ref IEnumerable<RelicModel> __result)
		{
			__result = FilterNaturalRelics(__result);
		}
	}
}
