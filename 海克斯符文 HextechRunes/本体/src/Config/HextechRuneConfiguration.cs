namespace HextechRunes;

internal static partial class HextechRuneConfiguration
{
	private const string ConfigFileName = "rune_config.json";
	// v15(0.8.4):一次性强制重置——旧版本配置载入时整体丢弃回默认(含禁用池/数量/权重/重随/价格/总开关)。
	private const int CurrentConfigVersion = 39;
	private const int ForceResetBelowConfigVersion = 15;
	private const int HexActCount = 3;
	private const int MinActHexCount = 0;
	private const int MaxActHexCount = 6;
	public const int InfiniteRerollLimit = -1;
	private const int MinFiniteRerollLimit = 0;
	private const int MaxFiniteRerollLimit = 9;
	private const int MinRarityWeight = 0;
	private const int MaxRarityWeight = 999;
	private const int MinRandomForgeShopPrice = 0;
	private const int MaxRandomForgeShopPrice = 9999;
	private const int DefaultRandomForgeShopPrice = 250;
	private const bool DefaultRandomForgeDirectGrant = false;
	private const bool DefaultPreventConsecutiveSilverRunes = true;
	private const int DefaultGoldenRerollChancePercent = 5;
	private const int MinGoldenRerollChancePercent = 0;
	private const int MaxGoldenRerollChancePercent = 100;
	// 模组总开关默认开启:关闭后本局表现得与原版一致(开局时快照,联机按房主)。
	private const bool DefaultModEnabled = true;
	private const int DefaultPlayerRuneRerollLimit = 1;
	private const int DefaultMonsterHexRerollLimit = 1;

	private static readonly object SyncRoot = new();
	private static RuneConfig _config = new();
	private static bool _loaded;

	public static bool HasDisabledPlayerRunes
	{
		get
		{
			EnsureLoaded();
			lock (SyncRoot)
			{
				return _config.DisabledPlayerRuneIds.Count > 0;
			}
		}
	}

	public static void Initialize()
	{
		EnsureLoaded();
	}

	public static int[] GetEnemyHexCountsByAct()
	{
		EnsureLoaded();
		lock (SyncRoot)
		{
			return NormalizeEnemyHexCounts(_config.EnemyHexCountsByAct);
		}
	}

	public static int[] GetPlayerHexCountsByAct()
	{
		EnsureLoaded();
		lock (SyncRoot)
		{
			return NormalizePlayerHexCounts(_config.PlayerHexCountsByAct);
		}
	}

	public static bool IsPlayerRuneEnabled(RelicModel relic)
	{
		ModelId id = relic.CanonicalInstance?.Id ?? relic.Id;
		return IsPlayerRuneEnabled(id.Entry);
	}

	public static bool IsPlayerRuneEnabled(string id)
	{
		EnsureLoaded();
		lock (SyncRoot)
		{
			return !_config.DisabledPlayerRuneIds.Contains(id);
		}
	}

	public static IReadOnlySet<string> GetDisabledPlayerRuneIds()
	{
		EnsureLoaded();
		lock (SyncRoot)
		{
			return _config.DisabledPlayerRuneIds.ToHashSet(StringComparer.Ordinal);
		}
	}

	public static IReadOnlySet<string> GetDisabledMonsterHexIds()
	{
		EnsureLoaded();
		lock (SyncRoot)
		{
			return NormalizeDisabledMonsterHexIds(_config.DisabledMonsterHexIds);
		}
	}

	public static IReadOnlySet<string> GetDisabledForgeIds()
	{
		EnsureLoaded();
		lock (SyncRoot)
		{
			return NormalizeDisabledForgeIds(_config.DisabledForgeIds);
		}
	}

	public static HextechRunConfigurationSnapshot GetSnapshot()
	{
		EnsureLoaded();
		lock (SyncRoot)
		{
			return NormalizeSnapshot(new HextechRunConfigurationSnapshot(
				_config.PlayerHexCountsByAct ?? DefaultPlayerHexCountsByAct,
				_config.EnemyHexCountsByAct ?? DefaultEnemyHexCountsByAct,
				_config.PlayerRuneRerollLimit,
				_config.MonsterHexRerollLimit,
				_config.DisabledPlayerRuneIds,
				_config.DisabledMonsterHexIds,
				_config.DisabledForgeIds,
				ToRarityWeightsByAct(_config.RuneRarityWeightsByAct, DefaultRuneRarityWeightsByAct),
				_config.PreventConsecutiveSilverRunes,
				_config.GoldenRerollChancePercent,
				ToForgeRarityWeights(_config.ForgeRarityWeights, DefaultForgeRarityWeights),
				_config.RandomForgeShopPrice,
				_config.RandomForgeDirectGrant,
				_config.ModEnabled,
				_config.ChaosRuneChancePercent));
		}
	}

	internal static HashSet<string> NormalizeDisabledPlayerRuneIds(IEnumerable<string>? ids)
	{
		return NormalizeConfigDisabledIds(ids);
	}

	internal static HashSet<string> NormalizeDisabledMonsterHexIds(IEnumerable<string>? ids)
	{
		HashSet<string> validIds = HextechContentRegistry.MonsterHexMetadata.EnabledKindsByRarity
			.Values
			.SelectMany(static kinds => kinds)
			.Select(static kind => kind.ToString())
			.ToHashSet(StringComparer.Ordinal);
		return NormalizeStringIds(ids, validIds);
	}

	internal static HashSet<string> NormalizeDisabledForgeIds(IEnumerable<string>? ids)
	{
		return NormalizeConfigStringIds(ids);
	}

	public static IReadOnlySet<string> GetDefaultDisabledPlayerRuneIds()
	{
		return HextechCatalog.GetDefaultDisabledPlayerRuneIds()
			.Select(static id => id.Entry)
			.ToHashSet(StringComparer.Ordinal);
	}

	public static IReadOnlySet<string> GetDefaultDisabledMonsterHexIds()
	{
		return new HashSet<string>(StringComparer.Ordinal)
		{
			MonsterHexKind.GetExcited.ToString(),
			MonsterHexKind.ShoulderVaku.ToString()
		};
	}

	public static IReadOnlySet<string> GetDefaultDisabledForgeIds()
	{
		return new HashSet<string>(StringComparer.Ordinal);
	}

	public static void SaveDisabledPlayerRuneIds(IEnumerable<string> disabledIds)
	{
		EnsureLoaded();
		lock (SyncRoot)
		{
			_config.ConfigVersion = CurrentConfigVersion;
			_config.DisabledPlayerRuneIds = NormalizeConfigDisabledIds(disabledIds);
			SaveConfig(_config);
		}
	}

	public static void SaveEnemyHexCountsByAct(IReadOnlyList<int> counts)
	{
		EnsureLoaded();
		lock (SyncRoot)
		{
			_config.ConfigVersion = CurrentConfigVersion;
			_config.EnemyHexCountsByAct = NormalizeEnemyHexCounts(counts);
			SaveConfig(_config);
		}
	}

	public static void SaveSnapshot(HextechRunConfigurationSnapshot snapshot)
	{
		EnsureLoaded();
		lock (SyncRoot)
		{
			HextechRunConfigurationSnapshot normalized = NormalizeSnapshot(snapshot);
			_config.ConfigVersion = CurrentConfigVersion;
			_config.PlayerHexCountsByAct = normalized.PlayerHexCountsByAct;
			_config.EnemyHexCountsByAct = normalized.EnemyHexCountsByAct;
			_config.PlayerRuneRerollLimit = normalized.PlayerRuneRerollLimit;
			_config.MonsterHexRerollLimit = normalized.MonsterHexRerollLimit;
			_config.DisabledPlayerRuneIds = normalized.DisabledPlayerRuneIds;
			_config.DisabledMonsterHexIds = normalized.DisabledMonsterHexIds;
			_config.DisabledForgeIds = normalized.DisabledForgeIds;
			_config.RuneRarityWeightsByAct = FromRarityWeightsByAct(normalized.RuneRarityWeightsByAct);
			_config.RuneRarityWeights = null;
			_config.PreventConsecutiveSilverRunes = normalized.PreventConsecutiveSilverRunes;
			_config.GoldenRerollChancePercent = normalized.GoldenRerollChancePercent;
			_config.ChaosRuneChancePercent = normalized.ChaosRuneChancePercent;
			_config.FirstActRuneRarityWeights = null;
			_config.NormalRuneRarityWeights = null;
			_config.SecondActAfterSilverRuneRarityWeights = null;
			_config.ForgeRarityWeights = FromForgeRarityWeights(normalized.ForgeRarityWeights);
			_config.RandomForgeShopPrice = normalized.RandomForgeShopPrice;
			_config.RandomForgeDirectGrant = normalized.RandomForgeDirectGrant;
			_config.ModEnabled = normalized.ModEnabled;
			SaveConfig(_config);
		}
	}

	// 模组总开关的当前(实时)配置值。运行中应优先读「本局冻结快照」,仅在无 run 场景(菜单外/商店初始化兜底)用它。
	public static bool GetModEnabled()
	{
		EnsureLoaded();
		lock (SyncRoot)
		{
			return _config.ModEnabled;
		}
	}

	public static bool GetDefaultModEnabled()
	{
		return DefaultModEnabled;
	}

	private static HashSet<string> NormalizeConfigDisabledIds(IEnumerable<string>? ids)
	{
		return HextechPlayerRuneConfigIds.Normalize(ids);
	}

	private static HashSet<string> NormalizeStringIds(IEnumerable<string>? ids, IReadOnlySet<string> validIds)
	{
		return (ids ?? [])
			.Where(static id => !string.IsNullOrWhiteSpace(id))
			.Select(static id => id.Trim())
			.Distinct(StringComparer.Ordinal)
			.Where(validIds.Contains)
			.OrderBy(static id => id, StringComparer.Ordinal)
			.ToHashSet(StringComparer.Ordinal);
	}

	private static HashSet<string> NormalizeConfigStringIds(IEnumerable<string>? ids)
	{
		return (ids ?? [])
			.Where(static id => !string.IsNullOrWhiteSpace(id))
			.Select(static id => id.Trim())
			.Distinct(StringComparer.Ordinal)
			.OrderBy(static id => id, StringComparer.Ordinal)
			.ToHashSet(StringComparer.Ordinal);
	}
}
