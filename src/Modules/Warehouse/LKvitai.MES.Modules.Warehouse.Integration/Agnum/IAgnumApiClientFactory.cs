namespace LKvitai.MES.Modules.Warehouse.Integration.Agnum;

public interface IAgnumApiClientFactory
{
    IAgnumApiClient GetForSndId(int sndId);
}
