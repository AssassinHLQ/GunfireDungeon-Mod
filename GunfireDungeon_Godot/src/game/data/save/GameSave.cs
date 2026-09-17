
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using FileAccess = Godot.FileAccess;

public partial class GameSave
{
    /// <summary>
    /// 是否全屏
    /// </summary>
    [JsonInclude]
    public bool FullScreen = true;
    
    /// <summary>
    /// 是否垂直同步
    /// </summary>
    [JsonInclude]
    public bool VerticalSync = true;
    
    /// <summary>
    /// 背景音乐音量, (0 - 1)
    /// </summary>
    [JsonInclude]
    public float BgmVolume = 0.5f;
    
    /// <summary>
    /// 音效音量, (0 - 1)
    /// </summary>
    [JsonInclude]
    public float SfxVolume = 0.5f;

    /// <summary>
    /// 主音量, (0 - 1)。
    /// 作用相当于系统的音量合成器总推子: 它同时缩放到 BGM 与 SFX 两条总线上,
    /// 也就是说 BGM 总线最终音量 = MasterVolume * BgmVolume, SFX 同理。
    /// </summary>
    [JsonInclude]
    public float MasterVolume = 1f;

    /// <summary>
    /// 帧率上限。
    /// 0 表示不限制；其余值直接给 Engine.MaxFps。
    /// </summary>
    [JsonInclude]
    public int TargetFps = 60;

    /// <summary>
    /// 鼠标跟随进度（0 - 1）
    /// </summary>
    [JsonInclude]
    public float FollowsMouseAmount = 0f;

    /// <summary>
    /// 是否使用完美像素
    /// </summary>
    [JsonInclude]
    public bool PerfectPixel = true;

    //======================= 画质 =========================
    // 这几个字段由 GraphicsQuality 统一管理, 正常情况下不要单独改其中一个 ——
    // 非"自定义"档位下, 每次启动都会用预设值把它们覆盖回来。

    /// <summary>
    /// 是否已经做过首次画质判断。false = 还没判断过, 会在启动时按设备自动选一档。
    /// </summary>
    [JsonInclude]
    public bool QualityInitialized = false;

    /// <summary>
    /// 画质档位, 取值见 <see cref="QualityPreset"/>
    /// </summary>
    [JsonInclude]
    public int QualityLevel = (int)QualityPreset.High;

    /// <summary>
    /// 是否开启辉光(全屏后期, 手机上最贵的一项)
    /// </summary>
    [JsonInclude]
    public bool GlowEnabled = true;

    /// <summary>
    /// 粒子数量比例 (0 - 1)
    /// </summary>
    [JsonInclude]
    public float ParticleAmount = 1f;

    /// <summary>
    /// 是否显示伤害数字
    /// </summary>
    [JsonInclude]
    public bool DamageNumberEnabled = true;

    /// <summary>
    /// 是否允许屏幕震动
    /// </summary>
    [JsonInclude]
    public bool ScreenShakeEnabled = true;

    
    /// <summary>
    /// 手柄锁定瞄准
    /// </summary>
    [JsonInclude]
    public bool JoystickAimAssist = true;
    
    /// <summary>
    /// 手柄辅助瞄准强度
    /// </summary>
    [JsonInclude]
    public float JoystickAimAssistStrength = 0.5f;

    /// <summary>
    /// 自动索敌: 开启后自动锁定最近的敌人并瞄准它, 不需要手动移动鼠标/摇杆瞄准。
    /// 对触屏与手柄玩家友好。
    /// </summary>
    [JsonInclude]
    public bool AutoTarget = true;

    /// <summary>
    /// 自动换弹: 弹夹打空后自动装填, 不需要再按一次换弹键。
    /// 与武器自身的 AutoReload 属性取或 —— 武器配了就是开的, 没配则听这个开关。
    /// </summary>
    [JsonInclude]
    public bool AutoReload = true;

    /// <summary>
    /// 自定义键位, 键为输入动作名称, 值为物理键码
    /// </summary>
    [JsonInclude]
    public Dictionary<string, long> KeyBindings = new();
    
    private float _timer;

    public void Init(GameApplication app)
    {
        GameApplication.Instance.CallDelay(0, () =>
        {
            DisplayServer.WindowSetMode(FullScreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
            DisplayServer.WindowSetVsyncMode(VerticalSync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
        });
        SoundManager.ApplyAllBusVolume();
        app.SetPerfectPixel(PerfectPixel);
        ApplyTargetFps();

        //画质: 先按设备定档(只做一次), 再应用到场景。
        //必须在场景里的地牢/粒子创建之前调用, 否则会先按全特效渲染一帧。
        GraphicsQuality.Initialize(this);
        GraphicsQuality.Apply(this);
    }

    /// <summary>
    /// 把帧率上限应用到引擎。0 = 不限制。
    /// </summary>
    public void ApplyTargetFps()
    {
        Engine.MaxFps = TargetFps <= 0 ? 0 : TargetFps;
    }

    public void Save()
    {
        var options = new JsonSerializerOptions();
        options.WriteIndented = true;
        var serialize = JsonSerializer.Serialize(this, options);
        
        SaveFile(GameConfig.GameSaveFile, serialize);
    }

    public static GameSave Load()
    {
        GameSave save;
        if (!HasFile(GameConfig.GameSaveFile))
        {
            save = new GameSave();
            save.Save();
        }
        else
        {
            var readFile = ReadFile(GameConfig.GameSaveFile);
            if (string.IsNullOrEmpty(readFile))
            {
                save = new GameSave();
                save.Save();
            }
            else
            {
                try
                {
                    save = JsonSerializer.Deserialize<GameSave>(readFile);
                }
                catch (Exception e)
                {
                    global::Debug.LogError("读取存档失败, 重新生成存档: " + e);
                    save = new GameSave();
                    save.Save();
                }
            }
        }
        
        if (save.Debug == null)
        {
            save.Debug = new DebugData();
            save.Debug.Init();
        }
        
        return save;
    }

    /// <summary>
    /// 延时保存
    /// </summary>
    public void LateSave()
    {
        _timer = 3f;
    }

    public void Tick(float delta)
    {
        if (_timer > 0)
        {
            _timer -= delta;
            if (_timer <= 0)
            {
                Save();
            }
        }
    }

    private static void SaveFile(string fileName, string text)
    {
        var osName = OS.GetName();
        if (osName == "Windows")
        {
            File.WriteAllText(fileName, text);
        }
        else if (osName == "macOS")
        {
#if TOOLS
            File.WriteAllText(fileName, text);
#else
            var file = FileAccess.Open("user://" + fileName, FileAccess.ModeFlags.Write);
            file.StoreString(text);
            file.Close();
#endif
        }
        else
        {
            var file = FileAccess.Open("user://" + fileName, FileAccess.ModeFlags.Write);
            file.StoreString(text);
            file.Close();
        }
    }

    private static bool HasFile(string fileName)
    {
        var osName = OS.GetName();
        if (osName == "Windows")
        {
            return File.Exists(fileName);
        }
        else if (osName == "macOS")
        {
#if TOOLS
            return File.Exists(fileName);
#else
            return FileAccess.FileExists("user://" + fileName);
#endif
        }
        else
        {
            return FileAccess.FileExists("user://" + fileName);
        }
    }

    private static string ReadFile(string fileName)
    {
        var osName = OS.GetName();
        if (osName == "Windows")
        {
            return File.ReadAllText(fileName);
        }
        else if (osName == "macOS")
        {
#if TOOLS
            return File.ReadAllText(fileName);
#else
            var file = FileAccess.Open("user://" + fileName, FileAccess.ModeFlags.Read);
            var save = file.GetAsText();
            file.Close();
            return save;
#endif
        }
        else
        {
            var file = FileAccess.Open("user://" + fileName, FileAccess.ModeFlags.Read);
            var save = file.GetAsText();
            file.Close();
            return save;
        }
    }
}