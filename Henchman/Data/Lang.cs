using Henchman.Features.RetainerVocate;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;

namespace Henchman.Data;

internal static class Lang
{
    internal static string SelectYesNoHireARetainer => Svc.Data.GetExcelSheet<RawRow>(name: "custom/000/CmnDefRetainerDesk_00009")
                                                          .GetRow(84)
                                                          .ReadStringColumn(1)
                                                          .ExtractText();

    internal static string SelectYesNoUseSavedAppearance => Svc.Data.GetExcelSheet<Lobby>()
                                                               .GetRow(2044)
                                                               .Text.ExtractText();

    internal static string SelectYesNoSaveAppearance => Svc.Data.GetExcelSheet<Lobby>()
                                                           .GetRow(2176)
                                                           .Text.ExtractText();

    internal static string SelectYesNoFinalizeRetainer => Svc.Data.GetExcelSheet<Lobby>()
                                                             .GetRow(621)
                                                             .Text.ExtractText();

    internal static string SelectYesNoHireThisRetainer => Svc.Data.GetExcelSheet<RawRow>(name: "custom/000/CmnDefRetainerDesk_00009")
                                                             .GetRow(76)
                                                             .ReadStringColumn(1)
                                                             .ExtractText();

    internal static string SelectStringHireARetainer => Svc.Data.GetExcelSheet<RawRow>(name: "custom/000/CmnDefRetainerDesk_00009")
                                                           .GetRow(6)
                                                           .ReadStringColumn(1)
                                                           .ExtractText();

    internal static string SelectStringNothingRetainerDesk => Svc.Data.GetExcelSheet<RawRow>(name: "custom/000/CmnDefRetainerDesk_00009")
                                                                 .GetRow(12)
                                                                 .ReadStringColumn(1)
                                                                 .ExtractText();

    internal static ReadOnlySeString SelectStringHireTheServicesRetainer => Svc.Data.GetExcelSheet<RawRow>(name: "custom/000/CmnDefRetainerDesk_00009")
                                                                               .GetRow(83)
                                                                               .ReadStringColumn(1);


    internal static string SelectStringAssignRetainerClass => Svc.Data.GetExcelSheet<Addon>()
                                                                 .GetRow(2391)
                                                                 .Text.ExtractText();

    internal static string SelectStringNoMainEquipped => Svc.Data.GetExcelSheet<Addon>()
                                                            .GetRow(2389)
                                                            .Text.ExtractText();

    internal static ReadOnlySeString SelectYesNoClassConfirmAsk => Svc.Data.GetExcelSheet<RawRow>(name: "custom/000/CmnDefRetainerCall_00010")
                                                                      .GetRow(208)
                                                                      .ReadStringColumn(1);

    internal static string SelectStringAssignVenture => Svc.Data.GetExcelSheet<Addon>()
                                                           .GetRow(2386)
                                                           .Text.ExtractText();

    internal static string SelectStringVentureCategoryFieldExploration => Svc.Data.GetExcelSheet<RawRow>(name: "custom/000/CmnDefRetainerCall_00010")
                                                                             .GetRow(196)
                                                                             .ReadStringColumn(1)
                                                                             .ExtractText();

    internal static string SelectStringVentureCategoryHighlandExploration => Svc.Data.GetExcelSheet<RawRow>(name: "custom/000/CmnDefRetainerCall_00010")
                                                                                .GetRow(198)
                                                                                .ReadStringColumn(1)
                                                                                .ExtractText();

    internal static string SelectStringVentureCategoryWoodlandExploration => Svc.Data.GetExcelSheet<RawRow>(name: "custom/000/CmnDefRetainerCall_00010")
                                                                                .GetRow(200)
                                                                                .ReadStringColumn(1)
                                                                                .ExtractText();

    internal static string SelectStringVentureCategoryWatersideExploration => Svc.Data.GetExcelSheet<RawRow>(name: "custom/000/CmnDefRetainerCall_00010")
                                                                                 .GetRow(202)
                                                                                 .ReadStringColumn(1)
                                                                                 .ExtractText();

    internal static string SelectStringQuitWithDot => Svc.Data.GetExcelSheet<Addon>()
                                                         .GetRow(917)
                                                         .Text.ExtractText();

    internal static string SelectStringCancel => Svc.Data.GetExcelSheet<Addon>()
                                                    .GetRow(2)
                                                    .Text.ExtractText();

    internal static string SelectYesNoLogout => Svc.Data.GetExcelSheet<Addon>()
                                                   .GetRow(115)
                                                   .Text.ExtractText();

    internal static ReadOnlySeString SelectYesNoReturnTo => Svc.Data.GetExcelSheet<Addon>()
                                                               .GetRow(111)
                                                               .Text;

    internal static string TradeText => Svc.Data.GetExcelSheet<Addon>()
                                           .GetRow(102223)
                                           .Text.ExtractText();

    internal static string SelectStringBoardOCShip => Svc.Data.GetExcelSheet<RawRow>(name: "custom/006/CtsIkdEntrance_00663")
                                                         .GetRow(4)
                                                         .ReadStringColumn(1)
                                                         .ExtractText();

    internal static ReadOnlySeString SelectYesNoEmbark => Svc.Data.GetExcelSheet<RawRow>(name: "custom/006/CtsIkdEntrance_00663")
                                                             .GetRow(10)
                                                             .ReadStringColumn(1);

    internal static ReadOnlySeString SelectOkCongested => Svc.Data.GetExcelSheet<Error>()
                                                             .GetRow(13206)
                                                             .Unknown0;

    internal static string SelectStringSkipCutscene => Svc.Data.GetExcelSheet<Addon>()
                                                          .GetRow(3)
                                                          .Text.ExtractText();

    internal static string SelectStringMouseKeyboard => Svc.Data.GetExcelSheet<Addon>()
                                                           .GetRow(3392)
                                                           .Text.ExtractText();

    internal static ReadOnlySeString DialogueNameTaken => Svc.Data.GetExcelSheet<Error>()
                                                             .GetRow(13004)
                                                             .Unknown0;

    internal static string SelectYesnoNewGame => Svc.Data.GetExcelSheet<Lobby>()
                                                    .GetRow(32)
                                                    .Text.ExtractText();

    internal static string SelectYesnoLeaveQueue => Svc.Data.GetExcelSheet<Lobby>()
                                                       .GetRow(1800)
                                                       .Text.ExtractText();

    internal static string DailyHuntString(uint rowId) => Svc.Data.GetExcelSheet<RawRow>(name: "custom/002/ComDefMobHuntBoard_00202")
                                                             .GetRow(rowId)
                                                             .ReadStringColumn(1)
                                                             .ExtractText();

    internal static string SelectStringRetainerPersonality(RetainerDetails.RetainerPersonality personality) => Svc.Data.GetExcelSheet<RawRow>(Svc.Data.Language, "custom/000/CmnDefRetainerDesk_00009")
                                                                                                                  .TryGetRow((uint)personality, out var row)
                                                                                                                       ? row.ReadStringColumn(1)
                                                                                                                            .ExtractText()
                                                                                                                       : "";
}
