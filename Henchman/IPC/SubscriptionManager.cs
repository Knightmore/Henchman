using System.Linq;
using System.Reflection;
using Dalamud.Plugin.Services;
using ECommons.EzIpcManager;

namespace Henchman.IPC;

internal static class SubscriptionManager
{
    private static readonly HashSet<string> InitializedIPCs = new();


    internal static bool IsInitialized(string plugin) => InitializedIPCs.Contains(plugin) && IsLoaded(plugin);

    internal static bool IsLoaded(string pluginName)
    {
        return Svc.PluginInterface.InstalledPlugins.Any(x => x.InternalName == pluginName && x.IsLoaded);
    }

    public static void Subscribe(IFramework framework)
    {
        try
        {
            EzIPC.Init(typeof(IPCProvider), "Henchman");
            foreach (var type in Assembly.GetExecutingAssembly()
                                         .GetTypes()
                                         .Where(type => type.GetCustomAttribute<IPCAttribute>() != null))
            {
                var attr = type.GetCustomAttribute<IPCAttribute>();
                if (!IsInitialized(attr.Name))
                {
                    if (IsLoaded(attr.Name))
                    {
                        // Why has RSR a different IPC prefix than its internal name!? (╯°□°)╯︵ ┻━┻
                        EzIPC.Init(type, attr.Name == "RotationSolver"
                                                 ? "RotationSolverReborn"
                                                 : attr.Name);
                        InitializedIPCs.Add(attr.Name);
                        Debug($"{attr.Name} IPC registered.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            PluginLog.Error("Could not subscribe to IPCs");
        }
    }

    public static bool AreMandatoryEnabled(this List<(string pluginName, bool mandatory)> requirements)
    {
        var missingPlugins = requirements
                            .Where(x => x.mandatory && !IsInitialized(x.pluginName))
                            .ToList();

        if (missingPlugins.Count > 0)
            Svc.Chat.PrintError($"Required plugins not enabled: \n {string.Join("\n", missingPlugins.Select(x => x.pluginName))}");

        return missingPlugins.Count == 0;
    }
}
