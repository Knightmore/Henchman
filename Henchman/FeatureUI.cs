namespace Henchman;

public abstract class FeatureUI
{
    public abstract string Name { get; }
    public abstract Action? Help { get; }
    public abstract bool LoginNeeded { get; }
    public virtual List<(string pluginName, bool mandatory)> Requirements { get; } = new();
    public abstract void Draw();
}
