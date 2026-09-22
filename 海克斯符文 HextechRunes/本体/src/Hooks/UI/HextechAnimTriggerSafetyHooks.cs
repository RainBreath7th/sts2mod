using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using static HextechRunes.HextechHookReflection;

namespace HextechRunes;

/// <summary>
/// 怪物动画机触发安全护栏:原版部分怪物的动画转移条件会无空判硬取 power
/// (花园鳗 GenerateAnimator 的 "Hit" 条件直接 GetPower&lt;SkittishPower&gt;().HasGainedBlockThisTurn),
/// 感受燃烧/升级:暴露剥除该 buff 后,任何受击动画触发都会 NullReferenceException,
/// 异常打穿伤害结算链导致回合卡死(本机日志实证)。动画触发纯属表现层,
/// 这里吞掉 NRE:代价只是该次动画转移不播,战斗流不再被打断。
/// </summary>
/// <remarks>
/// 作用域收窄到"本模组剥过机制类增益的怪物"(由 <see cref="HextechMonsterInteractionPolicy.RemoveMonsterBuffSafely"/> 标记):
/// 未被剥过 buff 的生物、玩家侧以及第三方在同一入口抛出的空引用一律原样抛出,不替别人把"未完成的操作"伪装成完成。
/// </remarks>
internal static class HextechAnimTriggerSafetyHooks
{
	private static readonly ConditionalWeakTable<Creature, object> BuffStrippedCreatures = new();
	private static readonly object Marker = new();

	internal static void MarkBuffStripped(Creature creature)
	{
		BuffStrippedCreatures.AddOrUpdate(creature, Marker);
	}

	internal static bool WasBuffStripped(Creature? creature)
	{
		return creature != null && BuffStrippedCreatures.TryGetValue(creature, out _);
	}

	[HarmonyPatch(typeof(NCreature), nameof(NCreature.SetAnimationTrigger), typeof(string))]
	[HextechPatch("ui.anim-trigger-safety", "动画触发安全")]
	private static class SetAnimationTriggerPatch
	{
		[HarmonyFinalizer]
		private static Exception? Finalizer(Exception? __exception, NCreature __instance, string trigger)
		{
			if (__exception is not NullReferenceException)
			{
				return __exception;
			}

			Creature? creature = __instance.Entity;
			if (creature == null || creature.Side != CombatSide.Enemy || !WasBuffStripped(creature))
			{
				return __exception;
			}

			if (HextechRunLogBudget.TryConsume("ui.animation-trigger-nre", 5))
			{
				Log.Warn($"[{ModInfo.Id}][AnimSafety] Suppressed NullReferenceException in animation trigger '{trigger}' on {creature.ModelId.Entry} (vanilla animator condition reading a power this mod removed).");
			}

			return null;
		}
	}
}
