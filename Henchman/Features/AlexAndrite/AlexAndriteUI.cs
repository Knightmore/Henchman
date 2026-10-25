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
        DrawCentered("##AlexAndriteStart", () => Layout.DrawButton(() =>
        {
            if (StartButton()) StartTask();
        }));

        if (Svc.ClientState.IsLoggedIn && SaddlebagHelper.GetItemAmountInSaddlebag(AlexAndrite.MapItemId) < 0 && DateTime.UtcNow >= nextSaddlebagRequest && SaddlebagHelper.CanUseSaddlebag())
        {
            nextSaddlebagRequest = DateTime.UtcNow.AddSeconds(5);
            SaddlebagHelper.RequestSaddlebag();
        }

        if (Svc.ClientState.IsLoggedIn)
        {
            var canUseSaddlebag = SaddlebagHelper.CanUseSaddlebag();
            var poetics = AlexAndrite.GetPoetics();
            var inventoryMaps = InventoryHelper.GetInventoryItemCount(AlexAndrite.MapItemId);
            var saddlebagMaps = SaddlebagHelper.GetItemAmountInSaddlebag(AlexAndrite.MapItemId);
            var decipheredMaps = AlexAndrite.GetDecipheredMapCount();
            var affordableMaps = (int)(poetics / AlexAndrite.MapCost);
            var heldMaps = inventoryMaps + Math.Max(0, decipheredMaps) + Math.Max(0, saddlebagMaps);

            DrawStat(AlexAndrite.PoeticsItemId, "Poetics", poetics.ToString("N0"));
            DrawStat(AlexAndrite.MapItemId, "Maps in inventory", inventoryMaps.ToString());
            DrawIcon(Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.EventItem>().GetRow(AlexAndrite.DecipheredMapItemId).Icon, 0.5f);
            ImGui.SameLine();
            ImGui.TextUnformatted($"Deciphered Alexandrite Maps: {(decipheredMaps < 0 ? "loading" : decipheredMaps.ToString())}");
            DrawStat(AlexAndrite.MapItemId, "Maps in saddlebag", saddlebagMaps < 0
                ? canUseSaddlebag ? "loading" : "unavailable"
                : saddlebagMaps.ToString());
            DrawStat(AlexAndrite.MapItemId, $"Maps you can buy ({AlexAndrite.MapCost} poetics each)", affordableMaps.ToString());
            DrawStat(AlexandriteItemId, "Alexandrite from held and active maps", saddlebagMaps < 0 || decipheredMaps < 0
                ? "unknown" : (heldMaps * AlexandritePerMap).ToString());
            DrawStat(AlexandriteItemId, "Alexandrite from held, active, and affordable maps", saddlebagMaps < 0 || decipheredMaps < 0
                ? "unknown" : ((heldMaps + affordableMaps) * AlexandritePerMap).ToString());
            if (Configuration.MapsToFarm > 0)
                DrawStat(AlexandriteItemId, "Alexandrite from map target", ((long)Configuration.MapsToFarm * AlexandritePerMap).ToString());
        }

        var useSaddlebag = Configuration.UseSaddlebag;
        if (ImGui.Checkbox("Use saddlebag for a spare map", ref useSaddlebag))
        {
            Configuration.UseSaddlebag = useSaddlebag;
            SaveConfig(Configuration);
        }

        var mapsToFarm = Configuration.MapsToFarm;
        ImGui.SetNextItemWidth(120f * GlobalFontScale);
        if (ImGui.InputInt("Maps to farm (0 = use current resources)", ref mapsToFarm))
        {
            Configuration.MapsToFarm = Math.Max(0, mapsToFarm);
            SaveConfig(Configuration);
        }

        var farmPoetics = Configuration.FarmPoetics;
        if (ImGui.Checkbox("Farm Poetics with AutoDuty when below 75", ref farmPoetics))
        {
            Configuration.FarmPoetics = farmPoetics;
            SaveConfig(Configuration);
        }

        if (Configuration.FarmPoetics)
        {
            if (Configuration.MapsToFarm == 0) ImGui.TextDisabled("Set a map target to enable Poetics farming.");
            if (!SubscriptionManager.IsLoaded(IPCNames.AutoDuty)) ImGui.TextDisabled("AutoDuty is not loaded.");
            var selectedDuty = (int)Configuration.SelectedDuty;
            if (ImGui.Combo("Duty support duty", ref selectedDuty, ["Castrum Meridianum", "The Praetorium", "The Porta Decumana"], 3))
            {
                Configuration.SelectedDuty = (PoeticsDuty)selectedDuty;
                SaveConfig(Configuration);
            }
        }
    }

    private static void DrawStat(uint itemId, string label, string value)
    {
        DrawIcon(Svc.Data.GetExcelSheet<Lumina.Excel.Sheets.Item>().GetRow(itemId).Icon, 0.5f);
        ImGui.SameLine();
        ImGui.TextUnformatted($"{label}: {value}");
    }
}
