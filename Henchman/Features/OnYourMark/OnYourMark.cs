using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ECommons.GameHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Henchman.Data;
using Henchman.Models;
using Lumina.Excel.Sheets;
using GrandCompany = ECommons.ExcelServices.GrandCompany;

namespace Henchman.Features.OnYourMark;

internal class OnYourMark
{
    internal async Task Start(CancellationToken token = default)
    {
        var mobHuntOrderType = GetCorrectedMobHuntOrderTypes()
               .ToList();

        if (C.DiscardOldBills)
        {
            foreach (var bill in Svc.Data.GetExcelSheet<MobHuntOrderType>()
                                    .Select(x => x.EventItem))
                await DiscardItem(bill.Value.RowId, token);

            await Task.Delay(GeneralDelayMs * 12, token);
        }
        else
            await ProcessBills(mobHuntOrderType.GetEnumerator(), token);

        await GetNewBills(mobHuntOrderType.GetEnumerator(), token);
        await ProcessBills(mobHuntOrderType.GetEnumerator(), token);
        await Lifestream.LifestreamReturn(C.ReturnTo, C.ReturnOnceDone, token);
    }

    private async Task ProcessBills(List<MobHuntOrderType>.Enumerator mobHuntOrderTypeEnumerator, CancellationToken token = default)
    {
        Verbose("--------------- HUNT BILLs ---------------");
        mobHuntOrderTypeEnumerator.MoveNext();
        byte[] mobHuntOrderTypeOffset;
        unsafe
        {
            mobHuntOrderTypeOffset = MobHunt.Instance()->ObtainedMarkId.ToArray();
        }

        var huntTargets = new List<HuntMark>();

        foreach (var expansion in Svc.Data.GetExcelSheet<ExVersion>())
        {
            Verbose($"############### {expansion.Name.ExtractText()} ###############");
            Verbose($"InExpansion Type: {mobHuntOrderTypeEnumerator.Current.RowId}");

            Verbose($"Current mobHuntTypeOrder: {mobHuntOrderTypeEnumerator.Current.RowId}");
            var enabledBillsSelectString = new List<string>();

            var configExpansionBills = C.EnableHuntBills.Where(x => x.Key.Contains(expansion.Name.ExtractText()));

            Verbose("--------------- BILLS ---------------");
            foreach (var expansionCategory in configExpansionBills)
            {
                var currentMobHuntType = mobHuntOrderTypeEnumerator.Current;

                if (!expansionCategory.Value)
                {
                    mobHuntOrderTypeEnumerator.MoveNext();
                    Verbose("--------------- SKIP BILL ---------------");
                    continue;
                }

                bool isMarkBillObtained;
                unsafe
                {
                    isMarkBillObtained = MobHunt.Instance()->IsMarkBillObtained((int)currentMobHuntType.RowId);
                }
                
                if (!isMarkBillObtained)
                {
                    mobHuntOrderTypeEnumerator.MoveNext();
                    continue;
                }

                var mobHuntTargets = Svc.Data.GetSubrowExcelSheet<MobHuntOrder>()[Svc.Data.GetExcelSheet<MobHuntOrderType>()
                                                                                     .GetRow(currentMobHuntType.RowId)
                                                                                     .OrderStart.Value.RowId +
                                                                                  ((uint)mobHuntOrderTypeOffset[currentMobHuntType.RowId] - 1)];

                bool allMobsKilled;
                unsafe
                {
                    allMobsKilled = mobHuntTargets.All(x => MobHunt.Instance()->GetKillCount((byte)currentMobHuntType.RowId, (byte)x.SubrowId) == x.NeededKills);
                }

                if (!allMobsKilled)
                {
                    enabledBillsSelectString.Add(expansionCategory.Key);
                    Verbose($"Adding: {HuntBoardSelect[expansionCategory.Key]}");
                    foreach (var mob in mobHuntTargets)
                    {
                        Verbose($"Mob: {mob.Target.Value.Name.Value.RowId} {mob.Target.Value.Name.Value.Singular.ExtractText()}");
                        if (HuntMarks.TryGetValue(mob.Target.Value.Name.Value.RowId, out var tempMark))
                        {
                            tempMark.NeededKills     = mob.NeededKills;
                            tempMark.MobHuntRowId    = (byte)currentMobHuntType.RowId;
                            tempMark.MobHuntSubRowId = (byte)mob.SubrowId;
                            Verbose($"Open Kills: {tempMark.GetOpenMobHuntKills.ToString()}");
                            if (tempMark.GetOpenMobHuntKills == 0)
                            {
                                continue;
                            }


                            huntTargets.Add(tempMark);
                            Verbose($"Adding: {tempMark.Name}");
                        }
                    }
                }

                mobHuntOrderTypeEnumerator.MoveNext();
                Verbose("--------------- NEXT BILL ---------------");
            }

            Verbose($"Enabled Bill Count: {enabledBillsSelectString.Count()}");
        }

        var orderedMarks = huntTargets.OrderBy(x => x.FateId)
                                      .ThenBy(x => x.TerritoryId)
                                      .ToList();
        await ProcessHuntMarks(orderedMarks, token: token);
    }

    private async Task GetNewBills(List<MobHuntOrderType>.Enumerator mobHuntOrderTypeEnumerator, CancellationToken token = default)
    {
        mobHuntOrderTypeEnumerator.MoveNext();
        byte[] mobHuntOrderTypeOffset;
        unsafe
        {
            mobHuntOrderTypeOffset = MobHunt.Instance()->AvailableMarkId.ToArray();
        }

        foreach (var expansion in Svc.Data.GetExcelSheet<ExVersion>())
        {
            Verbose($"############### {expansion.Name.ExtractText()} ###############");
            var enabledBillsSelectString = new List<string>();

            var configExpansionBills = C.EnableHuntBills.Where(x => x.Key.Contains(expansion.Name.ExtractText()));

            Verbose("--------------- BILLS ---------------");
            foreach (var expansionCategory in configExpansionBills)
            {
                if (!expansionCategory.Value)
                {
                    mobHuntOrderTypeEnumerator.MoveNext();
                    Verbose("--------------- SKIP TO NEXT BILL ---------------");
                    continue;
                }

                var  currentMobHuntType = mobHuntOrderTypeEnumerator.Current;
                bool isMarkBillUnlocked;
                bool isMarkBillObtained;
                int  availableMarkId;
                int  obtainedMarkId;

                unsafe
                {
                    isMarkBillUnlocked = MobHunt.Instance()->IsMarkBillUnlocked((byte)currentMobHuntType.RowId);
                    isMarkBillObtained = MobHunt.Instance()->IsMarkBillObtained((int)currentMobHuntType.RowId);
                    availableMarkId    = MobHunt.Instance()->GetAvailableHuntOrderRowId((byte)currentMobHuntType.RowId);
                    obtainedMarkId     = MobHunt.Instance()->GetObtainedHuntOrderRowId((byte)currentMobHuntType.RowId);
                }

                if (!isMarkBillUnlocked)
                    continue;

                var mobHuntTargets = Svc.Data.GetSubrowExcelSheet<MobHuntOrder>()[Svc.Data.GetExcelSheet<MobHuntOrderType>()
                                                                                     .GetRow(currentMobHuntType.RowId)
                                                                                     .OrderStart.Value.RowId +
                                                                                  ((uint)mobHuntOrderTypeOffset[currentMobHuntType.RowId] - 1)];

                bool allMobsKilled;
                unsafe
                {
                    allMobsKilled = mobHuntTargets.All(x => MobHunt.Instance()->GetKillCount((byte)currentMobHuntType.RowId, (byte)x.SubrowId) == x.NeededKills);
                }

                if ((availableMarkId != obtainedMarkId && !isMarkBillObtained) || (!isMarkBillObtained && !allMobsKilled))
                {
                    enabledBillsSelectString.Add(expansionCategory.Key);
                    Verbose($"Adding: {HuntBoardSelect[expansionCategory.Key]}");
                }

                mobHuntOrderTypeEnumerator.MoveNext();
                Verbose("--------------- NEXT BILL ---------------");
            }

            Verbose($"EnabledBillSelectString Count: {enabledBillsSelectString.Count()}");
            if (enabledBillsSelectString.Count == 0) continue;


            Location location;
            unsafe
            {
                location = expansion.Name.ExtractText() == "A Realm Reborn"
                                   ? ArrHuntBoardLocations[(HuntDatabase.GrandCompany)PlayerState.Instance()->GrandCompany]
                                   : ExpansionHuntBoardLocations[expansion.Name.ExtractText()];
            }

            await GoToHuntboard(location, expansion.Name.ExtractText(), enabledBillsSelectString, token);
        }
    }

    private async Task GoToHuntboard(Location location, string expansion, List<string> billsSelectString, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (Player.Territory != location.TerritoryId)
        {
            if (expansion == "A Realm Reborn" && Player.GrandCompany == GrandCompany.Maelstrom)
            {
                Lifestream.ExecuteCommand("gc");
                await WaitPulseConditionAsync(() => Lifestream.IsBusy(), "Moving To GC", token);
            }
            else
            {
                Verbose($"Teleport to: {location.Position} - ({location.TerritoryId}): {location.TerritoryType.Name.ExtractText()}");
                TeleportInfo? huntboardTerritory;
                unsafe
                {
                    Telepo.Instance()->UpdateAetheryteList();
                    huntboardTerritory = Telepo.Instance()->TeleportList.FirstOrNull(x => x.TerritoryId == location.TerritoryId);
                }

                ErrorThrowIf(huntboardTerritory == null, "No aetheryte for huntboard found!");

                var aetheryteId = huntboardTerritory!.Value.AetheryteId;
                Verbose($"AetheryteId: {aetheryteId}");
                await TeleportTo(aetheryteId, token);
                if (AethernetIdCloseToHuntboard.TryGetValue(expansion, out var aethernetId))
                {
                    Lifestream.AethernetTeleportById(aethernetId);
                    await WaitPulseConditionAsync(() => Lifestream.IsBusy(), "Waiting for teleport", token);
                }
            }
        }

        uint huntBoardId;
        if (expansion == "A Realm Reborn")
        {
            huntBoardId = Player.GrandCompany switch
                          {
                                  GrandCompany.Maelstrom      => GCHuntBoardIds[HuntDatabase.GrandCompany.Maelstrom],
                                  GrandCompany.TwinAdder      => GCHuntBoardIds[HuntDatabase.GrandCompany.OrderOfTheTwinAdder],
                                  GrandCompany.ImmortalFlames => GCHuntBoardIds[HuntDatabase.GrandCompany.ImmortalFlames]
                          };
        }
        else
            huntBoardId = HuntBoardIds[expansion];

        var mobhuntNum = Svc.Data.GetExcelSheet<ExVersion>()
                            .First(x => x.Name.GetText() == expansion)
                            .RowId +
                         1;
        Verbose($"Go To: {location.Position} - ({location.TerritoryId}): BaseId: {huntBoardId}");
        await MoveToStationaryObject(location.Position, huntBoardId, token: token);
        foreach (var bill in billsSelectString)
        {
            var billSelect = HuntBoardSelect[bill];
            await GatherHuntBills(huntBoardId, billSelect, mobhuntNum == 1
                                                                   ? ""
                                                                   : mobhuntNum.ToString(), token);
        }
    }

    private async Task GatherHuntBills(uint huntBoardId, string billSelect, string num, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        await InteractWithByBaseId(huntBoardId, token);
        await WaitUntilAsync(() => Svc.Targets.Target != null && Svc.Targets.Target.BaseId == huntBoardId, "Waiting for Huntboard Target", token);
        await WaitUntilAsync(() => TrySelectSpecificEntry(billSelect), $"SelectString {billSelect}", token);
        await WaitUntilAsync(() => ClickAddonButton($"Mobhunt{num}", 21), "Click Accept", token);
        await Task.Delay(GeneralDelayMs * 4, token)
                  .ConfigureAwait(true);
    }
}
