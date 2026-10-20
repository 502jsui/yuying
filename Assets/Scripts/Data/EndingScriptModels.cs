using System;
using System.Collections.Generic;

/// <summary>结局演出里由新结算层执行的画面提示。</summary>
public enum EndingCue
{
    None,
    RainHeavy,
    LightsOut,
    FocusMotherDoor,
    TimeCard,
    AllAtDoors,
    GirlAlone,
    GirlOlder,
    GirlLeaving,
    DoorsOpen,
    RainFadeOut,
    SnowStart,
    WalkToExit,
    FadeToWhite
}

/// <summary>
/// 一句结算台词或一个纯画面节拍。
/// text 为空而 cueText 有值时，表示时间卡 / 黑场提示，不进入字幕。
/// </summary>
[Serializable]
public class EndingLine
{
    public string speaker;
    public string text;
    public float holdSeconds;
    public float pauseAfterSeconds;
    public EndingCue cue;
    public string cueText;
    public float cueSeconds;

    public bool HasDialogue
    {
        get { return !string.IsNullOrEmpty(text); }
    }
}

/// <summary>一套完整结局演出。</summary>
[Serializable]
public class EndingScript
{
    public EndingId id;
    public string title;
    public List<EndingLine> lines = new List<EndingLine>();
}
