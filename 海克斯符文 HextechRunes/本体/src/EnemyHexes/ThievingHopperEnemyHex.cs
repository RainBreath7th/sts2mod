using System.Globalization;
using Godot;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace HextechRunes;

internal sealed class ThievingHopperEnemyHex : HextechEnemyHexEffect
{
	internal const string EscapeMoveId = "HEXTECH_THIEVING_HOPPER_ESCAPE";
	internal override MonsterHexKind Kind => MonsterHexKind.ThievingHopper;

	internal static string TheftKey(uint combatId) => $"enemy-thieving-hopper-stolen:{combatId}";

	internal static bool CanPlanTheft(Creature enemy, HextechEnemyHexContext context)
	{
		return enemy.Side == CombatSide.Enemy && !enemy.IsDead && !enemy.IsStunned
			&& enemy.CombatId is uint id && enemy.CombatState is { } combat && combat.RunState == context.RunState
			&& !IsProtectedBoss(combat.Encounter?.RoomType, enemy.IsPrimaryEnemy)
			&& context.Tracking.GlobalProcsThisCombat.GetValueOrDefault(TheftKey(id)) == 0
			&& enemy.Monster is { } monster && monster.NextMove.Id != EscapeMoveId
			&& !monster.NextMove.Intents.Any(i => i.IntentType is IntentType.Escape or IntentType.Stun);
	}

	internal static bool IsProtectedBoss(RoomType? roomType, bool isPrimaryEnemy)
		=> roomType == RoomType.Boss && isPrimaryEnemy;

	internal static bool RollTheft(HextechEnemyHexContext context, Creature enemy)
	{
		return HextechStableRandom.PercentChance(context.RunState, 20, "enemy-thieving-hopper",
			enemy.CombatId!.Value.ToString(CultureInfo.InvariantCulture),
			Math.Max(1, enemy.CombatState!.RoundNumber).ToString(CultureInfo.InvariantCulture));
	}

	internal static int StealPriority(CardModel card)
	{
		// 与原版 ThievingHopper 相同，优先顺走罕见牌，远古牌与灌注牌最后选择。
		if (card.Enchantment is Imbued || card.Rarity == CardRarity.Ancient) return 3;
		return card.Rarity switch
		{
			CardRarity.Uncommon => 0,
			CardRarity.Common or CardRarity.Rare or CardRarity.Event => 1,
			CardRarity.Basic or CardRarity.Quest => 2,
			_ => 4
		};
	}

	internal static async Task StealAndPlanEscape(HextechEnemyHexContext context, Creature enemy)
	{
		if (!CanPlanTheft(enemy, context)) return;
		// 敌人行动时已弃牌；与原版一样只从抽/弃牌堆选择仍在牌组中的原件，
		// 防止不同怪物通过战斗复制牌重复偷走同一张牌组原件。联机每个敌人也只偷一张。
		var targets = enemy.CombatState!.Players.Where(p => !p.Creature.IsDead)
			.OrderBy(p => p.NetId)
			.Select(p => CardPile.GetCards(p, PileType.Draw, PileType.Discard)
				.Where(c => c.DeckVersion != null && p.Deck.Cards.Contains(c.DeckVersion)).ToArray())
			.Where(cards => cards.Length > 0).ToArray();
		if (targets.Length == 0) return;
		string id = enemy.CombatId!.Value.ToString(CultureInfo.InvariantCulture);
		CardModel[] cards = targets[HextechStableRandom.Index(context.RunState, targets.Length, "hopper-player", id)];
		int priority = cards.Min(StealPriority);
		CardModel[] pool = cards.Where(c => StealPriority(c) == priority).ToArray();
		CardModel card = pool[HextechStableRandom.Index(context.RunState, pool.Length, "hopper-card", id)];
		context.Tracking.GlobalProcsThisCombat[TheftKey(enemy.CombatId.Value)] = 1;
		await CardPileCmd.RemoveFromCombat(card);
		SwipePower swipe = (SwipePower)ModelDb.Power<SwipePower>().ToMutable();
		await swipe.Steal(card);
		await PowerCmd.Apply(swipe, enemy, 1m, enemy, null);

		if (enemy.IsDead || enemy.CombatState == null) return;
		MoveState escape = CreateEscapeMove(() => Escape(enemy));
		MonsterModel monster = enemy.Monster!;
		monster.MoveStateMachine!.States[EscapeMoveId] = escape;
		monster.SetMoveImmediate(escape, forceTransition: true);
	}

	internal static MoveState CreateEscapeMove(Func<Task> escapeAction)
	{
		MoveState escape = new(EscapeMoveId, _ => escapeAction(), new EscapeIntent())
		{
			// 偷牌动作结束后原版还会 RollMove；必须保留逃跑动作直到它真正执行。
			MustPerformOnceBeforeTransitioning = true
		};
		escape.FollowUpState = escape;
		return escape;
	}

	private static async Task Escape(Creature enemy)
	{
		if (enemy.IsDead || enemy.CombatState == null) return;
		try
		{
			var node = NCombatRoom.Instance?.GetCreatureNode(enemy);
			if (node != null && GodotObject.IsInstanceValid(node))
			{
				node.ToggleIsInteractable(false);
				// 保持模型自己的待机姿态，仅移动表现节点；不要求各怪物提供 Flee 动画。
				node.SetAnimationTrigger("Idle");
				Tween tween = node.CreateTween();
				float distance = Math.Max(2400f, node.GetViewportRect().Size.X * 2f);
				tween.TweenProperty(node.Visuals, "position:x", node.Visuals.Position.X + distance, 0.85);
				await Cmd.Wait(0.9f);
			}
		}
		catch (Exception ex)
		{
			Log.Warn($"[{ModInfo.Id}] 偷窃草蜢逃跑表现失败，继续原版逃跑结算：{ex.Message}");
		}
		// 不走死亡命令：逃跑不应触发顺走的击杀返还。
		await CreatureCmd.Escape(enemy);
	}
}

internal sealed class ThievingHopperTheftIntent(Creature source) : CardDebuffIntent
{
	internal Creature Source => source;
}
