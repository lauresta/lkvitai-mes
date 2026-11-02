using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
namespace LKvitai.MES.Modules.Sales.Infrastructure.Sql;
public sealed partial class SqlSalesOrderEntryService
{
    public Task<OrderEntryCommandResult> SaveAddressAsync(Guid actorId,long orderId,SaveOrderAddressCommand command,CancellationToken ct)
        => Execute(actorId,command.CommandId,"address.save.v1",orderId,command.ExpectedRevision,command,async(c,tx,before,token)=>
        {
            var a=command.Address;
            if(a is null)throw new OrderEntryException("ADDRESS_REQUIRED","Address is required.",400);
            if(a.Building?.Length>12||a.Room?.Length>12||a.Phone?.Length>64||a.Note?.Length>512)throw new OrderEntryException("ADDRESS_LENGTH","An address field exceeds its legacy limit.");
            await RequireExists(c,tx,"SELECT 1 FROM dbo.DIC_Addresses WHERE iID=@id",a.StreetId,"Street",token);
            if(before!.Address is null)
                await NonQuery(c,tx,"INSERT dbo.TCR_OrderAddresses(iOrderID,iStreetID,sBuildingNo,sRoomNo,sPhoneNo,sNotes) VALUES(@order,@street,@building,@room,@phone,@note)",token,("@order",orderId),("@street",a.StreetId),("@building",a.Building),("@room",a.Room),("@phone",a.Phone),("@note",a.Note));
            else if(before.Address!=a)
                await NonQuery(c,tx,"UPDATE dbo.TCR_OrderAddresses SET iStreetID=@street,sBuildingNo=@building,sRoomNo=@room,sPhoneNo=@phone,sNotes=@note WHERE iOrderID=@order",token,("@order",orderId),("@street",a.StreetId),("@building",a.Building),("@room",a.Room),("@phone",a.Phone),("@note",a.Note));
            return(orderId,(long?)null);
        },ct);
    public Task<OrderEntryCommandResult> SaveDutyAsync(Guid actorId,long orderId,SaveOrderDutyCommand command,CancellationToken ct)
        => Execute(actorId,command.CommandId,"duty.save.v1",orderId,command.ExpectedRevision,command,async(c,tx,before,token)=>
        {
            var duty=command.Duty;
            if(duty is null)throw new OrderEntryException("DUTY_REQUIRED","Duty is required.",400);
            if(duty.Date<new DateOnly(1900,1,1)||duty.Date>new DateOnly(2079,6,6))throw new OrderEntryException("DUTY_DATE","Duty date exceeds legacy smalldatetime storage.");
            if(duty.Note?.Length>512)throw new OrderEntryException("DUTY_NOTE","Duty note is limited to 512 characters.");
            await RequireExists(c,tx,"SELECT 1 FROM dbo.TBD_Duties WHERE iDutyID=@id",duty.DutyId,"Duty",token);
            var old=before!.Duties.SingleOrDefault(d=>d.DutyId==duty.DutyId&&d.EmployeeId==duty.EmployeeId);
            if(old is null)await RequireExists(c,tx,"SELECT 1 FROM dbo.Zinynas_darbuotojai e WHERE e.DarbuotojoID=@id AND e.bHideInApplicationFlag=0 AND EXISTS(SELECT 1 FROM dbo.Loc_leidimas() access WHERE access.UKS=e.FilialasID)",duty.EmployeeId,"Employee",token);
            if(old is null)
                await NonQuery(c,tx,"INSERT dbo.TCR_OrdersDutiesEmployees(iOrderID,iDutyID,iEmployeeID,dtDate,sNotes) VALUES(@order,@duty,@employee,@date,@note)",token,("@order",orderId),("@duty",duty.DutyId),("@employee",duty.EmployeeId),("@date",duty.Date.ToDateTime(TimeOnly.MinValue)),("@note",duty.Note));
            else if(DateOnly.FromDateTime(old.Date)!=duty.Date||old.Note!=duty.Note)
            {
                var locked=await Scalar(c,tx,"SELECT 1 FROM dbo.TCR_OrdersDutiesEmployees WHERE iOrderID=@order AND iDutyID=@duty AND iEmployeeID=@employee AND dtAccuiranceDate>='20000105' AND GETDATE()>DATEADD(day,6,DATEADD(month,DATEDIFF(month,0,dtAccuiranceDate)+1,0))",token,("@order",orderId),("@duty",duty.DutyId),("@employee",duty.EmployeeId));
                if(locked is not null)throw new OrderEntryException("DUTY_POSTED","This duty assignment is outside the editable acquisition period.",409);
                await NonQuery(c,tx,"UPDATE dbo.TCR_OrdersDutiesEmployees SET dtDate=@date,sNotes=@note WHERE iOrderID=@order AND iDutyID=@duty AND iEmployeeID=@employee",token,("@order",orderId),("@duty",duty.DutyId),("@employee",duty.EmployeeId),("@date",duty.Date.ToDateTime(TimeOnly.MinValue)),("@note",duty.Note));
            }
            return(orderId,(long?)null);
        },ct);
}
