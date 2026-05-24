using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PITS.Services;

public class TrayService : IDisposable
{
    private readonly ILogger<TrayService> _logger;
    private readonly string _url;
    private bool _disposed;
    private IDisposable? _notifyIcon;

    public TrayService(ILogger<TrayService> logger, string url)
    {
        _logger = logger;
        _url = url;
    }

    public void Start()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            StartWindowsTray();
        else
            StartUnixTray();
    }

    private void StartWindowsTray()
    {
        try
        {
            var notifyIconType = Type.GetType("System.Windows.Forms.NotifyIcon, System.Windows.Forms");
            if (notifyIconType == null) return;

            _notifyIcon = Activator.CreateInstance(notifyIconType) as IDisposable;
            if (_notifyIcon == null) return;

            var textProp = notifyIconType.GetProperty("Text");
            textProp?.SetValue(_notifyIcon, "PITS - Personal Inventory Tracking System");

            var visibleProp = notifyIconType.GetProperty("Visible");
            visibleProp?.SetValue(_notifyIcon, true);

            var ctxMenuType = Type.GetType("System.Windows.Forms.ContextMenuStrip, System.Windows.Forms");
            if (ctxMenuType != null)
            {
                var ctxMenu = Activator.CreateInstance(ctxMenuType) as IDisposable;
                var itemsProp = ctxMenuType.GetProperty("Items");

                var openItem = CreateMenuItem("Open PITS", (_, _) => OpenBrowser(_url));
                var quitItem = CreateMenuItem("Quit", (_, _) => Environment.Exit(0));

                var addMethod = itemsProp?.PropertyType.GetMethod("Add", new[] { typeof(object) });
                addMethod?.Invoke(itemsProp?.GetValue(ctxMenu), new[] { openItem });
                addMethod?.Invoke(itemsProp?.GetValue(ctxMenu), new[] { CreateSeparator() });
                addMethod?.Invoke(itemsProp?.GetValue(ctxMenu), new[] { quitItem });

                var ctxProp = notifyIconType.GetProperty("ContextMenuStrip");
                ctxProp?.SetValue(_notifyIcon, ctxMenu);
            }

            _logger.LogInformation("System tray icon created");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not create system tray icon, running in console mode");
        }
    }

    private void StartUnixTray()
    {
        _logger.LogInformation("PITS running at {Url}", _url);
        Console.WriteLine($"PITS is running at: {_url}");
        Console.WriteLine("Open this URL in your browser to access the interface.");
        Console.WriteLine("Press Ctrl+C to quit.");
    }

    public static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not open browser: {ex.Message}");
        }
    }

    private static object CreateMenuItem(string text, EventHandler handler)
    {
        var itemType = Type.GetType("System.Windows.Forms.ToolStripMenuItem, System.Windows.Forms");
        if (itemType == null) throw new InvalidOperationException("WinForms not available");

        var item = Activator.CreateInstance(itemType, text) ?? throw new InvalidOperationException("Could not create menu item");
        var clickEvent = itemType.GetEvent("Click");
        if (clickEvent != null)
        {
            var handlerType = typeof(EventHandler);
            clickEvent.AddEventHandler(item, Delegate.CreateDelegate(handlerType, handler.Target, handler.Method));
        }
        return item;
    }

    private static object CreateSeparator()
    {
        var sepType = Type.GetType("System.Windows.Forms.ToolStripSeparator, System.Windows.Forms");
        return Activator.CreateInstance(sepType!)!;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _notifyIcon?.Dispose();
    }
}
