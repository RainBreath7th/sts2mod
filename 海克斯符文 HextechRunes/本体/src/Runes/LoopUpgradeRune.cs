namespace HextechRunes;

public sealed class LoopUpgradeRune : CardUpgradeRuneBase<Loop>
{
	protected override bool IsAvailableForCharacter(Player player) => IsDefectPlayer(player);

	internal static async Task TriggerAll(LoopPower power, PlayerChoiceContext context, Player player)
	{
		if (player != power.Owner.Player || player.PlayerCombatState == null) return;
		OrbModel[] orbs = player.PlayerCombatState.OrbQueue.Orbs.ToArray();
		int rounds = power.Amount;
		for (int round = 0; round < rounds; round++)
		{
			foreach (OrbModel orb in orbs)
			{
				if (CombatManager.Instance.IsOverOrEnding || player.Creature.IsDead || player.PlayerCombatState == null) return;
				if (player.PlayerCombatState.OrbQueue.Orbs.Contains(orb))
					// 与原版循环相同，每层直接触发一次被动，不额外套用回合末被动次数修正。
					await OrbCmd.Passive(context, orb, null);
			}
		}
	}

	[HarmonyPatch(typeof(LoopPower), nameof(LoopPower.AfterPlayerTurnStart))]
	[HextechPatch("rune.loop.all-orbs", "升级循环", Rune = typeof(LoopUpgradeRune))]
	private static class LoopAllOrbsPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(LoopPower __instance, PlayerChoiceContext choiceContext, Player player, ref Task __result)
		{
			if (__instance.Owner.Player?.GetRelic<LoopUpgradeRune>() == null) return true;
			__result = TriggerAll(__instance, choiceContext, player);
			return false;
		}
	}
}
