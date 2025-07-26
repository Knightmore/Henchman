using ECommons.EzIpcManager;
using System.Threading;
using System.Threading.Tasks;
using Action = System.Action;


namespace Henchman.IPC;

[IPC(IPCNames.vnavmesh)]
public static class Vnavmesh
{
    [EzIPC("Nav.IsReady")]
    public static Func<bool> NavIsReady;

    [EzIPC("Nav.BuildProgress")]
    public static Func<float> NavBuildProgress;

    [EzIPC("Nav.Reload")]
    public static Action NavReload;

    [EzIPC("Nav.Rebuild")]
    public static Action NavRebuild;

    [EzIPC("Nav.Pathfind")]
    public static Func<Vector3, Vector3, bool, Task<List<Vector3>>> NavPathfind;

    [EzIPC("Nav.PathfindCancelable")]
    public static Func<Vector3, Vector3, bool, CancellationToken, Task<List<Vector3>>> NavPathfindCancelable;

    [EzIPC("Nav.PathfindCancelAll")]
    public static Action NavPathfindCancelAll;

    [EzIPC("Nav.PathfindInProgress")]
    public static Func<bool> NavPathfindInProgress;

    [EzIPC("Nav.PathfindNumQueued")]
    public static Func<int> NavPathfindNumQueued;

    [EzIPC("Nav.IsAutoLoad")]
    public static Func<bool> NavIsAutoLoad;

    [EzIPC("Nav.SetAutoLoad")]
    public static Action<bool> NavSetAutoLoad;

    [EzIPC("Query.Mesh.NearestPoint")]
    public static Func<Vector3, float, float, Vector3?> QueryMeshNearestPoint;

    [EzIPC("Query.Mesh.PointOnFloor")]
    public static Func<Vector3, bool, float, Vector3?> QueryMeshPointOnFloor;

    [EzIPC("Path.MoveTo")]
    public static Action<List<Vector3>, bool> PathMoveTo;

    [EzIPC("Path.Stop")]
    public static Action PathStop;

    [EzIPC("Path.IsRunning")]
    public static Func<bool> PathIsRunning;

    [EzIPC("Path.NumWaypoints")]
    public static Func<int> PathNumWaypoints;

    [EzIPC("Path.GetMovementAllowed")]
    public static Func<bool> PathGetMovementAllowed;

    [EzIPC("Path.SetMovementAllowed")]
    public static Action<bool> PathSetMovementAllowed;

    [EzIPC("Path.GetAlignCamera")]
    public static Func<bool> PathGetAlignCamera;

    [EzIPC("Path.SetAlignCamera")]
    public static Action<bool> PathSetAlignCamera;

    [EzIPC("Path.GetTolerance")]
    public static Func<float> PathGetTolerance;

    [EzIPC("Path.SetTolerance")]
    public static Action<float> PathSetTolerance;

    [EzIPC("SimpleMove.PathfindAndMoveTo")]
    public static Func<Vector3, bool, bool> SimpleMovePathfindAndMoveTo;

    [EzIPC("SimpleMove.PathfindInProgress")]
    public static Func<bool> SimpleMovePathfindInProgress;

    [EzIPC("Window.IsOpen")]
    public static Func<bool> WindowIsOpen;

    [EzIPC("Window.SetOpen")]
    public static Action<bool> WindowSetOpen;

    [EzIPC("DTR.IsShown")]
    public static Func<bool> DtrIsShown;

    [EzIPC("DTR.SetShown")]
    public static Action<bool> DtrSetShown;

    public static void StopCompletely()
    {
        PathStop();
        NavPathfindCancelAll();
    }
}
