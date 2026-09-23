using System.Text.Json;
using System.Text.Json.Serialization;

namespace HextechRunes;

internal static partial class HextechRuneConfiguration
{
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		WriteIndented = true,
		PropertyNameCaseInsensitive = true
	};

	private static void EnsureLoaded()
	{
		lock (SyncRoot)
		{
			if (_loaded)
			{
				return;
			}

			_config = LoadOrCreateConfig();
			_loaded = true;
		}
	}

	private static RuneConfig LoadOrCreateConfig()
	{
		string? configPath = null;
		try
		{
			configPath = GetConfigPath();
			if (!File.Exists(configPath))
			{
				RuneConfig defaultConfig = CreateDefaultConfig();
				SaveConfig(defaultConfig);
				return defaultConfig;
			}

			RuneConfig? parsed = JsonSerializer.Deserialize<RuneConfig>(File.ReadAllText(configPath), JsonOptions);
			bool fromNewerVersion = parsed != null && parsed.ConfigVersion > CurrentConfigVersion;
			RuneConfig config = NormalizeLoadedConfig(parsed ?? new RuneConfig());
			if (fromNewerVersion)
			{
				// 更新版本写的配置退回本版本读取:类型化模型不保留未知字段,回写会把版本号压回并丢掉未来字段。
				// 只在内存里使用规范化结果,不覆盖文件;用户在本版本改设置时才会重写。
				Log.Warn($"[{ModInfo.Id}][RuneConfig] Config version {parsed!.ConfigVersion} is newer than supported {CurrentConfigVersion}; using it in memory without rewriting the file.", 2);
				return config;
			}

			SaveConfig(config);
			return config;
		}
		catch (JsonException ex)
		{
			Log.Warn($"[{ModInfo.Id}][RuneConfig] Config JSON is invalid; using defaults: {ex.Message}", 2);
			RuneConfig config = CreateDefaultConfig();
			if (configPath != null && TryBackupCorruptConfig(configPath))
			{
				SaveConfig(config);
			}

			return config;
		}
		catch (UnauthorizedAccessException ex)
		{
			Log.Warn($"[{ModInfo.Id}][RuneConfig] Config read was denied; using in-memory defaults without overwriting the file: {ex.Message}", 2);
			return CreateDefaultConfig();
		}
		catch (IOException ex)
		{
			Log.Warn($"[{ModInfo.Id}][RuneConfig] Config read failed due to I/O; using in-memory defaults without overwriting the file: {ex.Message}", 2);
			return CreateDefaultConfig();
		}
		catch (Exception ex)
		{
			Log.Error($"[{ModInfo.Id}][RuneConfig] Unexpected config read failure; using in-memory defaults without overwriting the file: {ex}");
			return CreateDefaultConfig();
		}
	}

	private static bool TryBackupCorruptConfig(string configPath)
	{
		try
		{
			File.Copy(configPath, configPath + ".corrupt.bak", overwrite: true);
			return true;
		}
		catch (UnauthorizedAccessException ex)
		{
			Log.Warn($"[{ModInfo.Id}][RuneConfig] Could not back up corrupt config; original file will not be overwritten: {ex.Message}", 2);
			return false;
		}
		catch (IOException ex)
		{
			Log.Warn($"[{ModInfo.Id}][RuneConfig] Could not back up corrupt config; original file will not be overwritten: {ex.Message}", 2);
			return false;
		}
		catch (Exception ex)
		{
			Log.Error($"[{ModInfo.Id}][RuneConfig] Unexpected corrupt-config backup failure; original file will not be overwritten: {ex}");
			return false;
		}
	}

	private static RuneConfig CreateDefaultConfig()
	{
		return new RuneConfig
		{
			ConfigVersion = CurrentConfigVersion,
			DisabledPlayerRuneIds = GetDefaultDisabledPlayerRuneIds().ToHashSet(StringComparer.Ordinal),
			PlayerHexCountsByAct = NormalizePlayerHexCounts(null),
			EnemyHexCountsByAct = NormalizeEnemyHexCounts(null),
			PlayerRuneRerollLimit = DefaultPlayerRuneRerollLimit,
			MonsterHexRerollLimit = DefaultMonsterHexRerollLimit,
			DisabledMonsterHexIds = GetDefaultDisabledMonsterHexIds().ToHashSet(StringComparer.Ordinal),
			DisabledForgeIds = GetDefaultDisabledForgeIds().ToHashSet(StringComparer.Ordinal),
			RuneRarityWeightsByAct = FromRarityWeightsByAct(DefaultRuneRarityWeightsByAct),
			PreventConsecutiveSilverRunes = DefaultPreventConsecutiveSilverRunes,
			GoldenRerollChancePercent = DefaultGoldenRerollChancePercent,
			ForgeRarityWeights = FromForgeRarityWeights(DefaultForgeRarityWeights),
			RandomForgeShopPrice = DefaultRandomForgeShopPrice,
			RandomForgeDirectGrant = DefaultRandomForgeDirectGrant,
			ModEnabled = DefaultModEnabled
		};
	}

	private static HextechRarityWeights ToRarityWeights(RarityWeightConfig? config, HextechRarityWeights fallback)
	{
		return config == null
			? fallback
			: new HextechRarityWeights(config.Silver, config.Gold, config.Prismatic);
	}

	private static HextechRarityWeights[] ToRarityWeightsByAct(
		IReadOnlyList<RarityWeightConfig>? configs,
		IReadOnlyList<HextechRarityWeights> fallback)
	{
		return Enumerable.Range(0, HexActCount)
			.Select(actIndex => configs != null && actIndex < configs.Count
				? ToRarityWeights(configs[actIndex], fallback[Math.Min(actIndex, fallback.Count - 1)])
				: fallback[Math.Min(actIndex, fallback.Count - 1)])
			.ToArray();
	}

	private static HextechForgeRarityWeights ToForgeRarityWeights(RarityWeightConfig? config, HextechForgeRarityWeights fallback)
	{
		return config == null
			? fallback
			: new HextechForgeRarityWeights(config.Silver, config.Gold, config.Prismatic);
	}

	private static RarityWeightConfig FromRarityWeights(HextechRarityWeights weights)
	{
		return new RarityWeightConfig
		{
			Silver = weights.Silver,
			Gold = weights.Gold,
			Prismatic = weights.Prismatic
		};
	}

	private static RarityWeightConfig[] FromRarityWeightsByAct(IEnumerable<HextechRarityWeights> weightsByAct)
	{
		return weightsByAct.Select(FromRarityWeights).ToArray();
	}

	private static RarityWeightConfig FromForgeRarityWeights(HextechForgeRarityWeights weights)
	{
		return new RarityWeightConfig
		{
			Silver = weights.Silver,
			Gold = weights.Gold,
			Prismatic = weights.Prismatic
		};
	}

	private static void SaveConfig(RuneConfig config)
	{
		try
		{
			string configPath = GetConfigPath();
			Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
			string serialized = JsonSerializer.Serialize(config, JsonOptions);
			File.WriteAllText(configPath, serialized);
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][RuneConfig] Config write failed: {ex.Message}", 2);
		}
	}

	private static string GetConfigPath()
	{
		return HextechDataPaths.GetFilePath(ConfigFileName);
	}

	private sealed class RuneConfig
	{
		[JsonPropertyName("config_version")]
		public int ConfigVersion { get; set; }

		[JsonPropertyName("disabled_player_rune_ids")]
		public HashSet<string> DisabledPlayerRuneIds { get; set; } = new(StringComparer.Ordinal);

		[JsonPropertyName("player_hex_counts_by_act")]
		public int[]? PlayerHexCountsByAct { get; set; }

		[JsonPropertyName("enemy_hex_counts_by_act")]
		public int[]? EnemyHexCountsByAct { get; set; }

		[JsonPropertyName("player_rune_reroll_limit")]
		public int PlayerRuneRerollLimit { get; set; } = DefaultPlayerRuneRerollLimit;

		[JsonPropertyName("monster_hex_reroll_limit")]
		public int MonsterHexRerollLimit { get; set; } = DefaultMonsterHexRerollLimit;

		[JsonPropertyName("disabled_monster_hex_ids")]
		public HashSet<string> DisabledMonsterHexIds { get; set; } = new(StringComparer.Ordinal);

		[JsonPropertyName("disabled_forge_ids")]
		public HashSet<string> DisabledForgeIds { get; set; } = new(StringComparer.Ordinal);

		[JsonPropertyName("rune_rarity_weights")]
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public RarityWeightConfig? RuneRarityWeights { get; set; }

		[JsonPropertyName("rune_rarity_weights_by_act")]
		public RarityWeightConfig[]? RuneRarityWeightsByAct { get; set; }

		[JsonPropertyName("prevent_consecutive_silver_runes")]
		public bool PreventConsecutiveSilverRunes { get; set; } = DefaultPreventConsecutiveSilverRunes;

		[JsonPropertyName("golden_reroll_chance_percent")]
		public int GoldenRerollChancePercent { get; set; } = DefaultGoldenRerollChancePercent;

		public int ChaosRuneChancePercent { get; set; } = 33;

		[JsonPropertyName("first_act_rune_rarity_weights")]
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public RarityWeightConfig? FirstActRuneRarityWeights { get; set; }

		[JsonPropertyName("normal_rune_rarity_weights")]
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public RarityWeightConfig? NormalRuneRarityWeights { get; set; }

		[JsonPropertyName("second_act_after_silver_rune_rarity_weights")]
		[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
		public RarityWeightConfig? SecondActAfterSilverRuneRarityWeights { get; set; }

		[JsonPropertyName("forge_rarity_weights")]
		public RarityWeightConfig? ForgeRarityWeights { get; set; }

		[JsonPropertyName("random_forge_shop_price")]
		public int RandomForgeShopPrice { get; set; } = DefaultRandomForgeShopPrice;

		[JsonPropertyName("random_forge_direct_grant")]
		public bool RandomForgeDirectGrant { get; set; } = DefaultRandomForgeDirectGrant;

		[JsonPropertyName("mod_enabled")]
		public bool ModEnabled { get; set; } = DefaultModEnabled;
	}

	private sealed class RarityWeightConfig
	{
		[JsonPropertyName("silver")]
		public int Silver { get; set; }

		[JsonPropertyName("gold")]
		public int Gold { get; set; }

		[JsonPropertyName("prismatic")]
		public int Prismatic { get; set; }
	}
}
