#if PRIVATE
using Henchman.Features.General.Multibox;
using Henchman.Features.Private.LGBInspector;
using Henchman.Features.Private.MappingTheRealm;
using Henchman.Features.Private.Debugging;
#endif
using System.Linq;
using System.Reflection;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using ECommons.Configuration;
using Henchman.Data;
using Henchman.Features.General;
using Henchman.Helpers;
using Henchman.Tweaks;
using Henchman.Windows;
using Module = ECommons.Module;

namespace Henchman;

public class Henchman : IDalamudPlugin
{
    private const string CommandName = "/henchman";

    internal static Henchman? P;

    public static readonly HashSet<FeatureUI> FeatureSet      = [];
    public static readonly HashSet<FeatureUI> ExperimentalSet = [];
    public static readonly HashSet<FeatureUI> GeneralSet      = [];
    

    public readonly WindowSystem  WindowSystem = new("Henchman");
    public          Configuration Config;
    private         FeatureBar    featureBar;
    internal        FeatureWindow FeatureWindow;
    private         MainWindow    mainWindow;
    internal        string        SelectedFeatureName = string.Empty;

    public Henchman(IDalamudPluginInterface pluginInterface)
    {
        P = this;

        ECommonsMain.Init(pluginInterface, this, Module.DalamudReflector, Module.ObjectFunctions);

        Initialize();
    }

    public          string        Name => "Henchman";
    internal static Configuration C    => P.Config;

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        mainWindow.Dispose();
        featureBar.Dispose();
        FeatureWindow.Dispose();

        Svc.Commands.RemoveHandler(CommandName);

        CancelAllTasks();
        Wrath.DisableWrath();

#if PRIVATE
        if(TryGetFeature<MultiboxUI>(out var Mutlibox))
            Mutlibox.Dispose();
#endif
        Svc.Framework.Update                     -= TaskManagerTick;
        Svc.Framework.Update                     -= SubscriptionManager.Subscribe;
        Svc.Framework.Update                     -= Tick;
        Svc.PluginInterface.UiBuilder.Draw       -= DrawUi;
        Svc.PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        ECommonsMain.Dispose();
    }

    private void Initialize()
    {
        HuntDatabase.Initialize();

        EzConfig.Migrate<Configuration>();
        Config               =  EzConfig.Init<Configuration>();
        Svc.Framework.Update += TaskManagerTick;

        Svc.Framework.Update += SubscriptionManager.Subscribe;

#if PRIVATE
        MappingTheRealm.Initialize();
#endif

        foreach (var type in GetType()
                            .Assembly.GetTypes()
                            .Where(type => type.GetCustomAttribute<FeatureAttribute>() != null))
        {
            var instance = (FeatureUI)Activator.CreateInstance(type)!;
            FeatureSet.Add(instance);
        }

        foreach (var type in GetType()
                            .Assembly.GetTypes()
                            .Where(type => type.GetCustomAttribute<ExperimentalAttribute>() != null))
        {
            var instance = (FeatureUI)Activator.CreateInstance(type)!;
            ExperimentalSet.Add(instance);
        }

        foreach (var type in GetType()
                            .Assembly.GetTypes()
                            .Where(type => type.GetCustomAttribute<GeneralAttribute>() != null))
        {
            var instance = (FeatureUI)Activator.CreateInstance(type)!;
            GeneralSet.Add(instance);
        }

        mainWindow = new MainWindow(this);
        featureBar = new FeatureBar();
        FeatureWindow = new FeatureWindow();

        WindowSystem.AddWindow(mainWindow);
        WindowSystem.AddWindow(featureBar);
        WindowSystem.AddWindow(FeatureWindow);


        Svc.Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
                                             {
                                                     HelpMessage = "Open plugin window"
                                             });

        Svc.PluginInterface.UiBuilder.Draw       += DrawUi;
        Svc.PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Svc.Framework.Update += Tick;
    }

    private static void Tick(object _)
    {
        TextAdvanceManager.Tick();
        YesAlreadyManager.Tick();
        SkipTalk.Tick();
    }

    private void OnCommand(string command, string args)
    {
        ToggleMainUi();
    }

    private void DrawUi()
    {
        WindowSystem.Draw();
    }

    public void ToggleMainUi()
    {
        if(!C.SeparateWindows)
            mainWindow.Toggle();
        else
            featureBar.Toggle();
    }

    public static bool TryGetFeature<T>(out T result) where T : FeatureUI
    {
        result = FeatureSet.OfType<T>()
                           .FirstOrDefault();
        return result != null;
    }
}
