namespace HextechRunes;

public sealed class NightmareUpgradeRune : CardUpgradeRuneBase<Nightmare>
{
	protected override bool IsAvailableForCharacter(Player player) => IsSilentPlayer(player);

	internal static async Task ResolveNewNightmares(Task original, Nightmare card,
		PlayerChoiceContext context, NightmarePower[] previous)
	{
		await original;
		// 原版先选牌、清除复制模板的污染，再填入独立 NightmarePower。
		// 直接提前执行其原生复制/移除回调，保留复制语义；不提前结算之前已有的夜魇。
		foreach (NightmarePower power in card.Owner.Creature.Powers.OfType<NightmarePower>().Except(previous).ToArray())
		{
			await power.BeforeHandDraw(card.Owner, context, card.CombatState!);
		}
	}

	[HarmonyPatch(typeof(Nightmare), "OnPlay", typeof(PlayerChoiceContext), typeof(CardPlay))]
	[HextechPatch("rune.nightmare.immediate", "升级夜魇", Rune = typeof(NightmareUpgradeRune))]
	private static class ImmediateNightmarePatch
	{
		[HarmonyPrefix]
		private static void Prefix(Nightmare __instance, out NightmarePower[]? __state)
		{
			__state = __instance.Owner?.GetRelic<NightmareUpgradeRune>() != null
				? __instance.Owner.Creature.Powers.OfType<NightmarePower>().ToArray() : null;
		}

		[HarmonyPostfix]
		private static void Postfix(Nightmare __instance, PlayerChoiceContext choiceContext,
			NightmarePower[]? __state, ref Task __result)
		{
			if (__state != null) __result = ResolveNewNightmares(__result, __instance, choiceContext, __state);
		}
	}
}
