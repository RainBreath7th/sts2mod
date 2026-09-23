using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HextechRunes;

/// <summary>
/// 敌方持久海克斯的 MaxHp 系数基准/投影维护。由 <see cref="HextechMayhemModifier"/> 的分部转发进来，
/// 状态一律经 <see cref="HextechMayhemModifier.CombatTracking"/> 读写，本类自身不持有可变静态状态。
/// </summary>
internal static class HextechMonsterMaxHpCoefficients
{
	internal static async Task ApplyPersistentMonsterHexes(
		HextechMayhemModifier modifier,
		Creature creature,
		bool replayOneShotPowers = false)
	{
		int? maxHpBaseOverride = replayOneShotPowers ? creature.MaxHp : null;
		_ = CaptureMonsterMaxHpCoefficientBase(
			modifier,
			creature,
			maxHpBaseOverride,
			out bool migratedLegacyCoefficients);
		if (migratedLegacyCoefficients)
		{
			// 旧存档的 persistent markers 已经置位，后续各 effect 会跳过 Apply。
			// 基准迁移后必须在这里统一投影一次，否则旧实际 MaxHp 会与新基准永久脱节。
			await ReapplyMonsterMaxHpCoefficients(modifier, creature);
		}

		await HextechEnemyHexDispatcher.ForEachActiveOrdered(
			modifier,
			static effect => effect.PersistentOrder,
			(effect, context) => effect.ApplyPersistentToEnemy(context, creature, maxHpBaseOverride, replayOneShotPowers));
	}

	internal static int CaptureMonsterMaxHpCoefficientBase(HextechMayhemModifier modifier, Creature creature, int? baseMaxHpOverride = null)
	{
		return CaptureMonsterMaxHpCoefficientBase(
			modifier,
			creature,
			baseMaxHpOverride,
			out _);
	}

	private static int CaptureMonsterMaxHpCoefficientBase(
		HextechMayhemModifier modifier,
		Creature creature,
		int? baseMaxHpOverride,
		out bool migratedLegacyCoefficients)
	{
		migratedLegacyCoefficients = false;
		if (creature.CombatId is not uint combatId)
		{
			return Math.Max(1, baseMaxHpOverride ?? creature.MaxHp);
		}

		if (baseMaxHpOverride is int overriddenBase)
		{
			int normalizedOverride = Math.Max(1, overriddenBase);
			modifier.CombatTracking.MonsterMaxHpCoefficientBase[combatId] = normalizedOverride;
			modifier.CombatTracking.MonsterMaxHpCoefficientProjected.Remove(combatId);
			return normalizedOverride;
		}

		if (modifier.CombatTracking.MonsterMaxHpCoefficientBase.TryGetValue(combatId, out int trackedBase)
			&& trackedBase > 0)
		{
			migratedLegacyCoefficients =
				modifier.CombatTracking.MonsterMaxHpCoefficientProjected.GetValueOrDefault(combatId, 0) <= 0
				&& HasAppliedMonsterMaxHpCoefficientMarker(modifier, combatId);
			return trackedBase;
		}

		bool coefficientsWereAlreadyApplied = HasAppliedMonsterMaxHpCoefficientMarker(modifier, combatId);
		int baseMaxHp = coefficientsWereAlreadyApplied
			? ResolveLegacyMonsterMaxHpCoefficientBase(modifier, creature, combatId)
			: Math.Max(1, creature.MaxHp);
		migratedLegacyCoefficients = coefficientsWereAlreadyApplied;

		modifier.CombatTracking.MonsterMaxHpCoefficientBase[combatId] = baseMaxHp;
		return baseMaxHp;
	}

	private static bool HasAppliedMonsterMaxHpCoefficientMarker(HextechMayhemModifier modifier, uint combatId)
	{
		return
			modifier.CombatTracking.GoliathApplied.Contains(combatId)
			|| modifier.CombatTracking.AstralBodyApplied.Contains(combatId)
			|| modifier.CombatTracking.GoldenSpatulaApplied.Contains(combatId)
			|| modifier.CombatTracking.StatsApplied.Contains(combatId)
			|| modifier.CombatTracking.StatsOnStatsApplied.Contains(combatId)
			|| modifier.CombatTracking.StatsOnStatsOnStatsApplied.Contains(combatId)
			|| modifier.CombatTracking.MadScientistApplied.Contains(combatId)
			|| modifier.CombatTracking.TankEngineStacks.GetValueOrDefault(combatId, 0) > 0;
	}

	private static int ResolveLegacyMonsterMaxHpCoefficientBase(HextechMayhemModifier modifier, Creature creature, uint combatId)
	{
		HextechEnemyHexContext context = new(modifier);
		List<decimal> appliedFixedBonusFractions = new(3);
		if (modifier.CombatTracking.GoliathApplied.Contains(combatId))
		{
			appliedFixedBonusFractions.Add(context.TierValue(MonsterHexKind.Goliath, 0.20m, 0.30m, 0.40m));
		}

		if (modifier.CombatTracking.AstralBodyApplied.Contains(combatId))
		{
			appliedFixedBonusFractions.Add(context.TierValue(MonsterHexKind.AstralBody, 0.20m, 0.30m, 0.40m));
		}

		if (modifier.CombatTracking.GoldenSpatulaApplied.Contains(combatId))
		{
			appliedFixedBonusFractions.Add(context.TierValue(MonsterHexKind.GoldenSpatula, 0.25m, 0.30m, 0.45m));
		}

		if (modifier.CombatTracking.StatsApplied.Contains(combatId))
		{
			appliedFixedBonusFractions.Add(EnemyAttributeBoostValues.GetBonusFraction(MonsterHexKind.Stats, context.GetStrengthTier(MonsterHexKind.Stats)));
		}

		if (modifier.CombatTracking.StatsOnStatsApplied.Contains(combatId))
		{
			appliedFixedBonusFractions.Add(EnemyAttributeBoostValues.GetBonusFraction(MonsterHexKind.StatsOnStats, context.GetStrengthTier(MonsterHexKind.StatsOnStats)));
		}

		if (modifier.CombatTracking.StatsOnStatsOnStatsApplied.Contains(combatId))
		{
			appliedFixedBonusFractions.Add(EnemyAttributeBoostValues.GetBonusFraction(MonsterHexKind.StatsOnStatsOnStats, context.GetStrengthTier(MonsterHexKind.StatsOnStatsOnStats)));
		}

		decimal madScientistLossFraction = modifier.CombatTracking.MadScientistApplied.Contains(combatId)
			? context.TierValue(MonsterHexKind.MadScientist, 0.30m, 0.15m, 0.00m)
			: 0m;
		int tankEngineStacks = Math.Max(0, modifier.CombatTracking.TankEngineStacks.GetValueOrDefault(combatId, 0));
		int? rawMonsterMaxHp = creature.MonsterMaxHpBeforeModification is int rawMaxHp && rawMaxHp > 0
			? rawMaxHp
			: null;
		int migratedBaseMaxHp = HextechLegacyEnemyMaxHpMigration.ResolveBaseMaxHp(
			creature.MaxHp,
			rawMonsterMaxHp,
			appliedFixedBonusFractions,
			madScientistLossFraction,
			tankEngineStacks);
		HextechLog.Info(
			$"[{ModInfo.Id}][Mayhem] Migrated legacy enemy max HP base: combatId={combatId} current={creature.MaxHp} raw={rawMonsterMaxHp?.ToString() ?? "unknown"} fixedBonuses={string.Join(",", appliedFixedBonusFractions)} madLoss={madScientistLossFraction} tankStacks={tankEngineStacks} base={migratedBaseMaxHp}");
		return migratedBaseMaxHp;
	}

	internal static async Task ReapplyMonsterMaxHpCoefficients(HextechMayhemModifier modifier, Creature creature, int? baseMaxHpOverride = null)
	{
		int baseMaxHp = CaptureMonsterMaxHpCoefficientBase(modifier, creature, baseMaxHpOverride);
		if (baseMaxHpOverride == null)
		{
			baseMaxHp = ReconcileObservedMonsterMaxHpChange(modifier, creature, baseMaxHp);
		}

		decimal scale = GetMonsterMaxHpCoefficientScale(modifier, creature);
		int expectedMaxHp = (int)Math.Clamp(Math.Floor(baseMaxHp * scale), 1m, int.MaxValue);
		int delta = expectedMaxHp - creature.MaxHp;
		if (delta > 0)
		{
			await GainMonsterMaxHpWithoutHeal(creature, delta);
			TrackProjectedMonsterMaxHp(modifier, creature);
			return;
		}

		if (delta < 0)
		{
			await CreatureCmdCompat.SetMaxHp(creature, expectedMaxHp);
		}

		TrackProjectedMonsterMaxHp(modifier, creature);
		await KeepFurCoatMarkedEnemyAtOneHp(creature);
	}

	private static int ReconcileObservedMonsterMaxHpChange(HextechMayhemModifier modifier, Creature creature, int baseMaxHp)
	{
		if (creature.CombatId is not uint combatId
			|| !modifier.CombatTracking.MonsterMaxHpCoefficientProjected.TryGetValue(
				combatId,
				out int projectedMaxHp)
			|| projectedMaxHp <= 0
			|| projectedMaxHp == creature.MaxHp)
		{
			return baseMaxHp;
		}

		long observedDelta = (long)creature.MaxHp - projectedMaxHp;
		int adjustedBaseMaxHp = (int)Math.Clamp(
			(long)baseMaxHp + observedDelta,
			1L,
			int.MaxValue);
		modifier.CombatTracking.MonsterMaxHpCoefficientBase[combatId] = adjustedBaseMaxHp;
		HextechLog.Info(
			$"[{ModInfo.Id}][Mayhem] Reconciled enemy max HP base after an external change: "
			+ $"combatId={combatId} base={baseMaxHp} projected={projectedMaxHp} "
			+ $"observed={creature.MaxHp} adjustedBase={adjustedBaseMaxHp}");
		return adjustedBaseMaxHp;
	}

	private static void TrackProjectedMonsterMaxHp(HextechMayhemModifier modifier, Creature creature)
	{
		if (creature.CombatId is uint combatId)
		{
			modifier.CombatTracking.MonsterMaxHpCoefficientProjected[combatId] =
				Math.Max(1, creature.MaxHp);
		}
	}

	private static decimal GetMonsterMaxHpCoefficientScale(HextechMayhemModifier modifier, Creature creature)
	{
		HextechEnemyHexContext context = new(modifier);
		return HextechEnemyCoefficientHelper.CombineBonusFractionsByHex(
			HextechEnemyHexEffects.GetActive(modifier)
				.OfType<IHextechEnemyMaxHpCoefficientProvider>()
				.Select(provider =>
				(((HextechEnemyHexEffect)provider).Kind, provider.GetMaxHpBonusFraction(context, creature))));
	}

	internal static async Task GainMonsterMaxHpWithoutHeal(Creature creature, int amount)
	{
		if (amount <= 0)
		{
			return;
		}

		int oldMaxHp = creature.MaxHp;
		int oldCurrentHp = creature.CurrentHp;
		await CreatureCmdCompat.SetMaxHp(creature, oldMaxHp + amount);

		int actualMaxHpGain = Math.Max(0, creature.MaxHp - oldMaxHp);
		if (actualMaxHpGain <= 0)
		{
			return;
		}

		int newCurrentHp = IsFurCoatMarkedEnemy(creature)
			? 1
			: Math.Min(creature.MaxHp, oldCurrentHp + actualMaxHpGain);
		if (newCurrentHp != creature.CurrentHp)
		{
			await CreatureCmd.SetCurrentHp(creature, newCurrentHp);
		}
	}

	private static Task KeepFurCoatMarkedEnemyAtOneHp(Creature creature)
	{
		if (IsFurCoatMarkedEnemy(creature) && creature.CurrentHp != 1)
		{
			return CreatureCmd.SetCurrentHp(creature, 1m);
		}

		return Task.CompletedTask;
	}

	private static bool IsFurCoatMarkedEnemy(Creature creature)
	{
		if (creature.Side != CombatSide.Enemy || !creature.IsAlive || creature.CombatState == null)
		{
			return false;
		}

		foreach (RelicModel relic in creature.CombatState.Players.SelectMany(static player => player.Relics))
		{
			if (relic is not FurCoat furCoat || furCoat.Owner?.RunState.CurrentMapPoint == null)
			{
				continue;
			}

			if (furCoat.GetMarkedCoords()?.Contains(furCoat.Owner.RunState.CurrentMapPoint.coord) == true)
			{
				return true;
			}
		}

		return false;
	}

	internal static void UpdateEnemyScale(HextechMayhemModifier modifier, Creature creature)
	{
		float baseScale = modifier.HasActiveMonsterHex(MonsterHexKind.Goliath) ? 1.35f : 1f;
		// 巨人杀手敌方版让敌人体型缩小(纯视觉,呼应「体型变小」的设定,无机制意义)。
		float giantSlayerShrink = modifier.HasActiveMonsterHex(MonsterHexKind.GiantSlayer) ? 0.25f : 0f;
		int tankStacks = creature.CombatId == null ? 0 : modifier.CombatTracking.TankEngineStacks.GetValueOrDefault(creature.CombatId.Value, 0);
		int shrinkStacks = creature.CombatId == null ? 0 : modifier.CombatTracking.ShrinkEngineStacks.GetValueOrDefault(creature.CombatId.Value, 0);
		float finalScale = Math.Max(0.2f, baseScale + tankStacks * 0.05f - shrinkStacks * 0.02f - giantSlayerShrink);
		try
		{
			NCombatRoom.Instance?.GetCreatureNode(creature)?.SetDefaultScaleTo(finalScale, 0f);
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Enemy scale visual failed: {ex.Message}");
		}
	}
}
