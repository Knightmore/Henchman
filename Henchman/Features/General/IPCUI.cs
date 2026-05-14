using System.Linq;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Henchman.Abstractions;
using Henchman.Windows.Layout;

namespace Henchman.Features.General;

[Feature]
internal class IPCUI : FeatureUI
{
    private         Table<IPCProvider.IpcEntry>? ipcTable;
    public override string                       Name        => "IPC";
    public override Category                     Category    => Category.System;
    public override FontAwesomeIcon              Icon        => FontAwesomeIcon.Plug;
    public override Action?                      Help        => () => { ImGui.Text("List of currently provided IPCs"); };
    public override bool                         LoginNeeded => false;

    public override void Draw()
    {
        ipcTable ??= new Table<IPCProvider.IpcEntry>(
                                                     "ipc_table",
                                                     new List<TableColumn<IPCProvider.IpcEntry>>
                                                     {
                                                             new("Return", x => x.ReturnType, 120),
                                                             new("Function", x => x.Signature, 250),
                                                             new("Description", x => x.Description, 400)
                                                     },
                                                     () => IPCProvider.BuildIpcList(typeof(IPCProvider))
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
