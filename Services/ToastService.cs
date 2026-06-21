namespace DndCompanion.Services;

public enum ToastType { Success, Error, Info }

/// <summary>
/// Servizio leggero per i messaggi "toast" a comparsa. Singleton: le pagine chiamano
/// ShowSuccess/ShowError/ShowInfo; il componente <c>ToastHost</c> (nel MainLayout) si
/// sottoscrive a <see cref="OnShow"/> e li visualizza con auto-dismiss.
/// </summary>
public class ToastService
{
    public event Action<string, ToastType>? OnShow;

    public void ShowSuccess(string message) => OnShow?.Invoke(message, ToastType.Success);
    public void ShowError(string message) => OnShow?.Invoke(message, ToastType.Error);
    public void ShowInfo(string message) => OnShow?.Invoke(message, ToastType.Info);
}
