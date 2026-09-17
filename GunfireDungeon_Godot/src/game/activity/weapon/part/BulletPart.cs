
using Config;
using Godot;

/// <summary>
/// 子弹零件
/// </summary>
[Part("Bullet")]
public class BulletPart : PartLogicBase
{
    /// <summary>
    /// 逻辑块消耗法力值
    /// </summary>
    public int Mana { get; set; }
    
    /// <summary>
    /// 散射角度
    /// </summary>
    public int ScatteringAngle { get; set; } = 5;
    
    /// <summary>
    /// 发射的子弹
    /// </summary>
    public ExcelConfig.BulletBase Bullet { get; set; }

    /// <summary>
    /// 子弹数量
    /// </summary>
    public int Count { get; set; } = 1;

    public override int GetMana()
    {
        return Mana;
    }

    public override void InitParam(ExcelConfig.PartBase config)
    {
        Mana = config.BaseMana;
        var bulletId = config.Param["Bullet"].GetString();
        Bullet = ExcelConfig.BulletBase_Map[bulletId];
        ScatteringAngle = Utils.Random.RandomConfigRange(config.Param.GetParam<int[]>("ScatteringAngle", [ScatteringAngle]));
        Count = config.Param.GetParam("Count", Count);
    }

    public override IBullet[] Execute(PlanningParam param)
    {
        if (!param.UseManaBuff(Mana))
        {
            param.SufficientMana = false;
            param.SetValue(PlanningParam.NoManaIndex, Index);
            return null;
        }
        // Debug.Log($"射击子弹({Index}), fireRotation:{param.FireRotation}");
        if (Bullet != null)
        {
            param.HasBullet = true;
            if (!param.HasValue(PlanningParam.FirstBullet))
            {
                param.SetValue(PlanningParam.FirstBullet, Bullet);
            }
            
            var bulletParam = new FireBulletParam(Bullet);
            bulletParam.FireRotation = param.FireRotation;
            
            if (param.HasValue(PlanningParam.PrevBullet)) // 有上一个子弹
            {
                var bulletInst = param.GetValue<IBullet>(PlanningParam.PrevBullet);
                bulletParam.Position = bulletInst.GetEndPosition();
                bulletParam.Altitude = bulletInst.Altitude;
                bulletParam.Camp = bulletInst.Camp;
            }
            else //没有上一个子弹
            {
                bulletParam.Position = Weapon.FirePoint.GlobalPosition;
                if (Weapon.TriggerRole != null && !Weapon.TriggerRole.IsDestroyed)
                {
                    bulletParam.Camp = Weapon.TriggerRole.Camp;
                    bulletParam.Altitude = Weapon.TriggerRole.GetFirePointAltitude();
                }
                else
                {
                    bulletParam.Camp = CampEnum.None;
                    bulletParam.Altitude = 1;
                }
            }
            
            // 「分裂子弹」这类被动通过 RoleState.CalcBulletCountEvent 改变每次开火发射的弹丸数量。
            // 这里必须调用 CalcBulletCount，否则对应的道具挂上了事件也永远不会生效。
            var bulletCount = Count;
            RoleState roleState = null;
            if (Weapon.Master != null && !Weapon.Master.IsDestroyed)
            {
                roleState = Weapon.Master.RoleState;
            }
            else if (Weapon.TriggerRole != null && !Weapon.TriggerRole.IsDestroyed)
            {
                roleState = Weapon.TriggerRole.RoleState;
            }

            if (roleState != null)
            {
                bulletCount = Mathf.Max(1, roleState.CalcBulletCount(bulletCount));
            }

            var result = new IBullet[bulletCount];
            for (var i = 0; i < bulletCount; i++)
            {
                result[i] = ShootBullet(bulletParam.Clone());
            }
            
            return result;
        }
        
        return null;
    }

    public IBullet ShootBullet(FireBulletParam param)
    {
        param.FireRotation += Mathf.DegToRad(Utils.Random.RandomRangeFloat(-ScatteringAngle, ScatteringAngle));
        if (Weapon.Master != null && !Weapon.Master.IsDestroyed)
        {
            param.Bullet = Weapon.Master.RoleState.CalcBullet(param.Bullet);
            var bInst = FireManager.ShootBullet(Weapon, param);
            Weapon.Master.ShootBulletHandler(Weapon, param.FireRotation, bInst);
            return bInst;
        }
        else if (Weapon.TriggerRole != null && !Weapon.TriggerRole.IsDestroyed)
        {
            param.Bullet = Weapon.TriggerRole.RoleState.CalcBullet(param.Bullet);
            var bInst = FireManager.ShootBullet(Weapon, param);
            Weapon.TriggerRole.ShootBulletHandler(Weapon, param.FireRotation, bInst);
            return bInst;
        }
        else
        {
            var bInst = FireManager.ShootBullet(Weapon, param);
            Weapon.Master?.ShootBulletHandler(Weapon, param.FireRotation, bInst);
            return bInst;
        }
    }
}