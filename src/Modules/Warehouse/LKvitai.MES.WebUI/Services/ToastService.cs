namespace LKvitai.MES.WebUI.Services;

public class ToastService
{
    public event Action<ToastMessage>? OnShow;

    public void ShowSuccess(string message) => OnShow?.Invoke(new ToastMessage(message, ToastType.Success, DateTime.UtcNow));

    public void ShowError(string message) => OnShow?.Invoke(new ToastMessage(message, ToastType.Error, DateTime.UtcNow));

    public void ShowWarning(string message) => OnShow?.Invoke(new ToastMessage(message, ToastType.Warning, DateTime.UtcNow));
}

public enum ToastType
{
    Success,
    Error,
    Warning
}

public record ToastMessage(string Message, ToastType Type, DateTime CreatedAt);
