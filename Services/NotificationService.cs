using PITS.Models;

namespace PITS.Services;

public class NotificationService
{
    public event Action<AppNotification>? OnNotification;

    public void Push(NotificationType type, string message)
    {
        var notification = new AppNotification
        {
            Type = type,
            Message = message
        };

        OnNotification?.Invoke(notification);
    }
}
