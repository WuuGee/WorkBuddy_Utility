namespace FocusCatAI.Domain.Pets;

public enum PetState
{
    Idle,
    Walking,
    Sleeping,
    Thinking,
    Talking,
    Happy
}

/// <summary>
/// Name of an animation defined in a pet manifest (pet.json). It is a string, not an enum, so a pet or skin
/// can add new animations (Groom, Yawn, Jump...) without code changes; behavior picks them up by tag.
/// </summary>
public readonly record struct PetActivity(string Name)
{
    public static readonly PetActivity Idle = new("Idle");
    public static readonly PetActivity Sit = new("Sit");
    public static readonly PetActivity Stretch = new("Stretch");
    public static readonly PetActivity WalkLeft = new("WalkLeft");
    public static readonly PetActivity WalkRight = new("WalkRight");
    public static readonly PetActivity Sleep = new("Sleep");
    public static readonly PetActivity Think = new("Think");
    public static readonly PetActivity Speak = new("Speak");
    public static readonly PetActivity Happy = new("Happy");
    public static readonly PetActivity Blink = new("Blink");
    public static readonly PetActivity TailFlick = new("TailFlick");
    public static readonly PetActivity LookLeft = new("LookLeft");
    public static readonly PetActivity LookRight = new("LookRight");
    public static readonly PetActivity Groom = new("Groom");

    public override string ToString() => Name;
}

public enum PetTrigger
{
    ChatStarted,
    AnswerReady,
    SpeechFinished,
    Speak,
    SitDown,
    Celebrate,
    UserInteracted
}
