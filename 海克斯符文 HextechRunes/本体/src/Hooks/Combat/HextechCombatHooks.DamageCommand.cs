namespace HextechRunes;

internal static partial class HextechCombatHooks
{
	private static readonly AsyncLocal<long[]?> ActualDamageCommandIds = new();
	private static long _nextActualDamageCommandId;

	internal static long CurrentActualDamageCommandId
	{
		get
		{
			long[]? ids = ActualDamageCommandIds.Value;
			return ids is { Length: > 0 } ? ids[^1] : 0L;
		}
	}


	// 只负责命令结束后的账目清理。AsyncLocal 的出栈不能放在这里:async 方法内对 AsyncLocal 的赋值只作用于
	// 它自己的执行上下文副本,不会回写到调用方;调用方的上下文由 Postfix 在同步返回前恢复(见 DamageCommandPatch)。
	private static async Task<T> CompleteWithActualDamageCommandReset<T>(Task<T> task, long commandId)
	{
		try
		{
			return await task;
		}
		finally
		{
			CompensationRune.ClearPendingCompensations(commandId);
			CompensationEnemyHex.ClearPendingCompensations(commandId);
			PiercingThreadRune.ClearPendingDamage(commandId);
			await ConsumeOstyRedirectedSlippery(commandId);
			ClearSlipperyReductions(commandId);
		}
	}

	private static void PopActualDamageCommand(long commandId)
	{
		long[]? current = ActualDamageCommandIds.Value;
		if (current is not { Length: > 0 })
		{
			return;
		}

		long[] next;
		if (current[^1] == commandId)
		{
			next = current[..^1];
		}
		else
		{
			next = current.Where(id => id != commandId).ToArray();
		}

		ActualDamageCommandIds.Value = next.Length == 0 ? null : next;
	}

	#if STS2_108_OR_NEWER
	[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Damage), typeof(PlayerChoiceContext), typeof(IEnumerable<Creature>), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel), typeof(CardPlay))]
	#else
	[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Damage), typeof(PlayerChoiceContext), typeof(IEnumerable<Creature>), typeof(decimal), typeof(ValueProp), typeof(Creature), typeof(CardModel))]
	#endif
	[HextechPatch("combat.damage-command", "伤害命令作用域")]
	private static class DamageCommandPatch
	{
		[HarmonyPrefix]
		private static void Prefix(out long __state)
		{
			__state = Interlocked.Increment(ref _nextActualDamageCommandId);
			long[] current = ActualDamageCommandIds.Value ?? [];
			long[] next = new long[current.Length + 1];
			Array.Copy(current, next, current.Length);
			next[^1] = __state;
			ActualDamageCommandIds.Value = next;
		}

		// Prefix 在调用方的执行上下文里入栈;原方法(async)的同步段在该上下文上运行,各 await 也捕获了带命令 ID 的上下文,
		// 所以它内部的 Hook 全程都能读到 ID。Postfix 同样运行在调用方上下文里、在返回 Task 之前同步执行,
		// 在这里出栈才能真正恢复调用方:否则调用方在这次伤害之后的预览/独立结算会一直看到残留的非零 ID。
		[HarmonyPostfix]
		private static void Postfix(long __state, ref Task<IEnumerable<DamageResult>> __result)
		{
			if (__state == 0L)
			{
				return;
			}

			__result = CompleteWithActualDamageCommandReset(__result, __state);
			PopActualDamageCommand(__state);
		}

		[HarmonyFinalizer]
		private static Exception? Finalizer(long __state, Exception? __exception)
		{
			// 原方法同步段抛异常时 Postfix 不会执行;这里兜底出栈,避免调用方带着残留 ID 继续。
			if (__exception != null && __state != 0L)
			{
				PopActualDamageCommand(__state);
			}

			return __exception;
		}
	}
}
