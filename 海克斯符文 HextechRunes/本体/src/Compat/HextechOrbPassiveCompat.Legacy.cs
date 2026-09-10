#if STS2_107_1
namespace HextechRunes;

internal static class HextechOrbPassiveCompat
{
	// 0.107.1 使用 OrbCmd 调度被动；0.110 起统一为 OrbModel.TriggerPassive。
	internal static async Task TriggerPassive(PlayerChoiceContext context, OrbModel orb)
	{
		var combatState = orb.Owner.Creature.CombatState;
		if (combatState == null)
		{
			return;
		}
		int count = MegaCrit.Sts2.Core.Hooks.Hook.ModifyOrbPassiveTriggerCount(combatState, orb, 1, out var modifiers);
		await MegaCrit.Sts2.Core.Hooks.Hook.AfterModifyingOrbPassiveTriggerCount(combatState, orb, modifiers);
		for (int i = 0; i < count; i++)
		{
			if (CombatManager.Instance.IsOverOrEnding || orb.Owner.Creature.IsDead)
			{
				return;
			}
			await OrbCmd.Passive(context, orb, null);
		}
	}
}
#endif
