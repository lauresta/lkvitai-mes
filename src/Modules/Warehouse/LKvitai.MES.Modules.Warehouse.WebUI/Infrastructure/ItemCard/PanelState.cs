namespace LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure.ItemCard;

/// <summary>A panel state that can be cleared when the page switches to another item.</summary>
public interface IPanelState
{
    void Reset();
}

/// <summary>
/// Load state of one item card panel: each panel loads on its own and shows its own progress and error,
/// so a failing block (e.g. reservations) never blanks the page. A failed reload keeps the last value.
/// </summary>
public sealed class PanelState<T> : IPanelState
{
    public bool IsLoading { get; private set; }

    public ApiException? Error { get; private set; }

    public T? Value { get; private set; }

    public bool HasValue { get; private set; }

    public async Task LoadAsync(Func<Task<T>> loader)
    {
        IsLoading = true;
        Error = null;

        try
        {
            Value = await loader();
            HasValue = true;
        }
        catch (ApiException ex)
        {
            Error = ex;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void ClearError() => Error = null;

    public void Reset()
    {
        Value = default;
        HasValue = false;
        Error = null;
        IsLoading = false;
    }
}
