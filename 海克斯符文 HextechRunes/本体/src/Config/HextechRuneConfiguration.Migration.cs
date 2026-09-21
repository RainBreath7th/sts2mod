namespace HextechRunes;

internal static partial class HextechRuneConfiguration
{
	// v4~v14 的历史迁移段与配套数组已删除:v15(0.8.4)强制重置使 ConfigVersion<15 一律整体回默认,
	// 那些分支永不可达。活跃链从 v16 起。
	// 腐化树枝生成分布加权(攻击40/技能20/能力40)后无限风险可控,转为默认启用。
	private static readonly Type[] Version16DefaultEnabledRuneTypes =
	[
		typeof(CorruptedBranchRune)
	];
	// 感受燃烧/回力OK镖重做为"获得时给卡"(0.8.4 数据驱动重做),转为默认启用。
	private static readonly Type[] Version17DefaultEnabledRuneTypes =
	[
		typeof(FeelTheBurnRune),
		typeof(OkBoomerangRune)
	];
	// 星界躯体改为百分比生命加成(50%)后强度自洽,转为默认启用。
	private static readonly Type[] Version18DefaultEnabledRuneTypes =
	[
		typeof(AstralBodyRune)
	];
	// 设计审查批次:咔咔!(代价先付收益小)/和平主义者(非亡灵自废输出),转为默认禁用。
	private static readonly Type[] Version19DefaultDisabledRuneTypes =
	[
		typeof(KakaRune),
		typeof(PacifistRune)
	];
	// 小猪存钱罐(鼓励挨打赚钱与防御方向相悖)转为默认禁用。
	private static readonly Type[] Version20DefaultDisabledRuneTypes =
	[
		typeof(PiggyBankRune)
	];
	// 升级打击/防御(围绕不该保留的牌做增强,遥测垫底)与验牌(每回合选牌拖慢节奏)转为默认禁用。
	private static readonly Type[] Version21DefaultDisabledRuneTypes =
	[
		typeof(StrikeUpgradeRune),
		typeof(DefendUpgradeRune),
		typeof(CardInspectionRune)
	];
	// 罪恶快感(开局+击杀双重资源滚雪球)转为默认禁用。
	private static readonly Type[] Version22DefaultDisabledRuneTypes =
	[
		typeof(GetExcitedRune)
	];

	// 0.8.5 遥测(69.8万局)选取率垫底批次转为默认禁用:豪猪7.7%/巨像的勇气10.6%/瓦库11.4%/
	// 死亡收割11.5%/最终形态12.8%(全体中位数30.3%)。
	private static readonly Type[] Version23DefaultDisabledRuneTypes =
	[
		typeof(ShoulderVakuRune),
		typeof(PorcupineRune),
		typeof(CourageOfColossusRune),
		typeof(DeathHarvestRune),
		typeof(FinalFormRune)
	];

	// 升级:打击/防御重做为"最高+999且战后升级本场打出过的"(棱彩),转为默认启用。
	private static readonly Type[] Version24DefaultEnabledRuneTypes =
	[
		typeof(StrikeUpgradeRune),
		typeof(DefendUpgradeRune)
	];

	// 安东尼的偏见转为默认启用(0.8.6)。
	private static readonly Type[] Version25DefaultEnabledRuneTypes =
	[
		typeof(AnthonyBiasRune)
	];

	// 高风险或流程偏慢的通用海克斯转为默认禁用;豪猪已在 v23 禁用,不重复覆盖玩家后续选择。
	private static readonly Type[] Version27DefaultDisabledRuneTypes =
	[
		typeof(OmegaRune),
		typeof(OkBoomerangRune),
		typeof(FeyMagicRune),
		typeof(AstralBodyRune)
	];

	// 以进为退转为默认启用。
	private static readonly Type[] Version30DefaultEnabledRuneTypes =
	[
		typeof(AdvanceToRetreatRune)
	];

	// 歪打正着重做为回合开始时按消耗牌堆状态牌生成充能球，转为默认启用。
	private static readonly Type[] Version31DefaultEnabledRuneTypes =
	[
		typeof(HappyAccidentRune)
	];

	private static RuneConfig NormalizeLoadedConfig(RuneConfig config)
	{
		// 0.8.4 一次性强制回默认:旧配置(含用户自定义)整体丢弃,不走增量迁移链。
		if (config.ConfigVersion < ForceResetBelowConfigVersion)
		{
			HextechLog.Info($"[{ModInfo.Id}][RuneConfig] Config version {config.ConfigVersion} < {ForceResetBelowConfigVersion}; forcing full reset to defaults (0.8.4).");
			return CreateDefaultConfig();
		}

		int previousConfigVersion = config.ConfigVersion;
		HashSet<string> disabledIds = NormalizeConfigDisabledIds(config.DisabledPlayerRuneIds);
		HashSet<string> disabledMonsterHexIds = NormalizeDisabledMonsterHexIds(config.DisabledMonsterHexIds);
		if (previousConfigVersion < 16)
		{
			disabledIds.ExceptWith(GetPlayerRuneIds(Version16DefaultEnabledRuneTypes));
		}

		if (previousConfigVersion < 17)
		{
			disabledIds.ExceptWith(GetPlayerRuneIds(Version17DefaultEnabledRuneTypes));
		}

		if (previousConfigVersion < 18)
		{
			disabledIds.ExceptWith(GetPlayerRuneIds(Version18DefaultEnabledRuneTypes));
		}

		if (previousConfigVersion < 19)
		{
			disabledIds.UnionWith(GetPlayerRuneIds(Version19DefaultDisabledRuneTypes));
		}

		if (previousConfigVersion < 20)
		{
			disabledIds.UnionWith(GetPlayerRuneIds(Version20DefaultDisabledRuneTypes));
		}

		if (previousConfigVersion < 21)
		{
			disabledIds.UnionWith(GetPlayerRuneIds(Version21DefaultDisabledRuneTypes));
		}

		if (previousConfigVersion < 22)
		{
			disabledIds.UnionWith(GetPlayerRuneIds(Version22DefaultDisabledRuneTypes));
		}

		if (previousConfigVersion < 23)
		{
			disabledIds.UnionWith(GetPlayerRuneIds(Version23DefaultDisabledRuneTypes));
		}

		if (previousConfigVersion < 24)
		{
			disabledIds.ExceptWith(GetPlayerRuneIds(Version24DefaultEnabledRuneTypes));
		}

		if (previousConfigVersion < 25)
		{
			disabledIds.ExceptWith(GetPlayerRuneIds(Version25DefaultEnabledRuneTypes));
		}

		if (previousConfigVersion < 27)
		{
			disabledIds.UnionWith(GetPlayerRuneIds(Version27DefaultDisabledRuneTypes));
		}

		if (previousConfigVersion < 28)
		{
			config.RuneRarityWeights = config.NormalRuneRarityWeights;
			config.PreventConsecutiveSilverRunes = DefaultPreventConsecutiveSilverRunes;
		}

		if (previousConfigVersion < 29)
		{
			config.GoldenRerollChancePercent = DefaultGoldenRerollChancePercent;
		}

		if (previousConfigVersion < 30)
		{
			disabledIds.ExceptWith(GetPlayerRuneIds(Version30DefaultEnabledRuneTypes));
		}

		if (previousConfigVersion < 31)
		{
			disabledIds.ExceptWith(GetPlayerRuneIds(Version31DefaultEnabledRuneTypes));
		}

		if (previousConfigVersion < 32)
		{
			HextechRarityWeights legacyWeights = ToRarityWeights(config.RuneRarityWeights, DefaultRuneRarityWeights);
			config.RuneRarityWeightsByAct = FromRarityWeightsByAct([ legacyWeights, legacyWeights, legacyWeights ]);
		}

		if (previousConfigVersion < 33 && config.MonsterHexRerollLimit == InfiniteRerollLimit)
		{
			config.MonsterHexRerollLimit = DefaultMonsterHexRerollLimit;
		}

		if (previousConfigVersion < 34)
		{
			// 默认关闭只迁移一次，后续尊重玩家手动重新启用的选择。
			disabledIds.UnionWith(GetPlayerRuneIds([typeof(IllusoryWeaponRune)]));
		}

		if (previousConfigVersion < 35)
		{
			disabledIds.UnionWith(GetPlayerRuneIds([typeof(AutoPatrolRune)]));
		}

		if (previousConfigVersion < 36)
		{
			// 我方已在 v22 默认禁用；敌方只迁移一次，之后尊重手动开启。
			disabledMonsterHexIds.Add(MonsterHexKind.GetExcited.ToString());
		}

		if (previousConfigVersion < 37)
		{
			// 只禁用我方；敌方"无本万利"不受影响。只迁移一次，之后尊重手动开启。
			disabledIds.UnionWith(GetPlayerRuneIds([typeof(SomethingForNothingRune), typeof(SoulCallingRune)]));
		}

		if (previousConfigVersion < 38)
		{
			// 我方"你肩上的瓦库"早已默认禁用；敌方只迁移一次，之后尊重手动开启。
			disabledMonsterHexIds.Add(MonsterHexKind.ShoulderVaku.ToString());
		}

		if (previousConfigVersion < 39)
		{
			// 只迁移一次，之后尊重手动开启。
			disabledIds.UnionWith(GetPlayerRuneIds([typeof(GhostFormRune), typeof(DieForYouRune)]));
		}

		config.ConfigVersion = CurrentConfigVersion;
		config.DisabledPlayerRuneIds = disabledIds;
		config.PlayerHexCountsByAct = NormalizePlayerHexCounts(config.PlayerHexCountsByAct);
		config.EnemyHexCountsByAct = NormalizeEnemyHexCounts(config.EnemyHexCountsByAct);
		config.PlayerRuneRerollLimit = ClampRerollLimit(config.PlayerRuneRerollLimit);
		config.MonsterHexRerollLimit = ClampRerollLimit(config.MonsterHexRerollLimit);
		config.DisabledMonsterHexIds = disabledMonsterHexIds;
		config.DisabledForgeIds = NormalizeDisabledForgeIds(config.DisabledForgeIds);
		config.RuneRarityWeightsByAct = FromRarityWeightsByAct(NormalizeRarityWeightsByAct(
			ToRarityWeightsByAct(config.RuneRarityWeightsByAct, DefaultRuneRarityWeightsByAct),
			DefaultRuneRarityWeightsByAct));
		config.RuneRarityWeights = null;
		config.GoldenRerollChancePercent = ClampGoldenRerollChancePercent(config.GoldenRerollChancePercent);
		config.ChaosRuneChancePercent = Math.Clamp(config.ChaosRuneChancePercent, 0, 100);
		config.FirstActRuneRarityWeights = null;
		config.NormalRuneRarityWeights = null;
		config.SecondActAfterSilverRuneRarityWeights = null;
		config.ForgeRarityWeights = FromForgeRarityWeights(NormalizeForgeRarityWeights(
			ToForgeRarityWeights(config.ForgeRarityWeights, DefaultForgeRarityWeights),
			DefaultForgeRarityWeights));
		config.RandomForgeShopPrice = ClampRandomForgeShopPrice(config.RandomForgeShopPrice);
		return config;
	}

	// 测试钩子:用真实迁移链跑一份合成配置,返回迁移后的版本号与禁用集(仅 HextechRunes.Tests 使用)。
	internal static (int ConfigVersion, IReadOnlySet<string> DisabledPlayerRuneIds) MigrateDisabledIdsForTests(int configVersion, IEnumerable<string> disabledIds)
	{
		RuneConfig config = new()
		{
			ConfigVersion = configVersion,
			DisabledPlayerRuneIds = disabledIds.ToHashSet(StringComparer.Ordinal)
		};
		RuneConfig normalized = NormalizeLoadedConfig(config);
		return (normalized.ConfigVersion, normalized.DisabledPlayerRuneIds);
	}

	internal static (int ConfigVersion, IReadOnlySet<string> DisabledMonsterHexIds) MigrateDisabledMonsterHexIdsForTests(
		int configVersion,
		IEnumerable<string> disabledIds)
	{
		RuneConfig config = new()
		{
			ConfigVersion = configVersion,
			DisabledMonsterHexIds = disabledIds.ToHashSet(StringComparer.Ordinal)
		};
		RuneConfig normalized = NormalizeLoadedConfig(config);
		return (normalized.ConfigVersion, normalized.DisabledMonsterHexIds);
	}

	internal static (int ConfigVersion, HextechRarityWeights RuneRarityWeights, bool PreventConsecutiveSilverRunes) MigrateRarityConfigForTests(
		int configVersion,
		HextechRarityWeights normalWeights,
		HextechRarityWeights afterSilverWeights)
	{
		RuneConfig config = new()
		{
			ConfigVersion = configVersion,
			NormalRuneRarityWeights = FromRarityWeights(normalWeights),
			SecondActAfterSilverRuneRarityWeights = FromRarityWeights(afterSilverWeights)
		};
		RuneConfig normalized = NormalizeLoadedConfig(config);
		return (
			normalized.ConfigVersion,
			ToRarityWeightsByAct(normalized.RuneRarityWeightsByAct, DefaultRuneRarityWeightsByAct)[0],
			normalized.PreventConsecutiveSilverRunes);
	}

	internal static HextechRarityWeights[] MigrateSingleRarityConfigForTests(
		int configVersion,
		HextechRarityWeights weights)
	{
		RuneConfig config = new()
		{
			ConfigVersion = configVersion,
			RuneRarityWeights = FromRarityWeights(weights)
		};
		RuneConfig normalized = NormalizeLoadedConfig(config);
		return ToRarityWeightsByAct(normalized.RuneRarityWeightsByAct, DefaultRuneRarityWeightsByAct);
	}

	internal static (int ConfigVersion, int MonsterHexRerollLimit) MigrateMonsterHexRerollLimitForTests(
		int configVersion,
		int rerollLimit)
	{
		RuneConfig config = new()
		{
			ConfigVersion = configVersion,
			MonsterHexRerollLimit = rerollLimit
		};
		RuneConfig normalized = NormalizeLoadedConfig(config);
		return (normalized.ConfigVersion, normalized.MonsterHexRerollLimit);
	}

	private static HashSet<string> GetPlayerRuneIds(IEnumerable<Type> runeTypes)
	{
		return HextechPlayerRuneConfigIds.FromTypes(runeTypes);
	}
}
