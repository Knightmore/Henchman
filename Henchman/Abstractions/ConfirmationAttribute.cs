namespace Henchman.Abstractions;

[AttributeUsage(AttributeTargets.Class)]
internal sealed class ConfirmationAttribute(string text = ConfirmationAttribute.DefaultText) : Attribute
{
    public const string DefaultText = """
                                      I hereby confirm, that I am solely responsible for any consequences and use these Tweaks/Hacks at my own risk!

                                      None of these hacks are persistent!

                                      USE IT AS IT IS, THERE WILL BE NO SUPPORT (NEITHER IN DMs NOR IN DISCORD SERVERS)
                                      """;

    public string Text { get; } = text;
}
