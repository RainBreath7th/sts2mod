namespace HextechRunes;

/// <summary>
/// 俯冲轰炸(仅联机):持有者倒下时,对所有敌人造成等于其最大生命值 50% 的伤害。
/// 原版先分发 AfterDeath、之后才停用死亡玩家的钩子,所以持有者自己的遗物能收到自己的死亡事件。
/// 伤害无来源、不吃力量与易伤,可被格挡;复活阻止了死亡(wasRemovalPrevented)时不触发。
/// </summary>
public sealed class DiveBomberRune : HextechRelicBase
{
	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("MaxHpPercent", 50m)
	];

	public override bool IsAvailableForPlayer(Player player)
	{
		return IsNetworkMultiplayer();
	}

	public override async Task AfterDeath(PlayerChoiceContext choiceContext, Creature target, bool wasRemovalPrevented, float deathAnimLength)
	{
		if (wasRemovalPrevented
			|| Owner == null
			|| target != Owner.Creature
			|| target.CombatState is not HextechCombatState combatState)
		{
			return;
		}

		decimal damage = GetDamage(target.MaxHp, DynamicVars["MaxHpPercent"].BaseValue);
		List<Creature> enemies = combatState.HittableEnemies.Where(static enemy => enemy.IsAlive).ToList();
		if (damage <= 0m || enemies.Count == 0)
		{
			return;
		}

		Flash(enemies);
		await HextechGameApiCompat.Damage(choiceContext, enemies, damage, ValueProp.Unpowered, null, null);
	}

	internal static decimal GetDamage(decimal maxHp, decimal percent)
	{
		return Math.Floor(maxHp * percent / 100m);
	}
}
