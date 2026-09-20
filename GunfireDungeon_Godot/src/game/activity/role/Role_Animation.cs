
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
	/// ⚠️ 【下限提醒】0.02 秒只有 1.2 个物理帧(1/60 ≈ 0.0167s)。
	///    再往下调, 整段挥击有可能被压缩进同一个物理帧里跑完 ——
	///    那时"挥到位"的姿势物理步完全看不到, 判定框扫不到人,
	///    就会重现"挥了没伤害"。如果 0.4 已经出问题, 说明到极限了,
	///    正确的做法是把判定从 AreaEntered 改成"挥到位主动查一次重叠区域",
	///    而不是继续压这个数。
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
		var isMeleeWeapon = WeaponPack.ActiveItem?.Attribute?.IsMelee == true;
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
			//重新计算武器阴影位置
			var activeItem = WeaponPack.ActiveItem;
			activeItem.CalcShadowTransform(true);
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
			var localFirePosition = activeItem.GetLocalFirePosition() - activeItem.Position;
			localFirePosition *= 0.9f;
			sprite.Position = p1 + localFirePosition.Rotated(Mathf.DegToRad(r));
			sprite.RotationDegrees = r;
			AddChild(sprite);
			effect.PlayEffect();
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
		//⚠️⚠️ 【绝对不能把 finish() 提前到收回之前】⚠️⚠️
		// 2026-09-20 试过一次"出招+判定结束就解锁下一次攻击"(取消收回硬直),
		// 结果【快速点击时近战完全没伤害】。原因:
		//   伤害是靠 MeleeAttackArea.AreaEntered 触发的, 它只在"重叠开始的那一刻"响一次。
		//   判定框开启时长 = totalTime + 0.02, 本来就比一次攻击间隔长一点点,
		//   正常情况靠"两次攻击之间判定框会关一下再开"来重新触发 AreaEntered。
		//   一旦把解锁提前到 0.015s(枪), 判定框(0.05s)就【再也不会关闭】,
		//   敌人进来之后只有第一次会结算, 后面每一下都不掉血。
		// 要保持"每次都结算", 攻击间隔就必须大于判定框时长。想再加快只能整体缩短动画,
		// 不要单独提前 finish()。
		EnableMeleeHitArea(totalTime + 0.02f);

		tween.Play();
	}
}
