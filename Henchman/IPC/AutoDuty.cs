using Henchman.Multiboxing.Command;

namespace Henchman.IPC;

[CommandGroup]
public static class AutoDuty
{
    [Command]
    public static void RunDutyUnsync(uint territoryType, int loops = 1, bool bareMode = true)
        => Underlings.IPC.AutoDuty.RunDutyUnsync(territoryType, loops, bareMode);
}
