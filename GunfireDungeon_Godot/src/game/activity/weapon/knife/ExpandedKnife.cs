using Godot;

/// <summary>
/// 全站仪近战逻辑: 保留刀的挥击机制, 但扩大攻击范围并修正手持方向。
/// </summary>
public partial class ExpandedKnife : Knife
{
    public override void OnInit()
    {
        AttackRange = 70;
        base.OnInit();
    }

    protected override void OnActive()
    {
        base.OnActive();
        AnimatedSprite.Scale = new Vector2(-0.5f, 0.5f);
    }

    protected override void OnConceal()
    {
        base.OnConceal();
        AnimatedSprite.Scale = new Vector2(0.5f, 0.5f);
    }

    protected override void OnRemove(Role master)
    {
        AnimatedSprite.Scale = new Vector2(0.5f, 0.5f);
        base.OnRemove(master);
    }
}
