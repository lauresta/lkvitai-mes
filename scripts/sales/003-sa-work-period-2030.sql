-- Owner approved on 2026-10-31: extend sa's period through 2030 as for d.bykovas.
-- Explicit shared production setting change, not an automatic app/CI migration.
-- Only sa.IndividualPeriodLastDate changes; the unchanged legacy trigger also
-- logs the update and sends its normal three-recipient administrative notice.
-- Owner also approved the normal administrative notification on 2026-10-31.
-- No impersonation or trigger changes.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 5000;
-- The owner approved the normal notification; preserve database-native semantics.
SET CONCAT_NULL_YIELDS_NULL OFF;
BEGIN TRY
    BEGIN TRANSACTION;
    IF DB_NAME()<>N'LKVITAIDBV1SQL'
        THROW 51000, 'Unexpected Sales work-period database', 1;
    IF NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
        AND database_guid='31FC1889-3D3E-4422-A4E3-362899649FF5')
        THROW 51000, 'Unexpected Sales work-period database identity', 1;
    -- Keep the reviewed legacy notification and audit trigger enabled.
    IF (SELECT COUNT(*) FROM sys.triggers WHERE parent_id=OBJECT_ID(N'dbo.SYS_UsersWorkPeriods') AND is_disabled=0)<>1
        THROW 51000, 'Unexpected work-period trigger set', 1;
    IF NOT EXISTS(SELECT 1 FROM sys.triggers WHERE object_id=OBJECT_ID(N'dbo.trg_UpdateUserWorkPeriod')
        AND parent_id=OBJECT_ID(N'dbo.SYS_UsersWorkPeriods') AND is_disabled=0)
        THROW 51000, 'Reviewed work-period trigger missing or disabled', 1;
    DECLARE @trigger varbinary(max)=CONVERT(varbinary(max),OBJECT_DEFINITION(OBJECT_ID(N'dbo.trg_UpdateUserWorkPeriod')));
    -- SQL2014 HASHBYTES supports at most8000 input bytes; hash both exact chunks.
    IF @trigger IS NULL OR DATALENGTH(@trigger)<>10948
        OR HASHBYTES('SHA2_256',SUBSTRING(@trigger,1,8000))<>0xd15dddbab8e2a6be29f32dd1fba7a134263f1907ac6757631898d8a5618e2e13
        OR HASHBYTES('SHA2_256',SUBSTRING(@trigger,8001,8000))<>0x7a615cdcb3ed1bd72b28ef54e2a960d9b9ad2672c7dd9ca93b55db50370aa11f
        THROW 51000, 'Work-period notification/audit trigger changed since review', 1;
    -- The reviewed legacy database has CONCAT_NULL_YIELDS_NULL OFF. Its unchanged
    -- trigger concatenates the three standard recipients with nullable sEmail.
    IF CONVERT(int,DATABASEPROPERTYEX(DB_NAME(),'IsNullConcat'))<>0
        THROW 51000, 'Legacy recipient-concatenation setting changed since review', 1;
    IF NOT EXISTS(SELECT 1 FROM sys.databases WHERE database_id=DB_ID() AND is_recursive_triggers_on=0)
        THROW 51000, 'Unexpected recursive work-period trigger setting', 1;
    IF (SELECT COUNT_BIG(*) FROM dbo.SYS_UsersWorkPeriods WITH(UPDLOCK,HOLDLOCK) WHERE sUserName=N'sa')<>1
        THROW 51000, 'Expected one exact sa work-period row', 1;
    IF NOT EXISTS(SELECT 1 FROM dbo.SYS_UsersWorkPeriods WHERE sUserName=N'sa'
        AND TypeOfWorkPeriod=2 AND IndividualPeriodFirstDate=CONVERT(datetime,'20010101',112)
        AND IndividualPeriodLastDate IN(CONVERT(datetime,'20260101',112),CONVERT(datetime,'20300131',112))
        AND MoovingPeriodDaysBeforeNow IS NULL AND MoovingPeriodDaysAfterNow IS NULL AND sEmail IS NULL)
        THROW 51000, 'sa work-period state differs from the reviewed baseline', 1;
    IF CONVERT(date,GETDATE())>CONVERT(date,'20300131',112)
        THROW 51000, 'Approved work-period end no longer covers current SQL date', 1;
    IF EXISTS(SELECT 1 FROM dbo.SYS_UsersWorkPeriods WHERE sUserName=N'sa'
        AND IndividualPeriodLastDate=CONVERT(datetime,'20300131',112))
    BEGIN
        COMMIT TRANSACTION;
        SELECT N'Already extended; no update or notification repeated' AS Result;
        RETURN;
    END;
    UPDATE dbo.SYS_UsersWorkPeriods SET IndividualPeriodLastDate=CONVERT(datetime,'20300131',112)
    WHERE sUserName=N'sa' AND IndividualPeriodLastDate=CONVERT(datetime,'20260101',112);
    DECLARE @changed int=@@ROWCOUNT;
    IF @changed<>1 THROW 51000, 'Unexpected work-period update count', 1;
    IF SYSTEM_USER=N'sa' AND dbo.fn_IsNowMyWorkPeriod(CONVERT(date,GETDATE()))<>'Y'
        THROW 51000, 'Current sa payment date is still outside its work period', 1;
    COMMIT TRANSACTION;
    SELECT sUserName,TypeOfWorkPeriod,IndividualPeriodFirstDate,IndividualPeriodLastDate,
        @changed ChangedRows FROM dbo.SYS_UsersWorkPeriods WHERE sUserName=N'sa';
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
