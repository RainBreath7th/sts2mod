namespace HextechRunes;

public sealed class QuantumComputingCard : HextechOwnerPoolTokenCard
{
	public override string PortraitPath => HextechAssets.QuantumComputingCardPortraitPath;

	public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];

	protected override IEnumerable<DynamicVar> CanonicalVars =>
	[
		new DynamicVar("DamagePercent", 15m),
		new DynamicVar("HealPercent", 15m)
	];

	public QuantumComputingCard()
		: base(2, CardType.Attack, CardRarity.Token, TargetType.AllEnemies, shouldShowInCardLibrary: true)
	{
	}

	protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
	{
		if (Owner?.Creature.CombatState == null)
		{
			return;
		}

		List<Creature> enemies = Owner.Creature.CombatState.HittableEnemies.ToList();
		if (enemies.Count == 0)
		{
			return;
		}

		// 表现沿用原符文:蓝紫量子光柱逐敌贯穿+吸血数据流回流(纯表现层);
		// 逻辑等待让首柱落点与首敌伤害对齐,后续逐敌结算的标准尾巴与柱间节拍一致。
		await CreatureCmd.TriggerAnim(Owner.Creature, "Cast", 0f);
		HextechCombatVfx.QuantumPulse(Owner.Creature, enemies);
		await Cmd.CustomScaledWait(0.42f, 0.55f);

		// 伤害按各敌人自己的最大生命值计算，所以逐敌结算。
		// 与原符文同口径用 Unpowered：生命比例伤害不吃力量、易伤和各类伤害系数，否则叠上重放族会按比例秒首领。
		int totalDamage = 0;
		foreach (Creature enemy in enemies)
		{
			if (enemy.IsDead)
			{
				continue;
			}

			decimal damage = Math.Floor(enemy.MaxHp * DynamicVars["DamagePercent"].BaseValue / 100m);
			if (damage <= 0m)
			{
				continue;
			}

			IEnumerable<DamageResult> results = await HextechGameApiCompat.Damage(choiceContext, enemy, damage, ValueProp.Unpowered, Owner.Creature, this, cardPlay);
			totalDamage += results.Sum(static result => result.UnblockedDamage);
		}

		int heal = (int)Math.Floor(totalDamage * DynamicVars["HealPercent"].BaseValue / 100m);
		if (heal > 0 && !Owner.Creature.IsDead)
		{
			await CreatureCmd.Heal(Owner.Creature, heal);
		}
	}

	protected override void OnUpgrade()
	{
		DynamicVars["HealPercent"].UpgradeValueBy(15m);
	}
}
