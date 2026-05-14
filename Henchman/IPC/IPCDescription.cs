namespace Henchman.IPC;

[AttributeUsage(AttributeTargets.Method)]
public class IPCDescriptionAttribute(string text) : Attribute
{
    public string Text { get; } = text;
}
