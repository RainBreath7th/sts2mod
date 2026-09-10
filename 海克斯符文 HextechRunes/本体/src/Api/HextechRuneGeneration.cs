namespace HextechRunes;

/// <summary>候选生成完成后的扩展点。不得消耗共享 RNG 或修改已有候选实例。</summary>
public static class HextechRuneGeneration
{
	public delegate List<RelicModel> CandidateTransform(Player player, HextechRarityTier rarity,
		RunState runState, int stage, int chancePercent, IReadOnlyList<RelicModel> options, int slot, int rerollOrdinal);

	private static CandidateTransform? _chaosTransform;
	public static bool ChaosAvailable => _chaosTransform != null;

	public static void RegisterChaosTransform(CandidateTransform transform)
	{
		ArgumentNullException.ThrowIfNull(transform);
		if (_chaosTransform != null && _chaosTransform != transform)
			throw new InvalidOperationException("A chaos rune generator is already registered.");
		_chaosTransform = transform;
	}

	internal static List<RelicModel> Transform(Player player, HextechRarityTier rarity,
		RunState runState, int stage, List<RelicModel> options, int slot = -1, int rerollOrdinal = 0)
	{
		if (_chaosTransform == null || options.Count == 0) return options;
		HextechMayhemModifier? modifier = runState.Modifiers.OfType<HextechMayhemModifier>().LastOrDefault();
		if (modifier == null || !modifier.IsModActiveForRun) return options;
		HextechRunConfigurationSnapshot config = modifier.GetEffectiveRunConfigurationSnapshot();
		if (config.ChaosRuneChancePercent <= 0) return options;
		return _chaosTransform(player, rarity, runState, stage, config.ChaosRuneChancePercent, options, slot, rerollOrdinal);
	}
}
