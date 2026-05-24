namespace PITS.Services;

public class InventoryChangeNotifier
{
    public event Action? Changed;

    public void Notify()
    {
        Changed?.Invoke();
    }
}
