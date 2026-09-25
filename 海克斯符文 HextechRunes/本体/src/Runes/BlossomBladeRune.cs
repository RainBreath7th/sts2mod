namespace HextechRunes;

/// <summary>
/// 花晓之剑(仅联机):持有者每打出 1 张牌(同一张牌的重放只算一次),生命值比例最低的存活队友回复 2% 最大生命值(至少 1 点)。
/// 队友按"当前生命/最大生命"升序、再按 NetId 取第一个,各端算出同一目标;治疗照常经过对方的治疗倍率与我们的治疗。
/// </summary>
public sealed class BlossomBladeRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("HealPercent", 2m)
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsNetworkMultiplayer();
	}

	public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (!cardPlay.IsFirstInSeries
			|| Owner == null
			|| cardPlay.Card.Owner != Owner
			|| Owner.Creature.IsDead
			|| Owner.Creature.CombatState == null
			|| CombatManager.Instance.IsOverOrEnding)
		{
			return;
		}

		Creature? teammate = FindLowestHpRatioTeammate(Owner);
		if (teammate == null)
		{
			return;
		}

		Flash([teammate]);
		await CreatureCmd.Heal(teammate, GetHealAmount(teammate.MaxHp, DynamicVars["HealPercent"].BaseValue));
	}

	internal static Creature? FindLowestHpRatioTeammate(Player owner)
	{
		return owner.RunState.Players
			.Where(player => player != owner && player.Creature.IsAlive && player.Creature.MaxHp > 0)
			.OrderBy(static player => (decimal)player.Creature.CurrentHp / player.Creature.MaxHp)
			.ThenBy(static player => player.NetId)
			.Select(static player => player.Creature)
			.FirstOrDefault();
	}

	internal static decimal GetHealAmount(decimal maxHp, decimal percent)
	{
		return Math.Max(1m, Math.Floor(maxHp * percent / 100m));
	}
}
