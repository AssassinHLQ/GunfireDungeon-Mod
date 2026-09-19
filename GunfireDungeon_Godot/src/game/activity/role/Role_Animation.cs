
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
	/// 2026-09-20 由用户要求: 加快手持枪类武器时的近战挥击。
	/// 0.6 表示三段时长各乘 0.6, 总时长 0.05s -> 0.03s。
	/// </summary>
	private const float GunMeleeSpeedScale = 0.6f;

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
		//⚠️ 动画越短这个保底越重要: 枪类走 0.6 倍后总时长只有 0.03 秒,
		//   加上 0.02 秒余量 = 0.05 秒, 仍然覆盖得住至少一个物理帧。
		EnableMeleeHitArea(totalTime + 0.02f);

		tween.Play();
	}
}
