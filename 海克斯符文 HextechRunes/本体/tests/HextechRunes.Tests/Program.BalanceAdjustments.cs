using HextechRunes;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.ValueProps;

namespace HextechRunes.Tests;

internal static partial class Program
{
	private static void BloodPactRequiresHpLossFromEnemyAttack()
	{
		Expect(BloodPactRune.ShouldGainStrength(CombatSide.Enemy, 1, ValueProp.Move), "enemy attack HP loss grants Strength");
		Expect(!BloodPactRune.ShouldGainStrength(CombatSide.Enemy, 0, ValueProp.Move), "fully blocked attacks do not grant Strength");
		Expect(!BloodPactRune.ShouldGainStrength(CombatSide.Player, 3, ValueProp.Move), "self or allied damage does not grant Strength");
		Expect(!BloodPactRune.ShouldGainStrength(null, 3, ValueProp.Unpowered), "HP costs and sourceless damage do not grant Strength");
		Expect(!BloodPactRune.ShouldGainStrength(CombatSide.Enemy, 3, ValueProp.Unpowered), "enemy non-attack damage does not grant Strength");
	}

	private static void EnemyBalanceUsesNewTierPercentagesAndUncappedSustain()
	{
		Equal(20, ExoskeletonEnemyHex.ResolveHardToKill(100, 1), "act one Hard to Kill threshold");
		Equal(15, ExoskeletonEnemyHex.ResolveHardToKill(100, 2), "act two Hard to Kill threshold");
		Equal(10, ExoskeletonEnemyHex.ResolveHardToKill(100, 3), "act three Hard to Kill threshold");
		Equal(6, ExoskeletonEnemyHex.ResolveHardToKill(10, 3), "Hard to Kill minimum remains six");
		for (int tier = 1; tier <= 3; tier++)
		{
			Equal(tier + 2, FinalFormEnemyHex.ResolvePlating(100, tier), "Final Form grants 3/4/5 percent Plating");
			Equal(tier, CourageOfColossusEnemyHex.ResolvePlating(100, tier), "Courage grants 1/2/3 percent Plating");
		}
		Equal(25, SoulEaterEnemyHex.ResolveMaxHpGain(100), "Soul Eater gains one quarter of the dead enemy's Max HP");
		Equal(24, SoulEaterEnemyHex.ResolveMaxHpGain(99), "Soul Eater rounds the Max HP gain down");
		Equal(5m, HextechMonsterSustainHelper.ResolveProteinShakeSustainMultiplier(2000, 1), "Protein Shake exceeds the old 100-percent cap");
		Equal(3m, HextechMonsterSustainHelper.ResolveProteinShakeSustainMultiplier(2000, 2), "Protein Shake divides its threshold by player count consistently");
		Equal(1.01m, HextechMonsterSustainHelper.ResolveProteinShakeSustainMultiplier(20, 4), "four-player Protein Shake first threshold");
		Equal(30m, VantomEnemyHex.MaxHpPerStack, "Vantom requires thirty Max HP per Slippery");
	}

	private static void DragonSoulAndMikaelsUseUpdatedUpgradeValues()
	{
		MikaelsBlessingCard mikaels = CreateMutableTestModel<MikaelsBlessingCard>();
		Equal(0, mikaels.EnergyCost.GetWithModifiers(CostModifiers.Local), "base Mikael's Blessing costs zero");
		Equal(10m, mikaels.DynamicVars["HealPercent"].BaseValue, "base Mikael's Blessing heals ten percent");
		Expect(mikaels.CanonicalKeywords.Contains(CardKeyword.Retain), "Mikael's Blessing retains");
		CardCmd.Upgrade(mikaels);
		Equal(0, mikaels.EnergyCost.GetWithModifiers(CostModifiers.Local), "upgraded Mikael's Blessing still costs zero");
		Equal(15m, mikaels.DynamicVars["HealPercent"].BaseValue, "upgrade increases healing to fifteen percent");
		InfernalDragonSoulCard infernal = CreateMutableTestModel<InfernalDragonSoulCard>();
		Equal(0, infernal.EnergyCost.GetWithModifiers(CostModifiers.Local), "Infernal Dragon Soul costs zero");
		Equal(8m, infernal.DynamicVars["BurnPower"].BaseValue, "Infernal Dragon Soul applies eight Burn");
		CardCmd.Upgrade(infernal);
		Equal(8m, infernal.DynamicVars["BurnPower"].BaseValue, "upgraded Infernal Dragon Soul retains eight Burn");
		Expect(infernal.Keywords.Contains(CardKeyword.Innate), "upgraded Infernal Dragon Soul is Innate");
		Equal(2m, new AncientWineRune().DynamicVars["HealPercent"].BaseValue, "Ancient Wine heals two percent after a Skill");
	}
}
