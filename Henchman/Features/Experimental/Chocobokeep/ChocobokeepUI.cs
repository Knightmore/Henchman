using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Henchman.Helpers;
using Henchman.Models;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Henchman.TaskManager;
using Action = System.Action;

namespace Henchman.Features.Experimental.Chocobokeep
{
    [Experimental]
    internal class ChocobokeepUI : FeatureUI
    {
        private readonly Chocobokeep feature = new();
        public override  string     Name            => "Chocobokeep";
        public override Action Help => () =>
                                       {
                                           ImGui.Text("""
                                                      This only exists because a certain completionist *cough* Kawaii *cough* asked for a Chocobokeep Unlocker.
                                                      Hit "Start" and it will unlock all missing ChocoboTaxiStands.
                                                      """
                                                      );


                                           ImGuiHelper.DrawRequirements(Requirements);
                                       };
        public override bool                         LoginNeeded     => false;
        public override Window.WindowSizeConstraints SizeConstraints { get; } = new Window.WindowSizeConstraints
                                                                                {
                                                                                        MinimumSize = new Vector2(400, 500),
                                                                                        MaximumSize = new Vector2(400, 500)
                                                                                };

        public override List<(string pluginName, bool mandatory)> Requirements =>
        [
                (IPCNames.vnavmesh, true),
                (IPCNames.Lifestream, true),
                (IPCNames.BossMod, false),
                (IPCNames.Wrath, false),
                (IPCNames.RotationSolverReborn, false)
        ];

        private bool HideUnlocked = false;
        public override unsafe void    Draw()
        {
            ImGuiEx.LineCentered("###Start", () =>
                                             {
                                                 if (ImGui.Button("Start") && !IsTaskEnqueued(Name))
                                                     EnqueueTask(new TaskRecord(feature.Start, Name));
                                             });
            ImGuiEx.LineCentered("###HideUnlocked", () =>
                                             {
                                                 ImGui.Checkbox("Hide Unlocked", ref HideUnlocked);
                                             });
            using (var table = ImRaii.Table("###KeepsTable", 3, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY))
            {
                ImGui.TableSetupColumn("KeepId", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Territory", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Unlocked", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableHeadersRow();
                foreach (var keep in HideUnlocked ? feature.keeps.Where(x => !UIState.Instance()->IsChocoboTaxiStandUnlocked(x.ChocoboTaxiStandId)) : feature.keeps)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGuiEx.Text(keep.ChocoboTaxiStandId.ToString());
                    ImGui.TableNextColumn();
                    ImGuiEx.Text(Svc.Data.GetExcelSheet<TerritoryType>().GetRow(keep.TerritoryId).PlaceName.Value.Name.ExtractText());
                    ImGui.TableNextColumn();
                    unsafe
                    {
                        var uiState = UIState.Instance();
                        var isUnlocked = uiState->IsChocoboTaxiStandUnlocked(keep.ChocoboTaxiStandId);
                        ImGuiEx.Text(isUnlocked.ToString());
                    }

                }
            }
        }

        public class ChocobokeepData
        {
            public uint Id                 { get; set; }
            public uint ChocoboTaxiStandId { get; set; }
            public uint TerritoryId        { get; set; }

            [JsonIgnore]
            public Vector3 Location { get; set; }

            [JsonPropertyName("Location")]
            public Vector3Dto LocationDto
            {
                get => new Vector3Dto(Location);
                set => Location = new Vector3(value.X, value.Y, value.Z);
            }
        }

        public class Vector3Dto
        {
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }

            public Vector3Dto() { }

            public Vector3Dto(Vector3 v)
            {
                X = v.X;
                Y = v.Y;
                Z = v.Z;
            }
        }
    }
}
