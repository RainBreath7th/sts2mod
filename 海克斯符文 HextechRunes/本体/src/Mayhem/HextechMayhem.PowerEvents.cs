namespace HextechRunes;

internal sealed partial class HextechMayhemModifier
{
	public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target, decimal amount, Creature? applier, out decimal modifiedAmount)
	{
		modifiedAmount = HextechEnemyHexDispatcher.Transform(
			this, amount,
			(effect, context, current) => effect.ModifyPowerAmountReceived(context, canonicalPower, target, current, applier));
		return modifiedAmount != amount;
	}

	public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
	{
		await HextechEnemyHexDispatcher.ForEachActive(
			this,
			(effect, context) => effect.AfterPowerAmountChanged(context, power, amount, applier, cardSource));

		if (power.Owner.CombatState?.RunState == RunState
			&& HextechEnemyPowerTriggerHelper.IsEnemyDebuffReceived(power, amount, applier, cardSource))
		{
			await HextechEnemyHexDispatcher.ForEachActive(
				this,
				(effect, context) => effect.AfterEnemyDebuffReceived(context, power.Owner));
		}
	}
}
