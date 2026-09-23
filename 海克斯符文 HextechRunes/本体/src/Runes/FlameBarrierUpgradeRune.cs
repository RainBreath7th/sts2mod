namespace HextechRunes;

public sealed class FlameBarrierUpgradeRune : CardUpgradeRuneBase<FlameBarrier>
{
	protected override bool IsAvailableForCharacter(Player player) => IsIroncladPlayer(player);

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<FlameBarrier>(), HoverTipFactory.FromPower<HextechBurnPower>()
	];

	[HarmonyPatch(typeof(FlameBarrierPower), nameof(FlameBarrierPower.AfterDamageReceived))]
	[HextechPatch("rune.flame-barrier.burn", "升级火焰屏障", Rune = typeof(FlameBarrierUpgradeRune))]
	private static class BurnRetaliationPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(FlameBarrierPower __instance, PlayerChoiceContext choiceContext,
			Creature target, ValueProp props, Creature? dealer, ref Task __result)
		{
			if (__instance.Owner.Player?.GetRelic<FlameBarrierUpgradeRune>() == null) return true;
			// 保留原版受击条件（包括完全格挡的攻击）；只替换反击命令，持续时间仍由原版管理。
			__result = target == __instance.Owner && dealer != null && props.IsPoweredAttack()
				? PowerCmd.Apply<HextechBurnPower>(choiceContext, dealer, __instance.Amount, __instance.Owner, null)
				: Task.CompletedTask;
			return false;
		}
	}
}
