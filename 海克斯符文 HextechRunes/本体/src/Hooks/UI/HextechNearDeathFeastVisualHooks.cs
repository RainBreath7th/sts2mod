using Godot;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace HextechRunes;

internal sealed class HextechNearDeathFeastVisual
{
	private const string NodeName = "HextechRunes_NearDeathFeastAura";
	private const float BodyHeightFactor = 0.64f;
	private const float GlowWidthFactor = 2.45f;
	private const float SurgeIntensityStep = 0.045f;

	private static readonly HashSet<ulong> ActiveCreatureNodes = [];

	private readonly NCreature _creature;
	private Node2D? _root;
	private Node2D? _renderLayer;
	private Sprite2D? _glow;
	private Sprite2D? _rays;
	private float _time;
	private float _surge;
	private bool _wasActive;
	private float _lastIntensity;

	private HextechNearDeathFeastVisual(NCreature creature)
	{
		_creature = creature;
	}

	internal static void TryAttach(NCreature? creature)
	{
		try
		{
			// 我方(带符文的玩家)与敌方(敌方海克斯激活时的所有敌人)都挂:轮询各自的濒死强度。
			if (!GodotObject.IsInstanceValid(creature)
				|| !creature.IsNodeReady()
				|| creature.Hitbox == null
				|| creature.Entity == null)
			{
				return;
			}

			ulong creatureInstanceId = creature.GetInstanceId();
			if (!ActiveCreatureNodes.Add(creatureInstanceId))
			{
				return;
			}

			HextechNearDeathFeastVisual visual = new(creature);
			if (!visual.Start())
			{
				ActiveCreatureNodes.Remove(creatureInstanceId);
				return;
			}

			TaskHelper.RunSafely(visual.RunAsync(creatureInstanceId));
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Could not attach Near-Death Feast visual: {ex.Message}");
		}
	}

	private static bool TryGetIntensity(NCreature creature, out float intensity)
	{
		intensity = 0f;
		return creature.Entity != null
			&& creature.Entity.IsAlive
			&& (NearDeathFeastRune.TryGetFeastIntensity(creature.Entity, out intensity)
				|| HextechEnemyNearDeath.TryGetFeastIntensity(creature.Entity, out intensity));
	}

	private bool Start()
	{
		Texture2D? glowTexture = HextechTextures.LoadPortableTexture(HextechAssets.NearDeathFeastGlowPath);
		if (glowTexture == null)
		{
			return false;
		}

		Node2D? renderLayer = HextechBehindCreaturesLayer.GetOrCreate(_creature.GetParent());
		if (renderLayer == null)
		{
			return false;
		}

		_renderLayer = renderLayer;
		_root = new Node2D
		{
			Name = NodeName,
			Visible = false,
			ZAsRelative = true,
			ZIndex = 0
		};
		renderLayer.AddChildSafely(_root);
		EnsureRenderOrder();

		// 原版 glowie / glowie2 共用这张红光纹理，分别为普通混合和加色；不再叠加程序生成的圆环。
		_glow = CreateSprite(_root, "FeastGlow", glowTexture, additive: false);
		_rays = CreateSprite(_root, "FeastRays", glowTexture, additive: true);
		UpdateTransform(1f);
		return true;
	}

	private async Task RunAsync(ulong creatureInstanceId)
	{
		try
		{
			while (GodotObject.IsInstanceValid(_creature) && GodotObject.IsInstanceValid(_root))
			{
				bool active = TryGetIntensity(_creature, out float intensity);
				_root!.Visible = active;

				if (active && !_wasActive)
				{
					_surge = 1f;
					_lastIntensity = intensity;
				}
				else if (active && intensity > _lastIntensity + SurgeIntensityStep)
				{
					_surge = 0.55f + intensity * 0.3f;
					_lastIntensity = intensity;
				}
				else if (!active)
				{
					_lastIntensity = 0f;
					_surge = 0f;
				}

				_wasActive = active;

				if (active)
				{
					EnsureRenderOrder();
					float dt = Mathf.Clamp((float)_root.GetProcessDeltaTime(), 1f / 120f, 0.05f);
					_time = Mathf.PosMod(_time + dt, 3600f);
					_surge = Mathf.MoveToward(_surge, 0f, dt * 2.8f);
					Animate(intensity);
				}

				SceneTree tree = _root.GetTree();
				if (!GodotObject.IsInstanceValid(tree))
				{
					return;
				}

				await _root.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}][Mayhem] Near-Death Feast visual stopped after runtime error: {ex.Message}");
		}
		finally
		{
			if (GodotObject.IsInstanceValid(_root))
			{
				_root.QueueFree();
			}

			ActiveCreatureNodes.Remove(creatureInstanceId);
		}
	}

	private void Animate(float intensity)
	{
		// 心跳:越濒死跳得越快、越重。两段"咚-咚"(lub-dub)而非匀速正弦,更有生命体征感。
		float rate = Mathf.Lerp(1.05f, 1.95f, intensity);
		float beat = Heartbeat(_time * rate);

		float glowScale = 1f + beat * Mathf.Lerp(0.05f, 0.12f, intensity) + _surge * 0.12f;
		UpdateTransform(glowScale);

		if (_glow != null)
		{
			float glowAlpha = Mathf.Lerp(0.35f, 0.65f, intensity) + beat * 0.15f + _surge * 0.20f;
			_glow.Modulate = Colors.White with { A = glowAlpha };
		}

		if (_rays != null)
		{
			float rayAlpha = Mathf.Lerp(0.08f, 0.20f, intensity) + beat * 0.12f + _surge * 0.25f;
			_rays.Modulate = Colors.White with { A = rayAlpha };
			_rays.Rotation = _time * 0.12f;
		}
	}

	private void UpdateTransform(float glowScalePulse)
	{
		if (_root == null || !GodotObject.IsInstanceValid(_creature) || _creature.Hitbox == null)
		{
			return;
		}

		Vector2 top = _creature.GetTopOfHitbox();
		Vector2 bottom = _creature.GetBottomOfHitbox();
		_root.GlobalPosition = bottom.Lerp(top, BodyHeightFactor);

		float width = Mathf.Clamp(_creature.Hitbox.Size.X, 120f, 360f);
		ScaleSprite(_glow, width * GlowWidthFactor * glowScalePulse);
		ScaleSprite(_rays, width * GlowWidthFactor * glowScalePulse);
	}

	private void EnsureRenderOrder()
	{
		HextechBehindCreaturesLayer.EnsureRenderOrder(_renderLayer);
	}

	private static Sprite2D CreateSprite(Node2D parent, string name, Texture2D texture, bool additive)
	{
		Sprite2D sprite = new()
		{
			Name = name,
			Texture = texture,
			Centered = true,
			Modulate = Colors.White with { A = 0f }
		};
		if (additive)
		{
			sprite.Material = new CanvasItemMaterial
			{
				BlendMode = CanvasItemMaterial.BlendModeEnum.Add
			};
		}

		parent.AddChildSafely(sprite);
		return sprite;
	}

	private static void ScaleSprite(Sprite2D? sprite, float diameter)
	{
		if (sprite?.Texture is { } texture)
		{
			sprite.Scale = Vector2.One * (diameter / Math.Max(texture.GetWidth(), 1));
		}
	}

	private static float Heartbeat(float t)
	{
		float phase = Mathf.PosMod(t, 1f);
		float lub = Bump(phase, 0f, 0.055f);
		float dub = Bump(phase, 0.17f, 0.065f) * 0.78f;
		return Mathf.Clamp(lub + dub, 0f, 1f);
	}

	private static float Bump(float x, float center, float sigma)
	{
		float d = x - center;
		if (d > 0.5f)
		{
			d -= 1f;
		}
		else if (d < -0.5f)
		{
			d += 1f;
		}

		return Mathf.Exp(-(d * d) / (2f * sigma * sigma));
	}
}
