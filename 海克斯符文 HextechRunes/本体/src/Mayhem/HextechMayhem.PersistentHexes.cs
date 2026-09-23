namespace HextechRunes;

internal sealed partial class HextechMayhemModifier
{
	private Task ApplyPersistentMonsterHexes(Creature creature, bool replayOneShotPowers = false)
	{
		return HextechMonsterMaxHpCoefficients.ApplyPersistentMonsterHexes(this, creature, replayOneShotPowers);
	}

	internal int CaptureMonsterMaxHpCoefficientBase(Creature creature, int? baseMaxHpOverride = null)
	{
		return HextechMonsterMaxHpCoefficients.CaptureMonsterMaxHpCoefficientBase(this, creature, baseMaxHpOverride);
	}

	internal Task ReapplyMonsterMaxHpCoefficients(Creature creature, int? baseMaxHpOverride = null)
	{
		return HextechMonsterMaxHpCoefficients.ReapplyMonsterMaxHpCoefficients(this, creature, baseMaxHpOverride);
	}

	internal void UpdateEnemyScale(Creature creature)
	{
		HextechMonsterMaxHpCoefficients.UpdateEnemyScale(this, creature);
	}
}
