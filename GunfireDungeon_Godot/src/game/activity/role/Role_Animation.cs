
using System;
using Godot;
using Vector2 = Godot.Vector2;

public partial class Role
{
	//近战总动画 0.05 秒（挥出 0.0125 + 停留 0.0125 + 收回 0.025）。
	//近战没有额外的固定冷却，只在挥刀动画期间阻止重入（见 Role.MeleeAttack），
	//所以动画时长直接决定近战攻速 —— 这里比原来快了一倍（原 0.1 秒）。
	private const float MeleeAttackWindupTime = 0.0125f;
	private const float MeleeAttackHoldTime = 0.0125f;
	private const float MeleeAttackReturnTime = 0.025f;

	/// <summary>
	/// 手持【非近战武器(枪类)】时, 挥击动画的时长倍率。1 = 不加速。
	///
	/// 【为什么要单独一个倍率】
	/// 刀的"自身攻击"走的是 Knife.OnFire() —— 那是完全独立的一条路
	/// (自己的 _hitArea、自己的特效、自己的蓄力角度), 根本不调用本函数。
	/// 本函数只服务【近战键(空格)的那次挥击】。
	/// 所以这里加速不会碰到刀的左键攻击动画; 但拿刀按空格时也一样要走这段,
	/// 用 IsMelee 区分开, 保证"刀"相关的动画一律保持原速。
	///
	/// 三段时长都会乘这个倍率:
	///   1.00 -> 挥 0.0125 / 停 0.0125 / 收 0.025  = 0.050s
	///   0.60 -> 挥 0.0075 / 停 0.0075 / 收 0.015  = 0.030s  (第一次加快)
	///   0.40 -> 挥 0.0050 / 停 0.0050 / 收 0.010  = 0.020s  (再加快, 当前)
	///
	/// ✅ 【下限已经不存在了】伤害判定已经从 MeleeAttackArea.AreaEntered
	///    改成"挥到位后每个物理帧主动查一次重叠区域"(见 Role.ProcessMeleeActiveQuery),
	///    不再依赖物理步恰好采到某个姿势。
	///    所以这个倍率可以继续往下调, 直到动画短到看不见为止 ——
	///    唯一还要留意的只是"别短到玩家看不出挥了刀"。
	/// </summary>
	private const float GunMeleeSpeedScale = 0.4f;

	//整段挥刀时长 —— 也就是近战判定框需要开着的时长(见 Role.EnableMeleeHitArea)
	private const float MeleeAttackTotalTime =
		MeleeAttackWindupTime + MeleeAttackHoldTime + MeleeAttackReturnTime;

	/// <summary>
	/// 播放近战攻击动画
	/// </summary>
	public virtual void PlayAnimation_MeleeAttack(Action finish)
	{
		//手持枪类武器(非近战武器)时挥击更快; 拿刀时保持原速。
		//判断依据是当前手持武器的 IsMelee 标记(刀 = true, 枪/弓 = false)。
		//空手(ActiveItem == null)也走快的这一档 —— 毕竟没有武器的重量。
		var activeItem = WeaponPack.ActiveItem;
		var isMeleeWeapon = activeItem?.Attribute?.IsMelee == true;
		var scale = isMeleeWeapon ? 1f : GunMeleeSpeedScale;

		var windupTime = MeleeAttackWindupTime * scale;
		var holdTime = MeleeAttackHoldTime * scale;
		var returnTime = MeleeAttackReturnTime * scale;
		var totalTime = windupTime + holdTime + returnTime;

		var r = MountPoint.RotationDegrees;
		//var gp = MountPoint.GlobalPosition;
		var p1 = MountPoint.Position;
		var p2 = p1 + new Vector2(6, 0).Rotated(Mathf.DegToRad(r - MeleeAttackAngle / 2f));
		var p3 = p1 + new Vector2(6, 0).Rotated(Mathf.DegToRad(r + MeleeAttackAngle / 2f));
		
		var tween = CreateTween();
		tween.SetParallel();
		
		tween.TweenProperty(MountPoint, "rotation_degrees", r - MeleeAttackAngle / 2f, windupTime);
		tween.TweenProperty(MountPoint, "position", p2, windupTime);
		tween.Chain();

		tween.TweenCallback(Callable.From(() =>
		{
			MountPoint.RotationDegrees = r + MeleeAttackAngle / 2f;
			MountPoint.Position = p3;
			//重新计算武器阴影位置。
			//空手(activeItem == null)时没有武器阴影可以算, 跳过。
			if (activeItem != null)
			{
				activeItem.CalcShadowTransform(true);
			}
			//创建屏幕抖动
			if (Face == FaceDirection.Right)
			{
				//GameCamera.Main.DirectionalShake(Vector2.FromAngle(Mathf.DegToRad(r - 90)) * 5);
				GameCamera.Main.DirectionalShake(Vector2.FromAngle(Mathf.DegToRad(r - 180)) * 6);
			}
			else
			{
				//GameCamera.Main.DirectionalShake(Vector2.FromAngle(Mathf.DegToRad(270 - r)) * 5);
				GameCamera.Main.DirectionalShake(Vector2.FromAngle(Mathf.DegToRad(-r)) * 6);
			}
			//播放特效
			var effect = ObjectManager.GetPoolItem<IEffect>(ResourcePath.prefab_effect_weapon_MeleeAttack1_tscn);
			var sprite = (Node2D)effect;
			//空手时没有枪口位置可量, 特效就落在 MountPoint 上(偏移 0)
			var localFirePosition = activeItem != null
				? (activeItem.GetLocalFirePosition() - activeItem.Position) * 0.9f
				: Vector2.Zero;
			sprite.Position = p1 + localFirePosition.Rotated(Mathf.DegToRad(r));
			sprite.RotationDegrees = r;
			AddChild(sprite);
			effect.PlayEffect();

			// ── 打开【主动近战判定】 ──
			// 从这一刻开始, Role.PhysicsProcess 每个物理帧都会查一次
			// MeleeAttackArea 的重叠区域并结算伤害, 不再依赖 AreaEntered
			// 是否恰好被物理步采到。这就是"挥得快也不会掉伤害"的根本原因。
			// 详见 Role.ProcessMeleeActiveQuery()。
			//
			// 注意这里传的是【这次挥击用的武器】可能会被下一刀换掉,
			// 但查询用的是 WeaponPack.ActiveItem, 换武器瞬间会有一次错配 ——
			// 影响极小(一帧), 不为它加锁。
			_meleeActiveQuery = true;
		}));
		tween.Chain();
		
		tween.TweenInterval(holdTime);
		tween.Chain();

		tween.TweenProperty(MountPoint, "rotation_degrees", r, returnTime);
		tween.TweenProperty(MountPoint, "position", p1, returnTime);
		tween.Chain();
		
		tween.TweenCallback(Callable.From(() =>
		{
			finish();
		}));

		//启用近战判定框, 覆盖整段挥刀动画。
		//【为什么不再放在"挥到位"那个回调里】挥刀动画被加快一倍之后, 那个窗口只剩 0.0125 秒,
		//比一个物理帧(1/60 ≈ 0.0167 秒)还短 —— 判定框的启用和禁用会落在同一个物理帧里被整帧跳过,
		//于是"挥了刀却打不到人"。开关细节与保底关闭见 Role.EnableMeleeHitArea()。
		//
		//【为什么现在再快也不怕了】伤害不再只靠 MeleeAttackArea.AreaEntered。
		// 挥到位那一刻会打开【主动判定】(见上面 _meleeActiveQuery),
		// Role.PhysicsProcess 每个物理帧都查一次重叠区域并结算, 与动画时长无关。
		//
		// ⚠️ 历史教训(2026-09-20): 曾经把 finish() 提到收回之前来"取消硬直",
		//   结果快速点击时完全没有伤害。当时的判定只能靠 AreaEntered,
		//   而判定框(0.05s)比新的攻击间隔(0.015s)长 -> 判定框再也不关 ->
		//   敌人进来后只有第一下结算。
		//   现在有了主动判定, 那个限制不存在了; 但【仍然不要单独提前 finish()】——
		//   收回动画是挥击的一部分, 提前解锁会让下一次挥击从收回中途起手。
		EnableMeleeHitArea(totalTime + 0.02f);

		tween.Play();
	}
}
