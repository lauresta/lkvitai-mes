namespace LKvitai.MES.Modules.Warehouse.Integration.Agnum;

public sealed class AgnumApiUnavailableException : Exception
{
    public AgnumApiUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
