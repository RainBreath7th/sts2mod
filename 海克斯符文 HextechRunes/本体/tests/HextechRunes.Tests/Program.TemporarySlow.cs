using System.Reflection;
using System.Runtime.CompilerServices;
using HextechRunes;
using MegaCrit.Sts2.Core.Combat;

namespace HextechRunes.Tests;

internal static partial class Program
{
	private static void TemporarySlowStartDamageCannotRenewPreviousRounds()
	{
		HextechTemporarySlowPower power = (HextechTemporarySlowPower)RuntimeHelpers.GetUninitializedObject(typeof(HextechTemporarySlowPower));
		int temporaryAmount = 0;
		int visibleAmount = 17; // 独立的永久缓慢不能被临时层清理抹掉。
		for (int round = 1; round <= 40; round++)
		{
			// 最坏回调顺序：沙漏先命中、百炼成钢先加 -8，本 Power 尚未收到清理回调。
			int expired = power.RecordStackedAmount(round, -8, temporaryAmount - 8);
			temporaryAmount += -8 - expired;
			visibleAmount += -8 - expired;
			Equal(-8, temporaryAmount, "start-of-turn damage must keep only the new round's reduction");
			Equal(9, visibleAmount, "expiry must preserve permanent Slow");

			// 本回合正常受击仍能叠加减伤，不能把修复做成禁止叠层或强行封顶。
			for (int hit = 0; hit < 5; hit++)
			{
				expired = power.RecordStackedAmount(round, -8, temporaryAmount - 8);
				Equal(0, expired, "same-round damage must retain existing temporary stacks");
				temporaryAmount -= 8;
				visibleAmount -= 8;
			}
			Equal(-48, temporaryAmount, "same-round reduction remains cumulative");
		}
	}

	private static void TemporarySlowPreservesOppositeSignNewRoundStacks()
	{
		HextechTemporarySlowPower power = (HextechTemporarySlowPower)RuntimeHelpers.GetUninitializedObject(typeof(HextechTemporarySlowPower));
		Equal(0, power.RecordStackedAmount(2, -50, -50), "first negative application has no expired component");
		int expired = power.RecordStackedAmount(3, 50, 0);
		Equal(-50, expired, "old reduction must expire even when Frost Wraith cancels the displayed total");
		Equal(50, 0 - expired, "new Frost Wraith stacks must survive a net-zero transition");
		Equal(0, power.RecordStackedAmount(3, 50, 100), "multiple players' Frost Wraith applications stack in the same round");
		expired = power.RecordStackedAmount(4, -8, 92);
		Equal(100, expired, "all previous-round positive stacks expire before a new reduction");
		Equal(-8, 92 - expired, "expiry must keep the new negative stacks");
	}

	private static void TemporarySlowCleanupRunsBeforePlayerStartEffects()
	{
		MethodInfo before = typeof(HextechTemporarySlowPower).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
			.Single(method => method.Name == "BeforeSideTurnStart");
		Expect(before.IsVirtual, "temporary Slow cleanup must participate in the official pre-turn hook");
		Expect(!typeof(HextechTemporarySlowPower).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
			.Any(method => method.Name == "AfterSideTurnStart"), "cleanup must not wait until after Mercury Hourglass/Frost Wraith");
		Expect(HextechTemporarySlowPower.ShouldExpireAtSide(CombatSide.Player, 3, 2), "old stacks expire at the next player turn boundary");
		Expect(!HextechTemporarySlowPower.ShouldExpireAtSide(CombatSide.Player, 3, 3), "same-round applications and extra turns must survive");
		Expect(!HextechTemporarySlowPower.ShouldExpireAtSide(CombatSide.Enemy, 3, 2), "enemy turn start does not expire temporary Slow");
	}
}
