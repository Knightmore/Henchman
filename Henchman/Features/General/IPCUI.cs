using System.Linq;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Underlings.Modules;

namespace Henchman.Features.General;

[Module]
internal class IPCUI : ModuleUI
{
    private         Table<IPCCatalog.IPCEntry>? ipcTable;
    public override string                      Name        => "IPC";
    public override Enum                        Category    => Henchman.Category.System;
    public override FontAwesomeIcon             Icon        => FontAwesomeIcon.Plug;
    public override Action?                     Help        => () => { ImGui.Text(T("HelpText")); };
    public override bool                        LoginNeeded => false;

    public override void Draw()
    {
        ipcTable ??= new Table<IPCCatalog.IPCEntry>(
                                                    "ipc_table",
                                                    new List<TableColumn<IPCCatalog.IPCEntry>>
                                                    {
                                                            new(T("ColReturn"), x => x.ReturnType, 120),
                                                            new(T("ColFunction"), x => x.Signature, 250),
                                                            new(T("ColDescription"), x => x.Description, 400)
                                                    },
                                                    () => IPCCatalog.BuildIpcList(typeof(IPCProvider))
                                                   );

        ipcTable.Draw();
    }


    public static string FormatIpcList(List<(string IPC, string Description)> list)
    {
        if (list.Count == 0)
            return string.Empty;

        var maxLen = list.Max(x => x.IPC.Length);

        var sb = new StringBuilder(list.Count * 64);

        foreach (var (ipc, desc) in list)
        {
            sb.Append(ipc.PadRight(maxLen));
            sb.Append(" - ");
            sb.Append(desc);
            sb.Append('\n');
        }

        return sb.ToString();
    }
}
