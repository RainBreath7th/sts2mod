namespace HextechRunes;

public sealed class BulletTimeUpgradeRune : CardUpgradeRuneBase<BulletTime>
{
	protected override bool IsAvailableForCharacter(Player player) => IsSilentPlayer(player);

	public override decimal ModifyPowerAmountGivenMultiplicative(PowerModel power, Creature giver,
		decimal amount, Creature? target, CardModel? cardSource)
	{
		return Owner != null && giver == Owner.Creature && target == Owner.Creature
			&& cardSource is BulletTime && cardSource.Owner == Owner && power is NoDrawPower ? 0m : 1m;
	}
}
