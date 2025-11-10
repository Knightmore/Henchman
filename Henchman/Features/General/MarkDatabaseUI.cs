using System.Linq;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons.GameHelpers;
using Henchman.Helpers;
using Henchman.Models;

namespace Henchman.Features.General;

[Feature]
internal class MarkDatabaseUI : FeatureUI
{
    public override string          Name        => "Mark Database";
    public override string          Category    => Henchman.Category.System;
    public override FontAwesomeIcon Icon        => FontAwesomeIcon.Database;
    public override Action?         Help        { get; }
    public override bool            LoginNeeded => true;

    public override void Draw()
    {
        ImGui.Text("Current Territory:");
        ImGui.SameLine(150);
        ImGui.Text($"{(Player.Available ? Player.Territory : 0)}");
        var currentTarget = Player.Available && Svc.Targets.Target is IBattleNpc targetNpc
                                    ? targetNpc
                                    : null;
        ImGui.Text("Current Target Id:");
        ImGui.SameLine(150);
        ImGui.Text($"{currentTarget?.NameId ?? 0}");
        ImGui.Text("Current Target Name:");
        ImGui.SameLine(150);
        ImGui.Text($"{(currentTarget != null ? currentTarget.Name.TextValue : "None")}");
        ImGui.Text("Current Position:");
        ImGui.SameLine(150);
        ImGui.Text($"{(Player.Available ? Player.Position : default)}");

        ImGui.Separator();

        var flatList = HuntMarks.Values.Where(x => x.TerritoryId == Player.Territory)
                                .SelectMany(x => x.Positions
                                                  .Where(y => Player.DistanceTo(y) <= 70 && currentTarget is IBattleNpc battleNpc && battleNpc.NameId == x.BNpcNameRowId)
                                                  .Select(pos => (Id: x.BNpcNameRowId, Name: Utils.ToTitleCaseExtended(x.BNpcNameSheet.Singular, Svc.ClientState.ClientLanguage), Position: pos)))
                                .ToList();

        if (ImGui.Button("Copy new area marker (current position)"))
        {
            if (currentTarget is IBattleNpc battleNpc && flatList.All(x => x.Id != battleNpc.NameId) && Player.Territory > 0)
            {
                var newPosition = new JsonHuntMark
                                  {
                                          BnpcName    = battleNpc.NameId,
                                          FateId      = 0,
                                          TerritoryId = Player.Territory,
                                          X           = Player.Position.X,
                                          Y           = Player.Position.Y,
                                          Z           = Player.Position.Z
                                  };

                var options = new JsonSerializerOptions { WriteIndented = true };
                ImGui.SetClipboardText(JsonSerializer.Serialize(newPosition, options));
                ChatPrintInfo("Copied positional data to clipboard!");
            }
        }

        if (flatList.Any())
        {
            ImGui.Text("Overlapping mark areas:");
            foreach (var mark in flatList)
            {
                ImGui.Text($"Id: {mark.Id}");
                ImGui.SameLine(80);
                ImGui.Text($"Name: {mark.Name}");
                ImGui.SameLine(350);
                ImGui.Text($"Area center: {mark.Position}");
            }
        }

        ImGui.Separator();

        using (var table = ImRaii.Table("###markTable", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY))
        {
            ImGui.TableSetupColumn("Id", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Position", ImGuiTableColumnFlags.WidthFixed);
            ImGui.TableHeadersRow();

            foreach (var mark in HuntMarks.Values.Where(x => x.TerritoryId == Player.Territory))
            {
                foreach (var position in mark.Positions)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text(mark.BNpcNameRowId.ToString());
                    ImGui.TableNextColumn();
                    ImGui.Text(Utils.ToTitleCaseExtended(mark.BNpcNameSheet.Singular, Svc.ClientState.ClientLanguage));
                    ImGui.TableNextColumn();
                    ImGui.Text(position.ToString());
                }
            }
        }
    }
}
