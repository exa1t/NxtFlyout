namespace NxtFlyout.Services;

/// <summary>
/// Ensures one copy of the app per Windows session. A second launch signals the running
/// copy (which then opens its settings window) and exits.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activateEvent;
    private RegisteredWaitHandle? _registration;
    private bool _disposed;

    public SingleInstance(string id)
    {
        // "Local\" scopes the names to the current session, so each signed-in user gets their own instance.
        _mutex = new Mutex(initiallyOwned: false, $@"Local\{id}.Mutex", out var createdNew);
        IsFirstInstance = createdNew;
        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\{id}.Activate");
    }

    /// <summary>True if no other copy was running when this object was created.</summary>
    public bool IsFirstInstance { get; }

    /// <summary>
    /// Calls <paramref name="onActivated"/> on a thread-pool thread each time another launch
    /// signals this instance.
    /// </summary>
    public void ListenForActivation(Action onActivated)
    {
        ArgumentNullException.ThrowIfNull(onActivated);

        _registration?.Unregister(null);
        _registration = ThreadPool.RegisterWaitForSingleObject(
            _activateEvent,
            (_, timedOut) =>
            {
                if (!timedOut)
                {
                    onActivated();
                }
            },
            null,
            Timeout.Infinite,
            false);
    }

    /// <summary>Asks the instance that is already running to come forward.</summary>
    public void SignalFirstInstance() => _activateEvent.Set();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _registration?.Unregister(null);
        _activateEvent.Dispose();
        _mutex.Dispose();
    }
}
