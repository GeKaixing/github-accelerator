using System.Threading;

namespace GitHub.Accelerator.Core;

public sealed class SingleInstanceGuard : IDisposable
{
    private readonly Mutex? _mutex;
    public bool Acquired { get; }

    private SingleInstanceGuard(Mutex? mutex, bool acquired)
    {
        _mutex = mutex;
        Acquired = acquired;
    }

    public static SingleInstanceGuard TryAcquire(string name)
    {
        try
        {
            var mutex = new Mutex(initiallyOwned: true, name: name, createdNew: out var createdNew);
            return new SingleInstanceGuard(mutex, createdNew);
        }
        catch
        {
            return new SingleInstanceGuard(null, false);
        }
    }

    public void Dispose()
    {
        try
        {
            _mutex?.ReleaseMutex();
        }
        catch
        {
        }

        try
        {
            _mutex?.Dispose();
        }
        catch
        {
        }
    }
}
