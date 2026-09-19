
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
	/// 正在播放的挥击 tween。下一次挥击会把它 Kill 掉。
	/// 不 Kill 的话两个 tween 会同时写 MountPoint.Position, 武器会抖成一团。
	/// </summary>
	private Tween _meleeTween;

	/// <summary>
	/// 挥击的"静止位置"(相对 MountPoint 的挂载偏移)。
	///
	/// 【为什么要缓存】原来每次挥击都取 <c>MountPoint.Position</c> 当静止点。
	/// 现在允许在上一次收回动画没播完时再次挥击, 那时候 MountPoint 还停在半路 ——
	/// 直接拿它当静止点, 每连击一次起手点就往前漂 6 像素。
	/// 所以只有在【没有挥击动画在播】时才重新采样。
	/// </summary>
	private Vector2 _meleeRestPos;
	private bool _meleeRestPosValid;

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

		//上一次的收回动画还没播完就又挥了一刀 -> 直接砍掉, 从静止姿势重新起手
		var interrupted = _meleeTween != null && _meleeTween.IsValid();
		if (interrupted)
		{
			_meleeTween.Kill();
		}

		if (!interrupted || !_meleeRestPosValid)
		{
			_meleeRestPos = MountPoint.Position;
			_meleeRestPosValid = true;
		}
		MountPoint.Position = _meleeRestPos;

		//旋转不用缓存: 出招+判定结束就会把 MountLookTarget 交回瞄准(见下面 finish 的时机),
		//所以打断时 MountPoint.RotationDegrees 已经是当前瞄准角度了。
		var r = MountPoint.RotationDegrees;
		var p1 = _meleeRestPos;
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

		// ── 关键改动: 【出招 + 判定结束就解锁下一次攻击】 ──
		// 原来 finish() 挂在整段动画的最后, 于是"必须等收回动画播完才能再挥一刀",
		// 收回时长直接变成了硬直。现在把它提到收回之前:
		//   · finish() 里会把 _meleeAttackPlaying 置回 false, 并让 MountLookTarget 恢复
		//     -> 下一刀随时可以挥, 而且瞄准立刻重新跟手
		//   · 收回只剩"把前送的 6 像素拉回来"这一个纯表现动作, 不再阻塞
		// 旋转也交回 MountLookTarget(SetLookAt 每帧重设), 所以收回不再补间旋转,
		// 免得和瞄准抢同一个属性。
		tween.TweenCallback(Callable.From(() => finish()));
		tween.Chain();

		tween.TweenProperty(MountPoint, "position", p1, returnTime);
		tween.Chain();

		//启用近战判定框, 覆盖整段挥刀动画。
		//【为什么不再放在"挥到位"那个回调里】挥刀动画被加快一倍之后, 那个窗口只剩 0.0125 秒,
		//比一个物理帧(1/60 ≈ 0.0167 秒)还短 —— 判定框的启用和禁用会落在同一个物理帧里被整帧跳过,
		//于是"挥了刀却打不到人"。开关细节与保底关闭见 Role.EnableMeleeHitArea()。
		//
		//⚠️ 动画越短这个保底越重要: 枪类走 0.6 倍后总时长只有 0.03 秒,
		//   加上 0.02 秒余量 = 0.05 秒, 仍然覆盖得住至少一个物理帧。
		EnableMeleeHitArea(totalTime + 0.02f);

		_meleeTween = tween;
		tween.Play();
	}
}
