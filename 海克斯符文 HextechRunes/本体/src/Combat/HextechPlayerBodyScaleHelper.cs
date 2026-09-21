using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HextechRunes;

internal static class HextechPlayerBodyScaleHelper
{
	private const float MinScale = 0.2f;

	internal static void Update(Player? player)
	{
		if (player == null)
		{
			return;
		}

		float scale = 1f;
		scale += player.GetRelic<GoliathRune>()?.BodyScaleDelta ?? 0f;
		scale += player.GetRelic<GiantSlayerRune>()?.BodyScaleDelta ?? 0f;
		scale += player.GetRelic<TankEngineRune>()?.BodyScaleDelta ?? 0f;
		scale += player.GetRelic<ShrinkEngineRune>()?.BodyScaleDelta ?? 0f;
		scale += player.GetRelic<NineDragonPowerRune>()?.BodyScaleDelta ?? 0f;

		try
		{
			NCombatRoom.Instance?.GetCreatureNode(player.Creature)?.SetDefaultScaleTo(Math.Max(MinScale, scale), 0f);
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][BodyScale] Creature visual failed: {ex.Message}");
		}
	}
}
