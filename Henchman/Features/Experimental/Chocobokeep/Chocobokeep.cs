using Henchman.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using static Henchman.Features.Experimental.Chocobokeep.ChocobokeepUI;

namespace Henchman.Features.Experimental.Chocobokeep
{
    internal class Chocobokeep
    {
        internal List<ChocobokeepData>? keeps = Utils.ReadLocalJsonFile<List<ChocobokeepData>>("ChocoboTaxiStands.json");

        internal async Task Start(CancellationToken token = default)
        {
            List<ChocobokeepData> lockedKeeps;
            unsafe
            {
                var uiState    = UIState.Instance();
                lockedKeeps = keeps.Where(x => !uiState->IsChocoboTaxiStandUnlocked(x.ChocoboTaxiStandId)).ToList();
            }

            foreach (var keep in lockedKeeps)
            {
                await UnlockChocobokeep(keep, token);
            }
        }

        internal async Task UnlockChocobokeep(ChocobokeepData keep, CancellationToken token = default)
        {
            var closestAetheryte = GetAetheryte(keep.TerritoryId, keep.Location);
            if (closestAetheryte > 0 && !IsAetheryteUnlocked(closestAetheryte))
            {
                FullWarning($"You are not attuned to Aetheryte {closestAetheryte} in {Svc.Data.GetExcelSheet<TerritoryType>().GetRow(keep.TerritoryId).PlaceName}");
            }
            else if (closestAetheryte == 0)
            {
                FullWarning($"There is no Aetheryte in {Svc.Data.GetExcelSheet<TerritoryType>().GetRow(keep.TerritoryId).PlaceName}. But a Reroute hasn't implemented yet!");
            }
            else
            {
                await TeleportTo(closestAetheryte, token);
                await MoveToStationaryObject(keep.Location, keep.Id, true, 2f, token);
                await InteractWithByDataId(keep.Id, token);
                await WaitUntilAsync(() => TrySelectSpecificEntry(Svc.Data.GetExcelSheet<Addon>()
                                                                     .GetRow(622)
                                                                     .Text.ExtractText()), "SelectString 'Nothing'", token);
            }
        }
    }
}
