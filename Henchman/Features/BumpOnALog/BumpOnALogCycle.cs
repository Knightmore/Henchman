using System.Threading;
using System.Threading.Tasks;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Henchman.Data;
using Lumina.Excel.Sheets;
using Underlings.GameHelpers;
using Underlings.TaskManager;

namespace Henchman.Features.BumpOnALog;

public partial class BumpOnALog
{
    private (bool Suppressed, bool MultiMode)? cycleAutoRetainerState;

    internal async Task RunCharacterCycle(IReadOnlyList<LogCycleCharacter> characters, bool gcLog, CancellationToken token)
    {
        ErrorThrowIf(!SubscriptionManager.IsLoaded(IPCNames.AutoRetainer), "AutoRetainer is required for character cycling.");
        ErrorThrowIf(!SubscriptionManager.IsLoaded(IPCNames.Lifestream), "Lifestream is required for character cycling.");
        ErrorThrowIf(AutoRetainer.IsBusy.Invoke() || Lifestream.IsBusy.Invoke(), "Wait for AutoRetainer and Lifestream to finish before cycling.");
        ErrorThrowIf(!AutoRotation.CheckForAvailability(C.AutoRotationPlugin), "A combat rotation plugin is required for character cycling.");
        token.ThrowIfCancellationRequested();
        cycleAutoRetainerState = (AutoRetainer.GetSuppressed.Invoke(), AutoRetainer.GetMultiModeEnabled.Invoke());
        try
        {
            AutoRetainer.SetSuppressed.Invoke(true);
            AutoRetainer.SetMultiModeEnabled.Invoke(false);
            for (var i = 0; i < characters.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                var character = characters[i];
                using var scope = new TaskDescriptionScope($"{(gcLog ? "GC" : "Class")} log cycle: {i + 1}/{characters.Count} — {character.Name}@{character.World}");
                try
                {
                    await Lifestream.SwitchToChar(character.Name, character.World, Lang.SelectYesNoLogout, token);
                    await WaitUntilAsync(() => Player.Available && Player.CID == character.CID && IsScreenAndPlayerReady() && !Player.IsBusy,
                                         "Waiting for the selected character", token, TimeSpan.FromMinutes(2));
                    token.ThrowIfCancellationRequested();
                    if (character.GearsetId is { } gearsetId)
                    {
                        var classJob = EquipCycleGearset(character, gearsetId);
                        await Task.Delay(GeneralDelayMs, token);
                        await WaitUntilAsync(() =>
                        {
                            token.ThrowIfCancellationRequested();
                            if (ConfirmCycleGearset()) return false;
                            return IsCycleGearsetEquipped(character, classJob);
                        }, "Equipping selected gearset", token, TimeSpan.FromSeconds(15));
                        await EquipRecommendedGear(character, classJob, token);
                    }
                    await Process(gcLog, token: token);
                }
                finally
                {
                    CleanupCombatAutomation();
                }
            }
        }
        finally
        {
            CleanupCycle();
        }
    }

    private static unsafe bool ConfirmCycleGearset()
    {
        if (!TryGetAddonByName<AddonSelectYesno>("SelectYesno", out var addon) || !IsAddonReady(&addon->AtkUnitBase)) return false;
        var text = MemoryHelper.ReadSeStringNullTerminated((nint)(byte*)addon->PromptText->NodeText.StringPtr).TextValue.NormalizeWhitespaces();
        foreach (var rowId in new uint[] { 4384, 4385, 4388 })
        {
            if (!Svc.Data.GetExcelSheet<Addon>().GetRow(rowId).Text.ToRegex().IsMatch(text)) continue;
            if (!addon->YesButton->IsEnabled) return true;
            (&addon->AtkUnitBase)->FireCallback(true, 0);
            return true;
        }
        return false;
    }

    private static async Task EquipRecommendedGear(LogCycleCharacter character, byte classJob, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        unsafe
        {
            var module = RecommendEquipModule.Instance();
            ErrorThrowIf(module == null || !module->SetupForClassJob(classJob), "Could not calculate recommended gear.");
        }
        await WaitUntilAsync(() =>
        {
            unsafe
            {
                var module = RecommendEquipModule.Instance();
                return module != null && !module->IsUpdating && module->ClassJob == classJob;
            }
        }, "Calculating recommended gear", token, TimeSpan.FromSeconds(15));
        token.ThrowIfCancellationRequested();
        ErrorThrowIf(!IsCycleGearsetEquipped(character, classJob), "Character or gearset changed while calculating recommended gear.");
        unsafe { RecommendEquipModule.Instance()->EquipRecommendedGear(); }
        await Task.Delay(GeneralDelayMs, token);
        await WaitUntilAsync(() => Player.Available && Player.CID == character.CID && IsScreenAndPlayerReady() && !Player.IsBusy,
                             "Equipping recommended gear", token, TimeSpan.FromSeconds(15));
    }

    private static unsafe byte EquipCycleGearset(LogCycleCharacter character, byte gearsetId)
    {
        var module = RaptureGearsetModule.Instance();
        ErrorThrowIf(module == null || module->CharacterContentId != character.CID,
                     $"Gearsets are not loaded for {character.Name}@{character.World}.");
        ErrorThrowIf(gearsetId >= 100 || !module->IsValidGearset(gearsetId),
                     $"Gearset {gearsetId + 1} no longer exists for {character.Name}@{character.World}.");
        var gearset = module->GetGearset(gearsetId);
        ErrorThrowIf(gearset == null || !gearset->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists), "Selected gearset no longer exists.");
        ErrorThrowIf(!IsCombat(gearset->ClassJob), "The selected gearset must be a combat class or job.");
        // Missing-item confirmations can defer the equip; the caller verifies it after answering them.
        module->EquipGearset(gearsetId);
        return gearset->ClassJob;
    }

    private static unsafe bool IsCycleGearsetEquipped(LogCycleCharacter character, byte classJob)
    {
        var module = RaptureGearsetModule.Instance();
        return Player.Available && Player.CID == character.CID && module != null && module->CharacterContentId == character.CID &&
               module->CurrentGearsetIndex == character.GearsetId && PlayerState.Instance()->CurrentClassJobId == classJob && !Player.IsBusy;
    }

    internal void CleanupCycle()
    {
        try
        {
            if (cycleAutoRetainerState is not { } state) return;
            cycleAutoRetainerState = null;
            if (!SubscriptionManager.IsLoaded(IPCNames.AutoRetainer)) return;
            try { AutoRetainer.SetMultiModeEnabled.Invoke(state.MultiMode); }
            finally { AutoRetainer.SetSuppressed.Invoke(state.Suppressed); }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to restore AutoRetainer after the log cycle.");
        }
        finally
        {
            CleanupCombatAutomation();
        }
    }
}
