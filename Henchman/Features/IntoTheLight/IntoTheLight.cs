using System.Threading;
using Dalamud.Game.ClientState.Conditions;
using ECommons.Automation;
using ECommons.Automation.UIInput;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Henchman.Data;
using Henchman.Helpers;
using Lumina.Excel.Sheets;
using Task = System.Threading.Tasks.Task;

namespace Henchman.Features.IntoTheLight;

internal class IntoTheLight
{
    private int index;

    public async Task Start(CancellationToken token = default)
    {
        AutoCutsceneSkipper.Enable();
        index = 0;
        uint validPresets;
        unsafe
        {
            validPresets = Framework.Instance()->CharamakeAvatarSaveData->Release.GetValidSlotCount();
        }

        unsafe
        {
            if (TryGetAddonByName<AtkUnitBase>("_TitleMenu", out var addon) && !IsAddonReady(addon) && !addon->IsVisible) return;
        }

        await OpenDataCenter(token);
        for (index = 0; index < C.LightCharacters.Count; index++)
        {
            var differentDataCenter = false;
            unsafe
            {
                if (AgentModule.Instance()->GetAgentLobby()->DataCenter != C.LightCharacters[index].DataCenterId)
                {
                    if (TryGetAddonByName<AtkUnitBase>("_CharaSelectReturn", out var charaSelectReturn) && IsAddonReady(charaSelectReturn))
                    {
                        Callback.Fire(charaSelectReturn, true, 19);
                        differentDataCenter = true;
                    }
                }
            }

            if (differentDataCenter)
                await OpenDataCenter(token);

            await WaitUntilAsync(() => SelectCreateCharacter(), "Select Create Character", token);
            if (validPresets > 0)
            {
                await WaitUntilAsync(() => SelectIfUsePreset(C.LightCharacters[index].PresetId != 255), "Select if to use Preset", token);
                if (C.LightCharacters[index].PresetId != 255) await WaitUntilAsync(() => AddonHelpers.SelectPreset(C.LightCharacters[index].PresetId), "Select Preset", token);
            }

            if (C.LightCharacters[index].PresetId == 255)
            {
                int maxRace;
                unsafe
                {
                    maxRace = Framework.Instance()->DevConfig.GetConfigOption(22)->Value.UInt >= 3
                                      ? 8
                                      : 6;
                }

                var genderId = Random.Shared.Next(2) == 0
                                       ? Random.Shared.Next(10, 10 + maxRace)
                                       : Random.Shared.Next(19, 19 + maxRace);

                await WaitUntilAsync(() => SelectRaceAndGender(genderId), "Select Race and Gender", token);
                await WaitUntilAsync(() => ProgressTribe(), "Progress Tribe", token);
                await WaitUntilAsync(() => RandomizeCharacterLook(), "Randomize Character", token);
            }

            await WaitUntilAsync(() => FinishCharacterLooks(), "Finish Character Looks", token);
            await WaitUntilAsync(() => GenericYesNo(false), "Deny Preset Saving", token);
            await WaitUntilAsync(() => ChooseRandomNameDay(), "Choose Nameday", token);
            await WaitUntilAsync(() => ChooseRandomGuardian(), "Choose Guardian", token);
            // ClassJob Ids are -1 in Callbacks
            await WaitUntilAsync(() => ChooseClass((uint)C.LightCharacters[index].ClassJob), "Choose Class", token);
            await WaitUntilAsync(() => UpdateServerList(), "Choose Server", token);
            await Task.Delay(500, token);
            await WaitUntilAsync(() => SelectServer(C.LightCharacters[index].WorldId), "Select Server", token);
            while (true)
            {
                await WaitUntilAsync(() => SelectName(C.LightCharacters[index].FirstName, C.LightCharacters[index].LastName), "Select Name", token);

                using var namingTokenSrc  = new CancellationTokenSource();
                using var linkedNamingCts = CancellationTokenSource.CreateLinkedTokenSource(token, namingTokenSrc.Token);

                var charConfirmation = WaitUntilAsync(() => ProcessYesNo(true, Lang.SelectYesnoNewGame), "Checking for New Game Yesno.", linkedNamingCts.Token);
                var takenName = WaitUntilAsync(() =>
                                               {
                                                   unsafe
                                                   {
                                                       if (TryGetAddonByName<AddonDialogue>("Dialogue", out var addon) && addon->IsReady && addon->IsVisible)
                                                       {
                                                           addon->GetComponentButtonById(4)->ClickAddonButton(&addon->AtkUnitBase);
                                                           return true;
                                                       }

                                                       return false;
                                                   }
                                               }, "Checking for name taken", linkedNamingCts.Token);

                var completedNamingTask = await Task.WhenAny(charConfirmation, takenName);
                linkedNamingCts.Cancel();
                await completedNamingTask;

                if (completedNamingTask == charConfirmation) break;
            }

            using var loginTokenSrc  = new CancellationTokenSource();
            using var linkedLoginCts = CancellationTokenSource.CreateLinkedTokenSource(token, loginTokenSrc.Token);

            var loginQueue  = WaitUntilAsync(() => ConfirmSpecificSelectOk(Lang.SelectOkCongested), "Checking for login queue.", linkedLoginCts.Token);
            var directLogin = WaitUntilAsync(() => Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent], "Checking for first Cutscene", linkedLoginCts.Token);

            var completedLoginTask = await Task.WhenAny(loginQueue, directLogin);
            linkedLoginCts.Cancel();
            await completedLoginTask;

            if (completedLoginTask != loginQueue)
            {
                Verbose("Progressing without queue!");
                await WaitUntilAsync(() => Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent], "Wait for cutscene", token);

                await WaitUntilAsync(() => TrySelectSpecificEntry(Lang.SelectStringSkipCutscene), "Skip cutscene", token);

                await WaitUntilAsync(() => TrySelectSpecificEntry(Lang.SelectStringMouseKeyboard), "Skip input controls", token);
                while (true)
                {
                    unsafe
                    {
                        if (TryGetAddonByName<AtkUnitBase>("SelectYesno", out _))
                            break;
                    }

                    Chat.SendMessage("/logout");
                    await Task.Delay(8 * GeneralDelayMs, token);
                }

                await WaitUntilAsync(() => ProcessYesNo(true, Lang.SelectYesNoLogout), "Confirm logout", token);
                await OpenDataCenter(token);
            }
            else
                await WaitUntilAsync(() => ProcessYesNo(true, Lang.SelectYesnoLeaveQueue), "Confirm leave queue", token);

            await Task.Delay(4 * GeneralDelayMs, token);
        }
    }

    internal async Task Stop()
    {
        AutoCutsceneSkipper.Disable();
        await Task.Delay(GeneralDelayMs);
    }

    private async Task OpenDataCenter(CancellationToken token = default)
    {
        await WaitUntilAsync(() => SelectDataCenterMenu(), "Select DataCenter Menu Entry", token);
        await WaitUntilAsync(() => SelectDataCenter((int)C.LightCharacters[index].DataCenterId), "Select DataCenter", token);
    }

    private unsafe bool SelectDataCenterMenu()
    {
        if (TryGetAddonByName<AtkUnitBase>("TitleDCWorldMap", out var dcWorldMapAddon) && dcWorldMapAddon->IsVisible)
        {
            PluginLog.Information("Visible");
            return true;
        }

        if (TryGetAddonMaster<AddonMaster._TitleMenu>(out var titleMenuAddon) && titleMenuAddon.IsReady) titleMenuAddon.DataCenter();

        return false;
    }

    private bool SelectDataCenter(int dc)
    {
        if (TryGetAddonMaster<AddonMaster.TitleDCWorldMap>(out var dcWorldMapAddon) && dcWorldMapAddon.IsAddonReady)
        {
            dcWorldMapAddon.Select(dc);
            return true;
        }

        return false;
    }

    private unsafe bool SelectCreateCharacter()
    {
        if (TryGetAddonByName<AtkUnitBase>("_CharaSelectListMenu", out var charaSelectListMenuAddon) && IsAddonReady(charaSelectListMenuAddon))
        {
            Callback.Fire(charaSelectListMenuAddon, true, 29, 0, -1);
            return true;
        }

        return false;
    }

    private unsafe bool SelectIfUsePreset(bool usePreset)
    {
        if (TryGetAddonByName<AtkUnitBase>("SelectYesno", out var selectYesNoAddon) && IsAddonReady(selectYesNoAddon))
        {
            Callback.Fire(selectYesNoAddon, true, usePreset
                                                          ? 6
                                                          : 7);
            return true;
        }

        return false;
    }

    private bool SelectRaceAndGender(int genderId)
    {
        unsafe
        {
            if (TryGetAddonByName<AtkUnitBase>("_CharaMakeRaceGender", out var charaMakeRaceGenderAddon) && IsAddonReady(charaMakeRaceGenderAddon))
            {
                if (TryGetAddonByName<AtkUnitBase>("_CharaMakeProgress", out var charaMakeProgressAddon) && IsAddonReady(charaMakeProgressAddon))
                {
                    var raceGenderEvt  = new AtkEvent { Listener = &charaMakeRaceGenderAddon->AtkEventListener, Target = &AtkStage.Instance()->AtkEventTarget };
                    var raceGenderData = new AtkEventData();
                    charaMakeRaceGenderAddon->ReceiveEvent(AtkEventType.ButtonClick, genderId, &raceGenderEvt, &raceGenderData);
                    var confirmEvt  = new AtkEvent { Listener = &charaMakeRaceGenderAddon->AtkEventListener, Target = &AtkStage.Instance()->AtkEventTarget };
                    var confirmData = new AtkEventData();
                    charaMakeRaceGenderAddon->ReceiveEvent(AtkEventType.ButtonClick, 28, &confirmEvt, &confirmData);
                    return true;
                }
            }
        }

        return false;
    }

    private unsafe bool ProgressTribe()
    {
        if (TryGetAddonByName<AtkUnitBase>("_CharaMakeTribe", out var charaMakeTribe) && IsAddonReady(charaMakeTribe))
        {
            var evt  = new AtkEvent { Node = null, Listener = &charaMakeTribe->AtkEventListener, Target = &AtkStage.Instance()->AtkEventTarget, Param = 3 };
            var data = new AtkEventData();
            charaMakeTribe->ReceiveEvent(AtkEventType.ButtonClick, 3, &evt, &data);
            return true;
        }

        return false;
    }

    private bool RandomizeCharacterLook()
    {
        unsafe
        {
            if (TryGetAddonByName<AtkUnitBase>("_CharaMakeFeature", out var charaMakeFeatureAddon) && IsAddonReady(charaMakeFeatureAddon))
            {
                Callback.Fire(charaMakeFeatureAddon, true, -9, 0);
                return true;
            }
        }

        return false;
    }

    private unsafe bool FinishCharacterLooks()
    {
        if (TryGetAddonByName<AtkUnitBase>("_CharaMakeFeature", out var charaMakeFeatureAddon) && IsAddonReady(charaMakeFeatureAddon))
        {
            Callback.Fire(charaMakeFeatureAddon, true, 100);

            return true;
        }

        return false;
    }

    private unsafe bool ChooseRandomNameDay()
    {
        if (TryGetAddonByName<AtkUnitBase>("_CharaMakeBirthDay", out var charaMakeBirthDayAddon) && TryGetAddonByName<AtkUnitBase>("_CharaMakeProgress", out var charaMakeProgessAddon) && IsAddonReady(charaMakeProgessAddon))
        {
            var dropDown = charaMakeBirthDayAddon->GetNodeById(3)->GetAsAtkComponentDropdownList();
            dropDown->SelectItem(Random.Shared.Next(11));
            var evt  = new AtkEvent { Listener = &charaMakeBirthDayAddon->AtkEventListener, Target = &AtkStage.Instance()->AtkEventTarget };
            var data = new AtkEventData();
            charaMakeBirthDayAddon->ReceiveEvent(AtkEventType.ButtonClick, Random.Shared.Next(2, 33), &evt, &data);
            charaMakeBirthDayAddon->ReceiveEvent(AtkEventType.ButtonClick, 0, &evt, &data);
            return true;
        }

        return false;
    }

    private unsafe bool ChooseRandomGuardian()
    {
        if (TryGetAddonByName<AtkUnitBase>("_CharaMakeGuardian", out var charaMakeGuardianAddon) && IsAddonReady(charaMakeGuardianAddon))
        {
            var evt  = new AtkEvent { Listener = &charaMakeGuardianAddon->AtkEventListener, Target = &AtkStage.Instance()->AtkEventTarget };
            var data = new AtkEventData();
            charaMakeGuardianAddon->ReceiveEvent(AtkEventType.ButtonClick, Random.Shared.Next(2, 13), &evt, &data);
            charaMakeGuardianAddon->ReceiveEvent(AtkEventType.ButtonClick, 0, &evt, &data);
            return true;
        }

        return false;
    }

    private unsafe bool ChooseClass(uint classJobId)
    {
        if (TryGetAddonByName<AtkUnitBase>("_CharaMakeProgress", out var charaMakeProgressAddon) && IsAddonReady(charaMakeProgressAddon))
        {
            Callback.Fire(charaMakeProgressAddon, true, 5, classJobId, -1, 0, string.Empty, 0);
            if (TryGetAddonByName<AtkUnitBase>("_CharaMakeClassSelector", out var charaMakeClassSelectorAddon) && IsAddonReady(charaMakeClassSelectorAddon))
            {
                var evt  = new AtkEvent { Listener = &charaMakeClassSelectorAddon->AtkEventListener, Target = &AtkStage.Instance()->AtkEventTarget };
                var data = new AtkEventData();
                charaMakeClassSelectorAddon->ReceiveEvent(AtkEventType.ButtonClick, 2, &evt, &data);
                return true;
            }
        }

        return false;
    }

    private unsafe bool UpdateServerList()
    {
        if (TryGetAddonByName<AtkUnitBase>("CharaMakeSelectYesNo", out var charaMakeSelectYesNo) && IsAddonReady(charaMakeSelectYesNo))
        {
            var resNode = charaMakeSelectYesNo->GetComponentNodeById(4);
            var nodeEvt = resNode->AtkEventManager.Event;
            while (nodeEvt != null)
            {
                if (nodeEvt->State.EventType == AtkEventType.ButtonClick)
                {
                    var evt  = new AtkEvent { Listener = &charaMakeSelectYesNo->AtkEventListener, Target = &AtkStage.Instance()->AtkEventTarget };
                    var data = new AtkEventData();
                    charaMakeSelectYesNo->ReceiveEvent(AtkEventType.ButtonClick, (int)nodeEvt->Param, &evt, &data);
                    if (TryGetAddonByName<AtkUnitBase>("_CharaMakeWorldServer", out var charaMakeWorldServer) && IsAddonReady(charaMakeWorldServer))
                    {
                        var list       = charaMakeWorldServer->GetComponentListById(10);
                        var listener   = list->GetAtkResNode()->AtkEventManager.Event->Listener;
                        var target     = list->GetAtkResNode()->AtkEventManager.Event->Target;
                        var scrollEvt  = new AtkEvent { Listener = listener, Target = target };
                        var scrollData = new AtkEventData();
                        listener->ReceiveEvent(AtkEventType.MouseWheel, 0, &scrollEvt, &scrollData);
                        listener->ReceiveEvent(AtkEventType.MouseWheel, 0, &scrollEvt, &scrollData);
                        listener->ReceiveEvent(AtkEventType.MouseWheel, 0, &scrollEvt, &scrollData);

                        return true;
                    }

                    break;
                }

                nodeEvt = nodeEvt->NextEvent;
            }
        }

        return false;
    }

    private unsafe bool SelectServer(uint worldId)
    {
        if (TryGetAddonByName<AtkUnitBase>("_CharaMakeWorldServer", out var charaMakeWorldServer) && IsAddonReady(charaMakeWorldServer))
        {
            var list = charaMakeWorldServer->GetComponentListById(10);

            for (var i = 0; i < list->GetItemCount(); i++)
            {
                var fiveContains = list->ItemRendererList[i].AtkComponentListItemRenderer->GetTextNodeById(5)->GetText()
                                  .ToString()
                                  .Contains(Svc.Data.GetExcelSheet<World>()
                                               .GetRow(worldId)
                                               .Name.ExtractText(), StringComparison.OrdinalIgnoreCase);
                var sixContains = list->ItemRendererList[i].AtkComponentListItemRenderer->GetTextNodeById(6)->GetText()
                                 .ToString()
                                 .Contains(Svc.Data.GetExcelSheet<World>()
                                              .GetRow(worldId)
                                              .Name.ExtractText(), StringComparison.OrdinalIgnoreCase);
                var sevenContains = list->ItemRendererList[i].AtkComponentListItemRenderer->GetTextNodeById(7)->GetText()
                                   .ToString()
                                   .Contains(Svc.Data.GetExcelSheet<World>()
                                                .GetRow(worldId)
                                                .Name.ExtractText(), StringComparison.OrdinalIgnoreCase);
                if (fiveContains || sixContains || sevenContains)
                {
                    ErrorThrowIf(list->ItemRendererList[i].AtkComponentListItemRenderer->GetResNodeById(4)->Alpha_2 == 127, "Server not open for new characters.");
                    if (TryGetAddonByName<AtkUnitBase>("_CharaMakeProgress", out var charaMakeProgressAddon) && IsAddonReady(charaMakeProgressAddon))
                    {
                        list->SelectItem(i);
                        var resNode = charaMakeWorldServer->GetComponentNodeById(13);
                        var nodeEvt = resNode->AtkEventManager.Event;
                        while (nodeEvt != null)
                        {
                            if (nodeEvt->State.EventType == AtkEventType.ButtonClick)
                            {
                                var evt  = new AtkEvent { Listener = &charaMakeWorldServer->AtkEventListener, Target = &AtkStage.Instance()->AtkEventTarget };
                                var data = new AtkEventData();
                                charaMakeWorldServer->ReceiveEvent(AtkEventType.ButtonClick, (int)nodeEvt->Param, &evt, &data);

                                return true;
                            }

                            nodeEvt = nodeEvt->NextEvent;
                        }
                    }
                }
            }
        }

        return false;
    }

    private unsafe bool SelectName(string firstName, string lastName)
    {
        if (TryGetAddonByName<AtkUnitBase>("_CharaMakeCharaName", out var charaMakeCharaName) && IsAddonReady(charaMakeCharaName) && TryGetAddonByName<AtkUnitBase>("_CharaMakeProgress", out var charaMakeProgressAddon) && IsAddonReady(charaMakeProgressAddon))
        {
            var text = charaMakeProgressAddon->GetComponentButtonById(2)->GetTextNodeById(5)->GetText()
                   .ToString();
            // © Male
            // ® Female
            var male = text.Contains("©");
            if (string.IsNullOrEmpty(firstName))
            {
                firstName = male
                                    ? NameGenerator.GetMasculineName()
                                    : NameGenerator.GetFeminineName();
            }

            var firstNameInput = charaMakeCharaName->GetNodeById(7)->GetAsAtkComponentTextInput();
            firstNameInput->SetText(firstName);

            if (string.IsNullOrEmpty(lastName)) lastName = NameGenerator.GetLastName(firstName);

            var lastNameInput = charaMakeCharaName->GetNodeById(9)->GetAsAtkComponentTextInput();
            lastNameInput->SetText(lastName);

            var resNode = charaMakeCharaName->GetComponentNodeById(16);
            var nodeEvt = resNode->AtkEventManager.Event;
            while (nodeEvt != null)
            {
                if (nodeEvt->State.EventType == AtkEventType.ButtonClick)
                {
                    var evt  = new AtkEvent { Listener = &charaMakeCharaName->AtkEventListener, Target = &AtkStage.Instance()->AtkEventTarget };
                    var data = new AtkEventData();
                    charaMakeCharaName->ReceiveEvent(AtkEventType.ButtonClick, (int)nodeEvt->Param, &evt, &data);

                    return true;
                }

                nodeEvt = nodeEvt->NextEvent;
            }
        }

        return false;
    }
}
