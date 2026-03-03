using MudBlazor;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Services;

public class ToastService
{
    private static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(5);
    private readonly ISnackbar _snackbar;

    public ToastService(ISnackbar snackbar)
    {
        _snackbar = snackbar;
    }

    public event Action<ToastMessage>? OnShow;

    public void ShowSuccess(string message) => Show(message, ToastType.Success, Severity.Success);

    public void ShowError(string message) => Show(message, ToastType.Error, Severity.Error);

    public void ShowWarning(string message) => Show(message, ToastType.Warning, Severity.Warning);

    private void Show(string message, ToastType type, Severity severity)
    {
        var toast = new ToastMessage(message, type, DateTime.UtcNow);
        OnShow?.Invoke(toast);

        _snackbar.Add(message, severity, options =>
        {
            options.ShowCloseIcon = true;
            options.VisibleStateDuration = (int)DefaultDuration.TotalMilliseconds;
            options.HideTransitionDuration = 150;
            options.ShowTransitionDuration = 150;
        });
    }
}

public enum ToastType
{
    Success,
    Error,
    Warning
}

public record ToastMessage(string Message, ToastType Type, DateTime CreatedAt);
