
using System;
using Godot;
using Vector2 = Godot.Vector2;

public partial class Role
{
    //近战总动画约 0.1 秒，避免前摇拖慢首次出伤和连续攻击。
    private const float MeleeAttackWindupTime = 0.025f;
    private const float MeleeAttackHoldTime = 0.025f;
    private const float MeleeAttackReturnTime = 0.05f;

    /// <summary>
    /// 播放近战攻击动画
    /// </summary>
    public virtual void PlayAnimation_MeleeAttack(Action finish)
    {
        var r = MountPoint.RotationDegrees;
        //var gp = MountPoint.GlobalPosition;
        var p1 = MountPoint.Position;
        var p2 = p1 + new Vector2(6, 0).Rotated(Mathf.DegToRad(r - MeleeAttackAngle / 2f));
        var p3 = p1 + new Vector2(6, 0).Rotated(Mathf.DegToRad(r + MeleeAttackAngle / 2f));
        
        var tween = CreateTween();
        tween.SetParallel();
        
        tween.TweenProperty(MountPoint, "rotation_degrees", r - MeleeAttackAngle / 2f, MeleeAttackWindupTime);
        tween.TweenProperty(MountPoint, "position", p2, MeleeAttackWindupTime);
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

            //启用近战碰撞区域
            MeleeAttackCollision.Disabled = false;
        }));
        tween.Chain();
        
        tween.TweenInterval(MeleeAttackHoldTime);
        tween.Chain();

        tween.TweenCallback(Callable.From(() =>
        {
            //关闭近战碰撞区域
            MeleeAttackCollision.Disabled = true;
        }));
        tween.TweenProperty(MountPoint, "rotation_degrees", r, MeleeAttackReturnTime);
        tween.TweenProperty(MountPoint, "position", p1, MeleeAttackReturnTime);
        tween.Chain();
        
        tween.TweenCallback(Callable.From(() =>
        {
            finish();
        }));
        tween.Play();
    }
}
