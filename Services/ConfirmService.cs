namespace DndCompanion.Services;

/// <summary>
/// Dialog di conferma a tema (sostituisce il <c>window.confirm()</c> nativo). Singleton: le pagine
/// chiamano <c>await Confirm.ShowAsync("...")</c>; il componente <c>ConfirmDialog</c> (nel MainLayout)
/// si registra e mostra il dialog, risolvendo il Task con true/false.
/// </summary>
public class ConfirmService
{
    private Func<string, Task<bool>>? _handler;

    public void Register(Func<string, Task<bool>> handler) => _handler = handler;
    public void Unregister() => _handler = null;

    public Task<bool> ShowAsync(string message)
        => _handler?.Invoke(message) ?? Task.FromResult(false);
}
