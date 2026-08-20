#if PRIVATE
using Henchman.Features.Private.Debugging;
using Henchman.Features.Private.Hooking;
#endif
#if LOCAL_CS
using FFXIVClientStructs.Interop.Generated;
using InteropGenerator.Runtime;
#endif
using System.IO;
using System.Linq;
using System.Reflection;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Henchman.Data;
using Henchman.Features.BumpOnALog;
using Henchman.Features.OnABoat;
using Henchman.Features.OnYourMark;
using Henchman.Features.RetainerVocate;
using Henchman.Tweaks;
using Serilog.Events;
using Underlings.Configuration;
using Underlings.Keybinds;
using Underlings.Modules;
using Underlings.TaskManager;
using ClassJob = Lumina.Excel.Sheets.ClassJob;

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

    public readonly Dictionary<Category, FontAwesomeIcon> categories = new()
                                                                       {
                                                                               { Category.Combat, FontAwesomeIcon.Khanda },
                                                                               { Category.Exploration, FontAwesomeIcon.Map },
                                                                               { Category.Economy, FontAwesomeIcon.Coins },
                                                                               { Category.Tweaks, FontAwesomeIcon.SlidersH },
                                                                               { Category.System, FontAwesomeIcon.Cog }
                                                                       };

    public readonly WindowSystem  WindowSystem = new("Henchman");
    public          Configuration Config;
    internal        MainWindow    MainWindow;
    internal        StatusWindow  StatusWindow;

    public Henchman(IDalamudPluginInterface pluginInterface, ISigScanner sigScanner)
    {
        P = this;
        Svc.Init(pluginInterface);
        Chat.Init("Henchman");

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

        MainWindow.Dispose();
        StatusWindow.Dispose();


        CancelAllTasks();
        CombatAutomation.ForceCleanup();
        Wrath.DisableWrath();
        Rendering.SetDisableRenderWhenUnfocused(false);
        Rendering.SetForceRenderEnabled(false);

        foreach (var feature in ModuleRegistry.Instances.OfType<ModuleUI>())
        {
            try
            {
                feature.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to dispose feature {feature.Name} | {ex}");
            }
        }

#if PRIVATE
        Hooks.UnloadHooks();
        Debugging.DisableMaxGC();
#endif
        Svc.Framework.Update -= SubscriptionManager.Subscribe;
        Svc.Framework.Update -= Tick;
        KeybindManager.Dispose();
        Svc.PluginInterface.UiBuilder.Draw       -= DrawUi;
        Svc.PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        AutoRetainer.Dispose();
        IPCProvider.Dispose();
        Svc.Commands.RemoveHandler("/henchman");
        Svc.Commands.RemoveHandler("/knightman");
        Svc.Commands.RemoveHandler("/henchmore");
        Svc.Commands.RemoveHandler("/henchmen");
        Svc.Commands.RemoveHandler("/hench");
        P = null;
    }

    private void Initialize()
    {
        Svc.Log.MinimumLogLevel = LogEventLevel.Verbose;
        HuntDatabase.Initialize();
        PluginConfig.Migrate<Configuration>();
        Config = PluginConfig.Init<Configuration>();
        Loc.Load(MapLanguage(Config.UILanguage));
        OnCancelled   += Vnavmesh.StopCompletely;
        OnTaskStarted += () => StatusWindow.IsOpen = true;

        IPCProvider.Init();
        Svc.Framework.Update += SubscriptionManager.Subscribe;

        foreach (var type in GetType()
                            .Assembly.GetTypes()
                            .Where(type => type.GetCustomAttribute<ModuleAttribute>() != null))
        {
            var instance = (ModuleUI)Activator.CreateInstance(type)!;
            ModuleRegistry.Instances.Add(instance);
        }

        KeybindDiscovery.Discover(GetType()
                                         .Assembly);
        KeybindManager.Initialize();

        Svc.Commands.AddHandler("/henchman", new CommandInfo(OnCommand)
                                             {
                                                     HelpMessage = """
                                                                   Open plugin window
                                                                   /henchman BumpOnALog <Class|GC> [RunDuties] → Run current huntlog rank for Class/GC
                                                                   /henchman OnYourMark → Runs with currently selected HuntBills
                                                                   /henchman RetainerVocate <1-10> <RetainerClassAbbr> <QuestClassAbbr> <FirstExploration> → Run retainer creation with selected parameters and random names
                                                                   /henchman SetupRetainer <Name> <PresetId> → Runs retainer setup for retainer fantasia. Keep presetId and/or name empty to randomize them.
                                                                   /henchman OnABoat → Run On A Boat (also works when you are already on a voyage)
                                                                   /henchman ToggleRender [On|Off] → De-/activate 3D rendering (safes A LOT of GPU load).
                                                                   /henchman Stop
                                                                   """,
                                                     ShowInHelp = true
                                             });
        Svc.Commands.AddHandler("/knightman", new CommandInfo(OnCommand) { ShowInHelp = false });
        Svc.Commands.AddHandler("/henchmore", new CommandInfo(OnCommand) { ShowInHelp = false });
        Svc.Commands.AddHandler("/henchmen", new CommandInfo(OnCommand) { ShowInHelp  = false });
        Svc.Commands.AddHandler("/hench", new CommandInfo(OnCommand) { ShowInHelp     = false });

        MainWindow = new MainWindow(
                                    $"{Name} - {GetType().Assembly.GetName().Version}",
                                    categories.ToDictionary(kv => (Enum)kv.Key, kv => kv.Value),
                                    Path.Combine(Svc.PluginInterface.AssemblyLocation.Directory?.FullName!, "Images", "Henchman.png"),
                                    () =>
                                    {
                                        TextCentered(Theme.ErrorRed, Loc.G("Splash.Attention"));
                                        TextCentered(Loc.G("Splash.AttentionBody"));
                                        ImGui.NewLine();
                                        ImGui.Separator();
                                        ImGui.NewLine();

                                        TextCentered(Theme.ErrorRed, Loc.G("Splash.PositionalData"));
                                        TextCentered(Loc.G("Splash.PositionalDataBody"));
                                    },
                                    () =>
                                    {
                                        ImGui.Text("Plugin by");
                                        ImGui.SameLine(0, 2);
                                        DrawLink("Knightmore", "GitHub", "https://github.com/Knightmore/Henchman");
                                        ImGui.SameLine();
                                        ImGui.Text("•");
                                        ImGui.SameLine();
                                        ImGui.Text("Theme/Design by");
                                        ImGui.SameLine(0, 2);
                                        DrawLink("Wah", "GitHub", "https://github.com/Brappp");
                                    },
                                    () => StatusWindow.IsOpen = !StatusWindow.IsOpen);
        StatusWindow = new StatusWindow("HENCHMAN STATUS", () => P!.MainWindow.IsOpen = true);


        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(StatusWindow);

        Svc.PluginInterface.UiBuilder.Draw       += DrawUi;
        Svc.PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Svc.Framework.Update += Tick;
    }

    [Keybind("Toggle Status Overlay")]
    private static void ToggleStatusOverlay() => P!.StatusWindow.IsOpen = !P!.StatusWindow.IsOpen;

    internal static string MapLanguage(UiLanguage lang) => lang switch
                                                           {
                                                                   UiLanguage.German    => "de",
                                                                   UiLanguage.French    => "fr",
                                                                   UiLanguage.Japanese  => "jp",
                                                                   UiLanguage.Korean    => "ko",
                                                                   UiLanguage.Chinese   => "zh",
                                                                   UiLanguage.Taiwanese => "tw",
                                                                   _                    => "en"
                                                           };

    private static void Tick(object _)
    {
        TextAdvance.Tick();
        YesAlready.Tick();
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
                    if (TryGetFeature<BumpOnALogUI>(out var bumpOnALog) && !IsTaskRunning(bumpOnALog.Name))
                        TryStartTask(new TaskRecord(token => bumpOnALog.Feature.StartGCRank(token, runDutyMarks), "Bump On A Log - GC Log"));
                }
            }
            else if (parameters.Length == 2 &&
                     parameters[1]
                            .EqualsIgnoreCase("Class"))
            {
                if (TryGetFeature<BumpOnALogUI>(out var bumpOnALog) && !IsTaskRunning(bumpOnALog.Name))
                    TryStartTask(new TaskRecord(bumpOnALog.Feature.StartClassRank, "Bump On A Log - Rank Log"));
            }
        }
        else if (args.StartsWith("OnYourMark", StringComparison.InvariantCultureIgnoreCase))
        {
            if (TryGetFeature<OnYourMarkUI>(out var onYourMark) && !IsTaskRunning(onYourMark.Name)) onYourMark.Feature.RunTask();
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
                                if (TryGetFeature<RetainerVocateUI>(out var retainerVocate) && !IsTaskRunning(retainerVocate.Name))
                                    TryStartTask(new TaskRecord(token => retainerVocate.Feature.RunFullCreation(token, amount, retainerClass.RowId, questClass.RowId, firstExploration), retainerVocate.Name));
                            }
                        }
                    }
                }
            }
        }
        else if (args.StartsWith("SetupRetainer", StringComparison.InvariantCultureIgnoreCase))
        {
            var  parameters = args.Split(" ");
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
                        Chat.Warning("Your Preset ID is invalid!");
                        return;
                    }

                    if (TryGetFeature<RetainerVocateUI>(out var retainerVocate) && !IsTaskRunning(retainerVocate.Name))
                        TryStartTask(new TaskRecord(token => retainerVocate.Feature.SetupRetainer(false, presetId, token: token), "Setup Retainer"));
                    break;
                }
                case 2:
                {
                    if (TryGetFeature<RetainerVocateUI>(out var retainerVocate) && !IsTaskRunning(retainerVocate.Name))
                        TryStartTask(new TaskRecord(token => retainerVocate.Feature.SetupRetainer(false, name: parameters[1], token: token), $"Setup {parameters[1]}"));
                    break;
                }
                case 3:
                {
                    var name = parameters[1];

                    if (byte.TryParse(parameters[2], out var presetId))
                    {
                        if (validPresets < presetId)
                        {
                            Chat.Warning("Your Preset ID is invalid!");
                            return;
                        }

                        if (TryGetFeature<RetainerVocateUI>(out var retainerVocate) && !IsTaskRunning(retainerVocate.Name))
                            TryStartTask(new TaskRecord(token => retainerVocate.Feature.SetupRetainer(false, presetId, name, token: token), $"Setup {name}"));
                    }

                    break;
                }
                default:
                {
                    if (TryGetFeature<RetainerVocateUI>(out var retainerVocate) && !IsTaskRunning(retainerVocate.Name))
                        TryStartTask(new TaskRecord(token => retainerVocate.Feature.SetupRetainer(false, token: token), "Setup Retainer"));
                    break;
                }
            }
        }
        else if (args.EqualsIgnoreCase("OnABoat"))
        {
            if (TryGetFeature<OnABoatUI>(out var onABoat) && !IsTaskRunning(onABoat.Name))
                onABoat.Start();
        }
        else if (args.StartsWith("ToggleRender", StringComparison.InvariantCultureIgnoreCase))
        {
            var parameters = args.Split(" ", StringSplitOptions.RemoveEmptyEntries);

            var renderEnabled = parameters.Length == 1
                                        ? Rendering.RenderDisabled
                                        : parameters[1] switch
                                          {
                                                  var s when s.EqualsIgnoreCase("on")  => true,
                                                  var s when s.EqualsIgnoreCase("off") => false,
                                                  _                                    => Rendering.RenderDisabled
                                          };

            Rendering.SetRender(renderEnabled);
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

    public static bool TryGetFeature<T>(out T? result) where T : ModuleUI => ModuleRegistry.TryGet(out result);
}
