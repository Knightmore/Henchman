#if PRIVATE
using Henchman.Features.Private.Hooking;
#endif
#if LOCAL_CS
using FFXIVClientStructs.Interop.Generated;
using InteropGenerator.Runtime;
using System.IO;
#endif
using Dalamud.Game;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using ECommons.Automation;
using ECommons.Configuration;
using ECommons.EzIpcManager;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Henchman.Abstractions;
using Henchman.Data;
using Henchman.Features.BumpOnALog;
using Henchman.Features.OnABoat;
using Henchman.Features.OnYourMark;
using Henchman.Features.RetainerVocate;
using Henchman.Helpers;
using Henchman.TaskManager;
using Henchman.Tweaks;
using Henchman.Windows;
using Lumina.Excel.Sheets;
using System.Linq;
using System.Reflection;
using ECommons.EzHookManager;
using Module = ECommons.Module;

namespace Henchman;

public class Henchman : IDalamudPlugin
{
    public enum Category
    {
        Combat,
        Exploration,
        Economy,
        Tweaks,
        System
    }

    internal static Henchman? P;

    public static readonly HashSet<FeatureUI> FeatureSet = [];

    public readonly Dictionary<Category, FontAwesomeIcon> categories = new()
                                                                       {
                                                                               { Category.Combat, FontAwesomeIcon.Khanda },
                                                                               { Category.Exploration, FontAwesomeIcon.Map },
                                                                               { Category.Economy, FontAwesomeIcon.Coins },
                                                                               { Category.Tweaks, FontAwesomeIcon.SlidersH },
                                                                               { Category.System, FontAwesomeIcon.Cog }
                                                                       };

    public readonly WindowSystem WindowSystem = new("Henchman");
    public Configuration Config;
    internal MainWindow MainWindow;
    internal StatusWindow StatusWindow;

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

    public string Name => "Henchman";
    internal static Configuration C => P.Config;

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        MainWindow.Dispose();
        StatusWindow.Dispose();


        CancelAllTasks();
        Wrath.DisableWrath();

        foreach (var feature in FeatureSet)
        {
            try
            {
                feature.Dispose();
            }
            catch (Exception ex)
            {
                InternalError($"Failed to dispose feature {feature.Name} | {ex}");
            }
        }

#if PRIVATE
        Hooks.UnloadHooks();
#endif
        Svc.Framework.Update -= TaskManagerTick;
        Svc.Framework.Update -= SubscriptionManager.Subscribe;
        Svc.Framework.Update -= Tick;
        Svc.PluginInterface.UiBuilder.Draw -= DrawUi;
        Svc.PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        ECommonsMain.Dispose();
        P = null;
    }

    private void Initialize()
    {
        HuntDatabase.Initialize();
        AutoCutsceneSkipper.Init(null);
        AutoCutsceneSkipper.Disable();
        EzConfig.Migrate<Configuration>();
        Config = EzConfig.Init<Configuration>();
        Loc.Load(MapLanguage(Config.UILanguage));
        Svc.Framework.Update += TaskManagerTick;

        EzIPC.Init(typeof(IPCProvider), "Henchman");
        Svc.Framework.Update += SubscriptionManager.Subscribe;

#if PRIVATE
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
                                          /henchman RetainerVocate <1-10> <RetainerClassAbbr> <QuestClassAbbr> <FirstExploration> → Run retainer creation with selected parameters and random names
                                          /henchman SetupRetainer <Name> <PresetId> → Runs retainer setup for retainer fantasia. Keep presetId and/or name empty to randomize them.
                                          /henchman OnABoat → Run On A Boat (also works when you are already on a voyage)
                                          /henchman ToggleRender [On|Off] → De-/activate 3D rendering (saves A LOT of GPU load).
                                          /henchman Stop
                                          """);
        EzCmd.Add("/knightman", OnCommand);
        EzCmd.Add("/henchmore", OnCommand);

        MainWindow = new MainWindow();
        StatusWindow = new StatusWindow();


        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(StatusWindow);

        Svc.PluginInterface.UiBuilder.Draw += DrawUi;
        Svc.PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Svc.Framework.Update += Tick;
    }

    internal static string MapLanguage(ClientLanguage lang) => lang switch
    {
        ClientLanguage.German => "de",
        ClientLanguage.French => "fr",
        ClientLanguage.Japanese => "jp",
        _ => "en"
    };

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
                {
                    if (TryGetFeature<BumpOnALogUI>(out var bumpOnALog) && !IsTaskEnqueued(bumpOnALog.Name))
                        EnqueueTask(new TaskRecord(token => bumpOnALog.Feature.StartGCRank(token, runDutyMarks), "Bump On A Log - GC Log"));
                }
            }
            else if (parameters.Length == 2 &&
                     parameters[1]
                            .EqualsIgnoreCase("Class"))
            {
                if (TryGetFeature<BumpOnALogUI>(out var bumpOnALog) && !IsTaskEnqueued(bumpOnALog.Name))
                    EnqueueTask(new TaskRecord(bumpOnALog.Feature.StartClassRank, "Bump On A Log - Rank Log"));
            }
        }
        else if (args.StartsWith("OnYourMark", StringComparison.InvariantCultureIgnoreCase))
        {
            if (TryGetFeature<OnYourMarkUI>(out var onYourMark) && !IsTaskEnqueued(onYourMark.Name)) EnqueueTask(new TaskRecord(onYourMark.Feature.Start, "On Your Mark"));
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
                                    EnqueueTask(new TaskRecord(token => retainerVocate.Feature.RunFullCreation(token, amount, retainerClass.RowId, questClass.RowId, firstExploration), retainerVocate.Name));
                            }
                        }
                    }
                }
            }
        }
        else if (args.StartsWith("SetupRetainer", StringComparison.InvariantCultureIgnoreCase))
        {
            var parameters = args.Split(" ");
            uint validPresets;
            unsafe
            {
                validPresets = Framework.Instance()->CharamakeAvatarSaveData->Release.GetValidSlotCount();
            }

            if (!Svc.Condition[ConditionFlag.CreatingCharacter]) return;

            switch (parameters.Length)
            {
                case 2 when byte.TryParse(parameters[1], out var presetId):
                    {
                        if (validPresets < presetId)
                        {
                            ChatPrintWarning("Your Preset ID is invalid!");
                            return;
                        }

                        if (TryGetFeature<RetainerVocateUI>(out var retainerVocate) && !IsTaskEnqueued(retainerVocate.Name))
                            EnqueueTask(new TaskRecord(token => retainerVocate.Feature.SetupRetainer(false, presetId, token: token), "Setup Retainer"));
                        break;
                    }
                case 2:
                    {
                        if (TryGetFeature<RetainerVocateUI>(out var retainerVocate) && !IsTaskEnqueued(retainerVocate.Name))
                            EnqueueTask(new TaskRecord(token => retainerVocate.Feature.SetupRetainer(false, name: parameters[1], token: token), $"Setup {parameters[1]}"));
                        break;
                    }
                case 3:
                    {
                        var name = parameters[1];

                        if (byte.TryParse(parameters[2], out var presetId))
                        {
                            if (validPresets < presetId)
                            {
                                ChatPrintWarning("Your Preset ID is invalid!");
                                return;
                            }

                            if (TryGetFeature<RetainerVocateUI>(out var retainerVocate) && !IsTaskEnqueued(retainerVocate.Name))
                                EnqueueTask(new TaskRecord(token => retainerVocate.Feature.SetupRetainer(false, presetId, name, token: token), $"Setup {name}"));
                        }

                        break;
                    }
                default:
                    {
                        if (TryGetFeature<RetainerVocateUI>(out var retainerVocate) && !IsTaskEnqueued(retainerVocate.Name))
                            EnqueueTask(new TaskRecord(token => retainerVocate.Feature.SetupRetainer(false, token: token), "Setup Retainer"));
                        break;
                    }
            }
        }
        else if (args.EqualsIgnoreCase("OnABoat"))
        {
            if (TryGetFeature<OnABoatUI>(out var onABoat) && !IsTaskEnqueued(onABoat.Name))
                onABoat.Start();
        }
        else if (args.EqualsIgnoreCase("ToggleRender"))
        {
            var parameters = args.Split(" ");

            GeneralTweaks.ActiveRenderFlag =
                    (byte)(parameters.Length == 1
                                   ? GeneralTweaks.ActiveRenderFlag ^ 1
                                   : parameters[1] switch
                                   {
                                       var s when s.EqualsIgnoreCase("on") => 0,
                                       var s when s.EqualsIgnoreCase("off") => 1,
                                       _ => GeneralTweaks.ActiveRenderFlag ^ 1
                                   });
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
        MainWindow.Toggle();
    }

    public static bool TryGetFeature<T>(out T? result) where T : FeatureUI
    {
        result = FeatureSet.OfType<T>()
                           .FirstOrDefault();
        return result != null;
    }
}
