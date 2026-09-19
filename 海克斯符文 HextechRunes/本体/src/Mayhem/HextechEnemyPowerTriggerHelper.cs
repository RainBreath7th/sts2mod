namespace HextechRunes;

internal static class HextechEnemyPowerTriggerHelper
{
	internal static bool IsEnemyDebuffReceived(PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		if (power.Owner?.Side != CombatSide.Enemy
			|| !power.Owner.IsAlive
			|| amount == 0m
			|| power is ITemporaryPower
			|| power.GetTypeForAmount(amount) != PowerType.Debuff)
		{
			return false;
		}

		// 普通减益减少层数不是再次施加。负力量等双向属性只记录外部施加的负变化；
		// TemporaryStrength 到期由 Owner 自己扣回力量，不能再次触发扇巴掌。
		return amount > 0m
			|| (power.AllowNegative
				&& power.GetTypeForAmount(-amount) == PowerType.Buff
				&& applier != power.Owner
				&& (applier != null || cardSource != null));
	}
}
