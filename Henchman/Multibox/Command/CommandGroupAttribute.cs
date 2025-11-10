namespace Henchman.Multibox.Command
{
    [AttributeUsage(AttributeTargets.Class)]
    public class CommandGroupAttribute(string name = null) : Attribute
    {
        public string? Name { get; } = name;
    }
}
