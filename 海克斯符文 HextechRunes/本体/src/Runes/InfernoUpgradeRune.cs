using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace HextechRunes;

public sealed class InfernoUpgradeRune : CardUpgradeRuneBase<Inferno>
{
	protected override bool IsAvailableForCharacter(Player player) => IsIroncladPlayer(player);

	protected override IEnumerable<IHoverTip> ExtraHoverTips =>
	[
		HoverTipFactory.FromCard<Inferno>(), HoverTipFactory.FromPower<HextechBurnPower>()
	];

	internal static async Task DamageAndBurn(PlayerChoiceContext context, InfernoPower power, Creature target, DamageResult result)
	{
		if (target != power.Owner || result.UnblockedDamage <= 0 || power.Owner.CombatState!.CurrentSide != power.Owner.Side)
			return;
		Creature[] enemies = power.CombatState!.HittableEnemies.ToArray();
		// 狱火原版的伤害命令没有 cardSource；直接使用这次命令的结果，避免把连锁伤害误算为狱火。
		var results = await CreatureCmd.Damage(context, power.CombatState!.HittableEnemies, power.Amount, ValueProp.Unpowered, power.Owner);
		foreach (DamageResult damage in results)
		{
			if (damage.Receiver.IsAlive && damage.TotalDamage > 0)
				await PowerCmd.Apply<HextechBurnPower>(context, damage.Receiver, damage.TotalDamage, power.Owner, null);
		}
		// 资源与节点只存在于本机；共享伤害和灼烧先结算，表现失败不能截断 Hook。
		try
		{
			foreach (Creature enemy in enemies)
				NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(NFireBurstVfx.Create(enemy, 0.75f));
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][InfernoUpgrade] Fire burst visual failed: {ex.Message}");
		}
	}

	[HarmonyPatch(typeof(InfernoPower), nameof(InfernoPower.AfterDamageReceived))]
	[HextechPatch("rune.inferno.burn", "升级狱火", Rune = typeof(InfernoUpgradeRune))]
	private static class InfernoBurnPatch
	{
		[HarmonyPrefix]
		[HarmonyPriority(Priority.Low)]
		private static bool Prefix(InfernoPower __instance, PlayerChoiceContext choiceContext,
			Creature target, DamageResult result, ref Task __result)
		{
			if (__instance.Owner.Player?.GetRelic<InfernoUpgradeRune>() == null) return true;
			__result = DamageAndBurn(choiceContext, __instance, target, result);
			return false;
		}
	}
}
