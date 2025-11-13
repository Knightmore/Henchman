#if PRIVATE
using Henchman.Features.Private.MappingTheRealm;
using Henchman.Features.Private;
#endif
using System.Linq;
using System.Reflection;
using Dalamud.Game;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using ECommons.Automation;
using ECommons.Configuration;
using ECommons.EzHookManager;
using Henchman.Data;
using Henchman.Features.BumpOnALog;
using Henchman.Features.RetainerVocate;
using Henchman.Helpers;
using Henchman.TaskManager;
using Henchman.Tweaks;
using Henchman.Windows;
using Lumina.Excel.Sheets;
using Module = ECommons.Module;

namespace Henchman;

public class Henchman : IDalamudPlugin
{
    internal static Henchman? P;

    public static readonly HashSet<FeatureUI> FeatureSet = [];

    public readonly Dictionary<string, FontAwesomeIcon> categories = new()
                                                                     {
                                                                             { Category.Combat, FontAwesomeIcon.Khanda },
                                                                             { Category.Exploration, FontAwesomeIcon.Map },
                                                                             { Category.Economy, FontAwesomeIcon.Coins },
                                                                             { Category.System, FontAwesomeIcon.Cog }
                                                                     };

    public readonly WindowSystem  WindowSystem = new("Henchman");
    public          Configuration Config;
    private         MainWindow    mainWindow;
    internal        StatusWindow  StatusWindow;


    public Henchman(IDalamudPluginInterface pluginInterface, ISigScanner sigScanner)
    {
        P = this;
        ECommonsMain.Init(pluginInterface, this, Module.DalamudReflector, Module.ObjectFunctions);

#if LOCAL_CS
        Addresses.Register();
        Resolver.GetInstance.Setup(Svc.SigScanner.SearchBase, Svc.Data.GameData.Repositories["ffxiv"].Version, new FileInfo(Path.Join(pluginInterface.ConfigDirectory.FullName, "SigCache.json")));
        Resolver.GetInstance.Resolve();
#endif

        Initialize();
    }

    public          string        Name => "Henchman";
    internal static Configuration C    => P.Config;

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        mainWindow.Dispose();
        StatusWindow.Dispose();

        CancelAllTasks();
        Wrath.DisableWrath();

#if PRIVATE
        Hooks.UnloadHooks();
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
        AutoCutsceneSkipper.Init(null);
        AutoCutsceneSkipper.Disable();
        EzConfig.Migrate<Configuration>();
        Config               =  EzConfig.Init<Configuration>();
        Svc.Framework.Update += TaskManagerTick;

        Svc.Framework.Update += SubscriptionManager.Subscribe;

#if PRIVATE
        MappingTheRealm.Initialize();
        EzSignatureHelper.Initialize(typeof(Hooks));
#endif

        foreach (var type in GetType()
                            .Assembly.GetTypes()
                            .Where(type => type.GetCustomAttribute<FeatureAttribute>() != null))
        {
            var instance = (FeatureUI)Activator.CreateInstance(type)!;
            FeatureSet.Add(instance);
        }

        EzCmd.Add("/henchman", OnCommand, """
                                          Open plugin window
                                          /henchman BumpOnALog <Class|GC> [RunDuties] → Run current huntlog rank for Class/GC
                                          /henchman OnYourMark → Runs with currently selected HuntBills
                                          /henchman RetainerVocate <1-10> <RetainerClassAbbr> <QuestClassAbbr> <FirstExploration> -> Run retainer creation with selected parameters and random names
                                          /henchman Stop
                                          """);
        EzCmd.Add("/knightman", OnCommand);
        EzCmd.Add("/henchmore", OnCommand);

        mainWindow   = new MainWindow();
        StatusWindow = new StatusWindow();

        WindowSystem.AddWindow(mainWindow);
        WindowSystem.AddWindow(StatusWindow);

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

    // TODO: Finish Commands AND IPCs
    private void OnCommand(string command, string args)
    {
        if (args.StartsWith("BumpOnALog", StringComparison.InvariantCultureIgnoreCase))
        {
            var parameters = args.Split(" ");
            if (parameters.Length == 3 &&
                parameters[1]
                       .EqualsIgnoreCase("GC"))
            {
                if (bool.TryParse(parameters[2], out var runDutyMarks))
                    if (TryGetFeature<BumpOnALogUi>(out var bumpOnALog) && !IsTaskEnqueued(bumpOnALog.Name))
                        EnqueueTask(new TaskRecord(token => bumpOnALog.feature.StartGCRank(token, runDutyMarks), "Bump On A Log - GC Log"));
            }
            else if (parameters.Length == 2 &&
                     parameters[1]
                            .EqualsIgnoreCase("Class"))
            {
                if (TryGetFeature<BumpOnALogUi>(out var bumpOnALog) && !IsTaskEnqueued(bumpOnALog.Name))
                    EnqueueTask(new TaskRecord(bumpOnALog.feature.StartClassRank, "Bump On A Log - Rank Log"));
            }
        }
        else if (args.StartsWith("OnYourMark", StringComparison.InvariantCultureIgnoreCase))
        {
            if (TryGetFeature<BumpOnALogUi>(out var bumpOnALog) && !IsTaskEnqueued(bumpOnALog.Name)) EnqueueTask(new TaskRecord(bumpOnALog.feature.StartClassRank, "Bump On A Log - Rank Log"));
        }
        else if (args.StartsWith("RetainerVocate", StringComparison.InvariantCultureIgnoreCase))
        {
            var parameters = args.Split(" ");
            if (parameters.Length == 5)
            {
                if (uint.TryParse(parameters[1], out var amount))
                {
                    if (Svc.Data.GetExcelSheet<ClassJob>()
                           .FirstOrNull(x => string.Equals(x.Abbreviation.ExtractText(), parameters[2], StringComparison.OrdinalIgnoreCase)) is { RowId: >= 1 and <= 7 or >= 16 and <= 18 or 26 } retainerClass)
                    {
                        if (Svc.Data.GetExcelSheet<ClassJob>()
                               .FirstOrNull(x => string.Equals(x.Abbreviation.ExtractText(), parameters[3], StringComparison.OrdinalIgnoreCase)) is { RowId: >= 1 and <= 7 or >= 19 and <= 42 } questClass)
                        {
                            if (bool.TryParse(parameters[4], out var firstExploration))
                            {
                                if (TryGetFeature<RetainerVocateUI>(out var retainerVocate) && !IsTaskEnqueued(retainerVocate.Name))
                                    EnqueueTask(new TaskRecord(token => retainerVocate.feature.RunFullCreation(token, amount, retainerClass.RowId, questClass.RowId, firstExploration), retainerVocate.Name));
                            }
                        }
                    }
                }
            }
        }
        else if (args.EqualsIgnoreCase("Stop"))
            CancelAllTasks();
        else
            ToggleMainUi();
    }

    private void DrawUi()
    {
        WindowSystem.Draw();
    }

    public void ToggleMainUi()
    {
        mainWindow.Toggle();
    }

    public static bool TryGetFeature<T>(out T result) where T : FeatureUI
    {
        result = FeatureSet.OfType<T>()
                           .FirstOrDefault();
        return result != null;
    }

    public class Category
    {
        public const string Combat      = "Combat";
        public const string Exploration = "Exploration";
        public const string Economy     = "Economy";
        public const string System      = "System";
    }
}
