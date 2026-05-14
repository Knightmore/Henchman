namespace Henchman.Abstractions;

public abstract class Feature
{
    public virtual void RunTask()           { }
    public virtual void RunTask(bool value) { }
}
