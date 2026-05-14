using System.Threading;
using System.Threading.Tasks;
using ECommons.GameHelpers;
using Henchman.Multiboxing.Command;
using GrandCompany = ECommons.ExcelServices.GrandCompany;

namespace Henchman.Multiboxing.RPC;

[CommandGroup]
internal class QuestingRPC
{
    [Command]
    internal static async Task<bool> UnlockDuty(uint dutyId, CancellationToken token = default)
    {
        switch (dutyId)
        {
            case 1245 when !IsDutyUnlocked(1245): // Halatali
                await Questionable.CompleteQuest(66233, token);
                break;
            case 1267 when !IsDutyUnlocked(1267): // Qarn
                await Questionable.CompleteQuest(66300, token);
                break;
            case 1303 when !IsDutyUnlocked(1303): // Cutter's Cry
                await Questionable.CompleteQuest(66457, token);
                break;
            case 1330 when !IsDutyUnlocked(1330): // Dzemael
                if (IsQuestAccepted(66515))
                    AbandonQuest(66515);
                await Questionable.CompleteQuest(Player.GrandCompany == GrandCompany.Maelstrom ? 66664u : Player.GrandCompany == GrandCompany.TwinAdder ? 66665u : 66666u, token);
                break;
            case 1331 when !IsDutyUnlocked(1331): // Aurum Vale
                if (IsQuestAccepted(66550))
                    AbandonQuest(66550);
                await Questionable.CompleteQuest(Player.GrandCompany == GrandCompany.Maelstrom ? 66667u : Player.GrandCompany == GrandCompany.TwinAdder ? 66668u : 66669u, token);
                break;
        }

        return true;
    }
}
