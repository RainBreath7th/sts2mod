using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Models.RelicPools;

namespace HextechRunes;

internal static class HextechAncientRelicHelper
{
	private static readonly Type[] NonupeipeRelicTypes =
	[
		typeof(BlessedAntler),
		typeof(BrilliantScarf),
		typeof(DelicateFrond),
		typeof(DiamondDiadem),
		typeof(FurCoat),
		typeof(Glitter),
		typeof(JewelryBox),
		typeof(LoomingFruit),
		typeof(SignetRing),
		typeof(BeautifulBracelet)
	];

	private static readonly Type[] VakuuRelicTypes =
	[
		typeof(BloodSoakedRose),
		typeof(ChoicesParadox),
		typeof(DistinguishedCape),
		typeof(Fiddle),
		typeof(JeweledMask),
		typeof(LordsParasol),
		typeof(MusicBox),
		typeof(PreservedFog),
		typeof(SereTalon),
		typeof(WhisperingEarring)
	];

	public static RelicModel CreateRandomNonupeipeRelic(Player player, string source)
	{
		return CreateRandomAncientRelic(NonupeipeRelicTypes, player, source);
	}

	// 逐件获得时依次调用：上一件入手后遗物数与已持有集合都变了，下一件自然不会重复。
	public static RelicModel CreateRandomVakuuRelic(Player player, string source)
	{
		return CreateRandomAncientRelic(VakuuRelicTypes, player, source);
	}

	private static RelicModel CreateRandomAncientRelic(Type[] pool, Player player, string source)
	{
		Type[] candidates = pool
			.Where(type => !HasRelic(player, type))
			.ToArray();
		if (candidates.Length == 0)
		{
			candidates = pool;
		}

		Type relicType = HextechStableRandom.Pick(
			candidates,
			(RunState)player.RunState,
			HextechStableRandom.TypeModelKey,
			source,
			HextechStableRandom.PlayerKey(player),
			player.Relics.Count.ToString());
		return ModelDb.GetById<RelicModel>(ModelDb.GetId(relicType)).ToMutable();
	}

	public static RelicModel CreateWaxRelicFromRewardPool(Player player)
	{
		RelicModel relic = RelicFactory.PullNextRelicFromFront(player);
		relic.IsWax = true;
		return relic;
	}

	public static RelicModel CreateRepeatableWaxRelic(Player player, string source, int ordinal)
	{
		RunState runState = (RunState)player.RunState;
		RelicRarity rarity = RelicFactory.RollRarity(player);
		List<RelicModel> candidates = BuildRepeatableRewardRelicPool(player, rarity);
		if (candidates.Count == 0)
		{
			candidates = BuildRepeatableRewardRelicPool(player, null);
		}

		RelicModel relic = HextechStableRandom.Pick(
			candidates,
			runState,
			static relic => relic.Id.Entry,
			source,
			HextechStableRandom.PlayerKey(player),
			runState.TotalFloor.ToString(),
			ordinal.ToString(),
			rarity.ToString()).ToMutable();
		relic.IsWax = true;
		return relic;
	}

	private static List<RelicModel> BuildRepeatableRewardRelicPool(Player player, RelicRarity? rarity)
	{
		IEnumerable<RelicModel> rewardRelics = ModelDb.RelicPool<SharedRelicPool>()
			.GetUnlockedRelics(player.UnlockState)
			.Concat(player.Character.RelicPool.GetUnlockedRelics(player.UnlockState));

		return rewardRelics
			.Where(relic => IsRepeatableRewardRelicCandidate(relic, rarity))
			.GroupBy(static relic => relic.Id)
			.Select(static group => group.First())
			.OrderBy(static relic => relic.Id.Entry, StringComparer.Ordinal)
			.ToList();
	}

	private static bool IsRepeatableRewardRelicCandidate(RelicModel relic, RelicRarity? rarity)
	{
		return (rarity == null || relic.Rarity == rarity)
			&& relic.Rarity is RelicRarity.Common or RelicRarity.Uncommon or RelicRarity.Rare;
	}

	private static bool HasRelic(Player player, Type relicType)
	{
		ModelId relicId = ModelDb.GetId(relicType);
		return player.Relics.Any(relic => (relic.CanonicalInstance?.Id ?? relic.Id) == relicId);
	}
}
