namespace HextechRunes;

public sealed class ParticleWallUpgradeRune : CardUpgradeRuneBase<ParticleWall>
{
	protected override bool IsAvailableForCharacter(Player player) => IsRegentPlayer(player);

	public override Task AfterCardPlayed(PlayerChoiceContext context, CardPlay cardPlay)
	{
		if (Owner != null && cardPlay.Card.Owner == Owner && cardPlay.Card is ParticleWall)
		{
			// 只改战斗卡实例，不回写 DeckVersion；本次结算后成长，下一次打出开始受益。
			cardPlay.Card.DynamicVars.Block.BaseValue += 1m;
			Flash();
		}
		return Task.CompletedTask;
	}
}
