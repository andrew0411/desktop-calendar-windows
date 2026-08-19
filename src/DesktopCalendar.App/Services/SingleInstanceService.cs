namespace DesktopCalendar.App.Services;

public sealed class SingleInstanceService : IDisposable
{
    private const string MutexName = "Local\\DesktopCalendarApp-Mutex-1A7D86D5";
    private const string EventName = "Local\\DesktopCalendarApp-Activate-1A7D86D5";
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activationEvent;
    private readonly CancellationTokenSource _cancellation = new();

    public SingleInstanceService()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        IsFirstInstance = createdNew;
        _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
    }

    public bool IsFirstInstance { get; }

    public void SignalExistingInstance() => _activationEvent.Set();

    public void Listen(Action callback)
    {
        if (!IsFirstInstance)
            return;
        _ = Task.Run(() =>
        {
            while (!_cancellation.IsCancellationRequested)
            {
                var signaled = WaitHandle.WaitAny([_activationEvent, _cancellation.Token.WaitHandle]);
                if (signaled == 0)
                    callback();
            }
        });
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        _activationEvent.Dispose();
        if (IsFirstInstance)
            _mutex.ReleaseMutex();
        _mutex.Dispose();
        _cancellation.Dispose();
    }
}

