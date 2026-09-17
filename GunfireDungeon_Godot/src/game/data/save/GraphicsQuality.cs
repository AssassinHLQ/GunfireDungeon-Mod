using Godot;

/// <summary>
/// 画质档位。
/// <para/>
/// 这个项目是像素游戏, 世界渲染在 480x270 的 SubViewport 里再放大, 填充率本身很低,
/// 所以"低画质"要省的并不是分辨率, 而是下面这几样真正吃性能的东西:
/// <list type="bullet">
/// <item>辉光(glow) —— 全屏后期, 每帧多次模糊, 手机上最贵的一项</item>
/// <item>粒子数量 —— 子弹烟雾/血液/爆炸, 弹幕游戏里同屏数量非常大</item>
/// <item>伤害数字 —— 每次命中都会 new 一个 ActivityObject(带物理体), CPU 开销可观</item>
/// <item>屏幕震动 —— 低端机上会放大掉帧的观感</item>
/// </list>
/// </summary>
public enum QualityPreset
{
    /// <summary>低: 关辉光、砍粒子、关伤害数字与屏幕震动。给低端机与手机。</summary>
    Low = 0,

    /// <summary>中: 辉光减弱、粒子减量, 其余全开。</summary>
    Medium = 1,

    /// <summary>高: 全部效果全开。</summary>
    High = 2,

    /// <summary>自定义: 由玩家逐项调整, 预设不再覆盖。</summary>
    Custom = 3,
}

/// <summary>
/// 画质系统。
/// <para/>
/// 两个使用方式:
/// <list type="number">
/// <item><see cref="Install"/> 挂上 <see cref="SceneTree.NodeAdded"/>, 之后场景里新出现的
/// 粒子/环境/相机都会自动套用当前画质。地牢是逐层重建的, 靠这个钩子才能覆盖到新楼层。</item>
/// <item><see cref="Apply"/> 在设置里改画质时调用, 会把已经存在的节点也刷一遍。</item>
/// </list>
/// </summary>
public static class GraphicsQuality
{
    // ---------------------- 预设数值 ----------------------

    /// <summary>低画质的粒子数量比例</summary>
    public const float LowParticleAmount = 0.3f;

    /// <summary>中画质的粒子数量比例</summary>
    public const float MediumParticleAmount = 0.7f;

    /// <summary>高画质的粒子数量比例</summary>
    public const float HighParticleAmount = 1f;

    /// <summary>中画质辉光强度相对基准值的倍率</summary>
    private const float MediumGlowScale = 0.6f;

    // ---------------------- 元数据 key ----------------------
    // 用来记住"原始值"。因为画质是可以反复切换的, 每次都必须从原始值算,
    // 不能在上一次调整过的基础上再乘一遍, 否则越切越少。

    private static readonly StringName BaseAmountRatioMeta = "__gq_base_amount_ratio";
    private static readonly StringName BaseGlowStrengthMeta = "__gq_base_glow_strength";
    private static readonly StringName BaseGlowEnabledMeta = "__gq_base_glow_enabled";

    // ---------------------- 当前生效的设置 ----------------------

    private static bool _installed;
    private static bool _glowEnabled = true;
    private static float _glowScale = 1f;
    private static float _particleAmount = 1f;

    /// <summary>当前是否允许显示伤害数字</summary>
    public static bool DamageNumberEnabled { get; private set; } = true;

    /// <summary>当前是否允许屏幕震动</summary>
    public static bool ScreenShakeEnabled { get; private set; } = true;

    /// <summary>
    /// 挂上全局钩子。重复调用是安全的。
    /// </summary>
    public static void Install(SceneTree tree)
    {
        if (_installed || tree == null)
        {
            return;
        }

        _installed = true;
        tree.NodeAdded += OnNodeAdded;
    }

    private static void OnNodeAdded(Node node)
    {
        //场景里节点极多, 先做最便宜的过滤
        switch (node)
        {
            case GpuParticles2D particles:
                ApplyParticles(particles);
                break;
            case WorldEnvironment worldEnvironment:
                ApplyEnvironment(worldEnvironment);
                break;
            case GameCamera camera:
                camera.EnableShake = ScreenShakeEnabled;
                break;
        }
    }

    // ---------------------- 预设 <-> 存档 ----------------------

    /// <summary>
    /// 首次运行时按设备自动挑一档; 之后每次启动都把子项同步成预设值, 保证两边一致。
    /// </summary>
    public static void Initialize(GameSave save)
    {
        if (save == null)
        {
            return;
        }

        if (!save.QualityInitialized)
        {
            //只自动判断一次 —— 玩家手动改过之后就不再覆盖他
            save.QualityInitialized = true;
            save.QualityLevel = (int)DetectPreset();
            ApplyPreset(save);
            return;
        }

        if ((QualityPreset)save.QualityLevel != QualityPreset.Custom)
        {
            ApplyPreset(save);
        }
    }

    /// <summary>
    /// 把预设写进各个子项。自定义档不做任何事(保留玩家自己调的值)。
    /// </summary>
    public static void ApplyPreset(GameSave save)
    {
        if (save == null)
        {
            return;
        }

        switch ((QualityPreset)save.QualityLevel)
        {
            case QualityPreset.Low:
                save.GlowEnabled = false;
                save.ParticleAmount = LowParticleAmount;
                save.DamageNumberEnabled = false;
                save.ScreenShakeEnabled = false;
                break;

            case QualityPreset.Medium:
                save.GlowEnabled = true;
                save.ParticleAmount = MediumParticleAmount;
                save.DamageNumberEnabled = true;
                save.ScreenShakeEnabled = true;
                break;

            case QualityPreset.High:
                save.GlowEnabled = true;
                save.ParticleAmount = HighParticleAmount;
                save.DamageNumberEnabled = true;
                save.ScreenShakeEnabled = true;
                break;

            case QualityPreset.Custom:
                break;
        }
    }

    /// <summary>
    /// 按设备挑一个保守的初始档位。
    /// 桌面端默认给高(和原来的表现一致), 手机/平板默认给低。
    /// </summary>
    private static QualityPreset DetectPreset()
    {
        if (OS.HasFeature("mobile"))
        {
            return QualityPreset.Low;
        }

        //核心数很少的老机器给中档, 免得一上手就卡
        if (OS.GetProcessorCount() <= 4)
        {
            return QualityPreset.Medium;
        }

        return QualityPreset.High;
    }

    // ---------------------- 应用 ----------------------

    /// <summary>
    /// 把存档里的画质设置应用到当前场景, 并刷新已存在的节点。
    /// </summary>
    public static void Apply(GameSave save)
    {
        if (save == null)
        {
            return;
        }

        _glowEnabled = save.GlowEnabled;
        _glowScale = (QualityPreset)save.QualityLevel == QualityPreset.Medium ? MediumGlowScale : 1f;
        _particleAmount = Mathf.Clamp(save.ParticleAmount, 0f, 1f);
        DamageNumberEnabled = save.DamageNumberEnabled;
        ScreenShakeEnabled = save.ScreenShakeEnabled;

        var app = GameApplication.Instance;
        var tree = app?.GetTree();
        if (tree?.Root == null)
        {
            return;
        }

        //只刷【世界根节点】下面。
        //需要刷的粒子/环境都在世界里(SceneRoot 下面), 而整棵树还包含体积很大的 UI ——
        //玩家在设置里拖粒子滑块时这个函数会连着调用很多次, 没必要去遍历 UI。
        var sweepRoot = app.SceneRoot as Node ?? tree.Root;
        Sweep(sweepRoot);

        if (GameCamera.Main != null)
        {
            GameCamera.Main.EnableShake = ScreenShakeEnabled;
        }
    }

    private static void Sweep(Node node)
    {
        switch (node)
        {
            case GpuParticles2D particles:
                ApplyParticles(particles);
                break;
            case WorldEnvironment worldEnvironment:
                ApplyEnvironment(worldEnvironment);
                break;
            case GameCamera camera:
                camera.EnableShake = ScreenShakeEnabled;
                break;
        }

        foreach (var child in node.GetChildren())
        {
            Sweep(child);
        }
    }

    /// <summary>
    /// 粒子减量。用 amount_ratio 而不是直接改 amount ——
    /// amount 是各个特效自己配的比例, 直接覆盖会丢掉美术调好的量;
    /// amount_ratio 是整体缩放系数, 正合适。
    /// </summary>
    private static void ApplyParticles(GpuParticles2D particles)
    {
        if (!particles.HasMeta(BaseAmountRatioMeta))
        {
            particles.SetMeta(BaseAmountRatioMeta, particles.AmountRatio);
        }

        var baseRatio = particles.GetMeta(BaseAmountRatioMeta).AsSingle();
        particles.AmountRatio = Mathf.Clamp(baseRatio * _particleAmount, 0f, 1f);
    }

    /// <summary>
    /// 辉光开关与强度。
    /// <para/>
    /// 两个注意点:
    /// <list type="number">
    /// <item>要记住原始强度, 否则每次切换都会在上次结果上再乘一次, 越切越暗。</item>
    /// <item>要记住场景本身是不是开了辉光。项目里 default_env.tres 之类的环境本来就没开,
    /// 如果无脑打开, 会把不该有辉光的场景点亮。</item>
    /// </list>
    /// </summary>
    private static void ApplyEnvironment(WorldEnvironment worldEnvironment)
    {
        var env = worldEnvironment.Environment;
        if (env == null)
        {
            return;
        }

        if (!env.HasMeta(BaseGlowStrengthMeta))
        {
            env.SetMeta(BaseGlowStrengthMeta, env.GlowStrength);
            env.SetMeta(BaseGlowEnabledMeta, env.GlowEnabled);
        }

        var baseStrength = env.GetMeta(BaseGlowStrengthMeta).AsSingle();
        var baseEnabled = env.GetMeta(BaseGlowEnabledMeta).AsBool();

        //场景本来就没开辉光的, 保持不开
        env.GlowEnabled = baseEnabled && _glowEnabled;

        if (env.GlowEnabled)
        {
            env.GlowStrength = baseStrength * _glowScale;
        }
    }
}
