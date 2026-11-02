-- Explicit SQL Server 2014 deployment. No legacy tables/procedures/triggers changed.
-- Execute only against the reviewed LKVITAIDBV1SQL production catalog.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF DB_NAME() <> N'LKVITAIDBV1SQL'
    THROW 51000, 'Unexpected Sales database', 1;
IF NOT EXISTS (SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
               AND database_guid='31FC1889-3D3E-4422-A4E3-362899649FF5')
    THROW 51000, 'Unexpected Sales database identity', 1;
IF SCHEMA_ID(N'mes_sales') IS NULL EXEC(N'CREATE SCHEMA mes_sales');
IF OBJECT_ID(N'mes_sales.CommandReceipt', N'U') IS NULL
BEGIN
    CREATE TABLE mes_sales.CommandReceipt
    (
        ActorId uniqueidentifier NOT NULL,
        CommandId uniqueidentifier NOT NULL,
        Operation varchar(64) NOT NULL,
        RequestHash binary(32) NOT NULL,
        OrderId bigint NOT NULL,
        ProductId bigint NULL,
        SeriesId int NOT NULL,
        ResultRevision varchar(64) NOT NULL,
        CommittedAtUtc datetime2(3) NOT NULL,
        SqlLogin nvarchar(128) NOT NULL,
        BuildVersion varchar(64) NOT NULL,
        CONSTRAINT PK_SalesCommandReceipt PRIMARY KEY CLUSTERED(ActorId,CommandId)
    );
END;
IF COL_LENGTH(N'mes_sales.CommandReceipt',N'RelatedEntityId') IS NULL
    ALTER TABLE mes_sales.CommandReceipt ADD RelatedEntityId bigint NULL;
-- Server-generated operator warnings survive unknown-result recovery. SQL2014
-- stores the application JSON as text; no JSON functions or legacy DML needed.
IF COL_LENGTH(N'mes_sales.CommandReceipt',N'WarningsJson') IS NULL
    ALTER TABLE mes_sales.CommandReceipt ADD WarningsJson nvarchar(max) NULL;
COMMIT TRANSACTION;
