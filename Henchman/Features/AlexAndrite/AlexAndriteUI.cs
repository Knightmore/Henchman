using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Underlings.Keybinds;
using Underlings.Modules;

namespace Henchman.Features.AlexAndrite;

[Module]
internal class AlexAndriteUI : ModuleUI<AlexAndrite, Configuration>
{
    private const uint AlexandriteItemId = 7883;
    private const int AlexandritePerMap = 5;
    private DateTime nextSaddlebagRequest;

    public AlexAndriteUI() => Configuration = LoadConfig<Configuration>() ?? new Configuration();

    public override Configuration Configuration { get; init; }

    public override string Name => "Alex Andrite";
    public override Enum Category => Henchman.Category.Combat;
    public override FontAwesomeIcon Icon => FontAwesomeIcon.MapMarkedAlt;
    public override Action Help => () => DrawRequirements(Requirements);
    public override bool LoginNeeded => false;

    public override string Description => "Buys Alexandrite maps with Poetics and digs them up. Can farm Poetics in duty support first.";

    public override ModuleAction PrimaryAction => new(Loc.G("UI.V2.Start", "Start"), StartTask, "Alex Andrite - Start");

    public override Action? ActionBarInfo => Configuration.FarmPoetics && !SubscriptionManager.IsLoaded(IPCNames.AutoDuty)
                                                     ? () =>
                                                       {
                                                           Widgets.Dot(V2Palette.Warning, 8f);
                                                           ImGui.SameLine(0, 10 * GlobalFontScale);
                                                           Widgets.TextSoft("AutoDuty is not loaded, so Poetics farming is skipped. Map farming still works.");
                                                       }
                                                     : null;

    public override List<(string pluginName, bool mandatory)> Requirements =>
    [
        (IPCNames.vnavmesh, true),
        (IPCNames.Lifestream, true),
        (IPCNames.AutoDuty, false),
        (IPCNames.BossMod, false),
        (IPCNames.Wrath, false),
        (IPCNames.RotationSolverReborn, false)
    ];

    [Keybind("Alex Andrite - Start")]
    private void StartTask()
    {
        if (!IsTaskRunning(Name)) Feature.RunTask();
    }

    public override void Draw()
    {
        if (Svc.ClientState.IsLoggedIn && SaddlebagHelper.GetItemAmountInSaddlebag(AlexAndrite.MapItemId) < 0 && DateTime.UtcNow >= nextSaddlebagRequest && SaddlebagHelper.CanUseSaddlebag())
        {
            nextSaddlebagRequest = DateTime.UtcNow.AddSeconds(5);
            SaddlebagHelper.RequestSaddlebag();
        }

        var notes = Configuration.FarmPoetics
                            ? new Dictionary<string, string> { [IPCNames.AutoDuty] = "Needed for Poetics farming" }
                            : null;

        Widgets.Columns(16f, (0f, () =>
                                  {
                                      DrawStats();
                                      Widgets.Columns(12f, (0f, DrawForecast), (0f, DrawSettings));
                                  }),
                        (262f, () => Widgets.RequirementsCard(this, notes)));
    }

    private void DrawStats()
    {
        if (!Svc.ClientState.IsLoggedIn)
        {
            Widgets.Banner(BannerKind.Info, "Log in to see your Poetics and maps.");
            return;
        }

        var canUseSaddlebag = SaddlebagHelper.CanUseSaddlebag();
        var poetics         = AlexAndrite.GetPoetics();
        var inventoryMaps   = InventoryHelper.GetInventoryItemCount(AlexAndrite.MapItemId);
        var saddlebagMaps   = SaddlebagHelper.GetItemAmountInSaddlebag(AlexAndrite.MapItemId);
        var decipheredMaps  = AlexAndrite.GetDecipheredMapCount();
        var affordableMaps  = (int)(poetics / AlexAndrite.MapCost);

        var mapIcon        = ItemIcon(AlexAndrite.MapItemId);
        var decipheredIcon = (uint)Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.EventItem>().GetRow(AlexAndrite.DecipheredMapItemId).Icon;

        Widgets.Grid(4, 10f,
        [
            width => Widgets.StatCard("Poetics", poetics.ToString("N0"), $"{affordableMaps} maps affordable at {AlexAndrite.MapCost}", ItemIcon(AlexAndrite.PoeticsItemId),
                                      color: V2Palette.Warning, width: width),
            width => Widgets.StatCard("Maps in inventory", inventoryMaps.ToString(), "Not deciphered yet", mapIcon, width: width),
            width => Widgets.StatCard("Deciphered maps", decipheredMaps < 0 ? "loading" : decipheredMaps.ToString(), "Active, ready to dig", decipheredIcon,
                                      color: V2Palette.Blue, width: width),
            width => Widgets.StatCard("Maps in saddlebag", saddlebagMaps < 0
                                                                   ? canUseSaddlebag ? "loading" : "unavailable"
                                                                   : saddlebagMaps.ToString(), "Spare map", mapIcon, width: width)
        ]);
    }

    private void DrawForecast()
    {
        Widgets.Card("Alexandrite forecast", () =>
                                             {
                                                 if (!Svc.ClientState.IsLoggedIn)
                                                 {
                                                     Widgets.TextMuted("Available once you are logged in.", true);
                                                     return;
                                                 }

                                                 var poetics        = AlexAndrite.GetPoetics();
                                                 var inventoryMaps  = InventoryHelper.GetInventoryItemCount(AlexAndrite.MapItemId);
                                                 var saddlebagMaps  = SaddlebagHelper.GetItemAmountInSaddlebag(AlexAndrite.MapItemId);
                                                 var decipheredMaps = AlexAndrite.GetDecipheredMapCount();
                                                 var affordableMaps = (int)(poetics / AlexAndrite.MapCost);
                                                 var heldMaps       = inventoryMaps + Math.Max(0, decipheredMaps) + Math.Max(0, saddlebagMaps);
                                                 var known          = saddlebagMaps >= 0 && decipheredMaps >= 0;

                                                 var held   = (long)heldMaps * AlexandritePerMap;
                                                 var bought = (long)(heldMaps + affordableMaps) * AlexandritePerMap;
                                                 var target = (long)Configuration.MapsToFarm * AlexandritePerMap;
                                                 var max    = Math.Max(1, Math.Max(bought, target));

                                                 ForecastRow("From held and active maps", known ? held.ToString() : "unknown", known ? held / (float)max : 0f, V2Palette.TextSoft);
                                                 ForecastRow("Plus maps you can buy", known ? bought.ToString() : "unknown", known ? bought / (float)max : 0f, V2Palette.Accent);
                                                 if (Configuration.MapsToFarm > 0)
                                                     ForecastRow($"From map target ({Configuration.MapsToFarm} maps)", target.ToString(), target / (float)max, V2Palette.Success);
                                             }, 0f, () => Widgets.TextMuted($"{AlexandritePerMap} per map"));
    }

    private static void ForecastRow(string label, string value, float fraction, Vector4 color)
    {
        Widgets.TextSoft(label);
        ImGui.SameLine();
        Widgets.RightAligned($"##forecast_{label}", () => ImGui.TextUnformatted(value));
        Widgets.ProgressBar(fraction, color, 8f);
    }

    private void DrawSettings()
    {
        Widgets.Card("Run settings", () =>
                                     {
                                         var rowY = ImGui.GetCursorScreenPos().Y;
                                         ImGui.BeginGroup();
                                         ImGui.TextUnformatted("Maps to farm");
                                         Widgets.TextMuted("0 uses your current resources");
                                         ImGui.EndGroup();
                                         ImGui.SameLine();
                                         Widgets.RightAligned("##mapsToFarm", () =>
                                                                              {
                                                                                  var mapsToFarm = Configuration.MapsToFarm;
                                                                                  if (Widgets.Stepper("mapsToFarm", ref mapsToFarm))
                                                                                  {
                                                                                      Configuration.MapsToFarm = Math.Max(0, mapsToFarm);
                                                                                      SaveConfig(Configuration);
                                                                                  }
                                                                              }, rowY);

                                         var useSaddlebag = Configuration.UseSaddlebag;
                                         if (Widgets.SwitchRow("Use saddlebag for a spare map", ref useSaddlebag))
                                         {
                                             Configuration.UseSaddlebag = useSaddlebag;
                                             SaveConfig(Configuration);
                                         }

                                         var farmPoetics = Configuration.FarmPoetics;
                                         if (Widgets.SwitchRow("Farm Poetics with AutoDuty when below 75", ref farmPoetics))
                                         {
                                             Configuration.FarmPoetics = farmPoetics;
                                             SaveConfig(Configuration);
                                         }

                                         if (!Configuration.FarmPoetics) return;

                                         ImGui.Indent(16 * GlobalFontScale);
                                         if (Configuration.MapsToFarm == 0) Widgets.TextMuted("Set a map target to enable Poetics farming.", true);
                                         if (!SubscriptionManager.IsLoaded(IPCNames.AutoDuty)) Widgets.TextColored(V2Palette.Warning, "AutoDuty is not loaded.", true);
                                         Widgets.TextDim("Duty support duty");
                                         var selectedDuty = (int)Configuration.SelectedDuty;
                                         ImGui.SetNextItemWidth(Widgets.Avail);
                                         if (ImGui.Combo("##dutySupportDuty", ref selectedDuty, ["Castrum Meridianum", "The Praetorium", "The Porta Decumana"], 3))
                                         {
                                             Configuration.SelectedDuty = (PoeticsDuty)selectedDuty;
                                             SaveConfig(Configuration);
                                         }

                                         ImGui.Unindent(16 * GlobalFontScale);
                                     });
    }

    private static uint ItemIcon(uint itemId) => Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRow(itemId).Icon;
}
