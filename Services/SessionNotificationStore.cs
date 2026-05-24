using PITS.Models;

namespace PITS.Services;

public class SessionNotificationStore
{
    private readonly List<AppNotification> _active = new();
    private readonly List<AppNotification> _history = new();
    private readonly object _lock = new();

    public IReadOnlyList<AppNotification> Active
    {
        get { lock (_lock) return _active.ToList(); }
    }

    public IReadOnlyList<AppNotification> History
    {
        get { lock (_lock) return _history.ToList(); }
    }

    public void Add(AppNotification notification)
    {
        lock (_lock)
        {
            if (notification.Type == NotificationType.UserError)
            {
                var existing = _active.FirstOrDefault(n =>
                    n.Type == notification.Type && n.Message == notification.Message);
                if (existing != null)
                {
                    _active.Remove(existing);
                    existing.IsDismissed = true;
                }
            }

            _active.Add(notification);
            _history.Add(notification);
        }
    }

    public void Dismiss(string id)
    {
        lock (_lock)
        {
            var notification = _active.FirstOrDefault(n => n.Id == id);
            if (notification != null)
            {
                _active.Remove(notification);
                notification.IsDismissed = true;
            }
        }
    }

    public void AutoDismiss(string id)
    {
        lock (_lock)
        {
            var notification = _active.FirstOrDefault(n => n.Id == id);
            if (notification != null)
                _active.Remove(notification);
        }
    }

    public void ClearHistory()
    {
        lock (_lock)
        {
            _active.Clear();
            _history.Clear();
        }
    }
}
