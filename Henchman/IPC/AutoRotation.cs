namespace Henchman.IPC;

internal static class AutoRotation
{
    internal static void Enable()
    {
        switch (C.AutoRotationPlugin)
        {
            case IPCNames.BossMod:
                Bossmod.EnableRotation();
                break;
            case IPCNames.RotationSolverReborn:
                RotationSolverReborn.Enable();
                break;
            case IPCNames.Wrath:
                Wrath.EnableWrathAutoAndConfigureIt();
                break;
        }
    }

    internal static void Disable()
    {
        switch (C.AutoRotationPlugin)
        {
            case IPCNames.BossMod:
                Bossmod.DisableRotation();
                break;
            case IPCNames.RotationSolverReborn:
                RotationSolverReborn.Disable();
                break;
            case IPCNames.Wrath:
                Wrath.DisableWrath();
                break;
        }
    }
}
