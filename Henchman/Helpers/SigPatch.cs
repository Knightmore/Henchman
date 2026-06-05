using Dalamud.Memory;

namespace Henchman.Helpers;

public sealed class SigPatch : IDisposable
{
    private readonly nint   address;
    private readonly byte[] original;
    private readonly byte[] patch;

    public SigPatch(string signature, byte[] patch, int offset = 0)
    {
        address    = Svc.SigScanner.ScanText(signature) + offset;
        this.patch = patch;
        original   = MemoryHelper.ReadRaw(address, patch.Length);
    }

    public bool IsEnabled { get; private set; }

    public void Dispose() => Disable();

    public void Enable()
    {
        if (IsEnabled) return;

        var old = MemoryHelper.ChangePermission(
                                                address,
                                                patch.Length,
                                                MemoryProtection.ExecuteReadWrite);

        MemoryHelper.WriteRaw(address, patch);

        MemoryHelper.ChangePermission(address, patch.Length, old);
        IsEnabled = true;
    }

    public void Disable()
    {
        if (!IsEnabled) return;

        var old = MemoryHelper.ChangePermission(
                                                address,
                                                original.Length,
                                                MemoryProtection.ExecuteReadWrite);

        MemoryHelper.WriteRaw(address, original);

        MemoryHelper.ChangePermission(address, original.Length, old);
        IsEnabled = false;
    }
}
