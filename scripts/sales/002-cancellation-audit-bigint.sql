-- DEFERRED BY OWNER on 2026-10-31: leave cancellation disabled; do not execute for current M2.
-- OWNER APPROVAL REQUIRED: this changes a shared legacy audit table for ALL branches.
-- It widens one identifier column only; no row deletion, ID reseed, trigger or procedure change.
-- Reviewed SQL Server 2014 target. Existing cancellation trigger behavior remains enabled.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 5000;

BEGIN TRY
    BEGIN TRANSACTION;
    IF DB_NAME()<>N'LKVITAIDBV1SQL'
        THROW 51000, 'Unexpected cancellation audit database', 1;
    IF NOT EXISTS(SELECT 1 FROM sys.database_recovery_status WHERE database_id=DB_ID()
        AND database_guid='31FC1889-3D3E-4422-A4E3-362899649FF5')
        THROW 51000, 'Unexpected cancellation audit database identity', 1;

    DECLARE @table int=OBJECT_ID(N'dbo.AUDIT_Anuliavimai',N'U');
    IF @table IS NULL THROW 51000, 'Cancellation audit table is missing', 1;
    DECLARE @column int,@systemType int,@userType int,@nullable bit,@computed bit,@identity bit,@default int;
    SELECT @column=column_id,@systemType=system_type_id,@userType=user_type_id,
        @nullable=is_nullable,@computed=is_computed,@identity=is_identity,@default=default_object_id
    FROM sys.columns WHERE object_id=@table AND name=N'OrderID';
    IF @column IS NULL THROW 51000, 'Cancellation audit OrderID is missing', 1;
    IF @nullable<>0 OR @computed<>0 OR @identity<>0 OR @userType<>@systemType
        THROW 51000, 'Unexpected cancellation audit OrderID column shape or type alias', 1;
    IF @systemType NOT IN(56,127)
        THROW 51000, 'Cancellation audit OrderID must currently be int or bigint', 1;

    DECLARE @rowsBefore bigint,@rowsAfter bigint;
    -- Bound lock acquisition avoids an unbounded wait on the shared audit table.
    SELECT @rowsBefore=COUNT_BIG(*) FROM dbo.AUDIT_Anuliavimai WITH(TABLOCKX,HOLDLOCK);
    IF @systemType=127
    BEGIN
        COMMIT TRANSACTION;
        SELECT N'Already bigint; no change' AS Result,@rowsBefore AS PreservedRows;
        RETURN;
    END;

    IF @default<>0 THROW 51000, 'Unreviewed OrderID default constraint', 1;
    IF EXISTS(SELECT 1 FROM sys.index_columns WHERE object_id=@table AND column_id=@column)
        THROW 51000, 'Unreviewed OrderID index dependency', 1;
    IF EXISTS(SELECT 1 FROM sys.stats_columns WHERE object_id=@table AND column_id=@column)
        THROW 51000, 'Unreviewed OrderID statistics dependency', 1;
    IF EXISTS(SELECT 1 FROM sys.foreign_key_columns
        WHERE(parent_object_id=@table AND parent_column_id=@column)
           OR(referenced_object_id=@table AND referenced_column_id=@column))
        THROW 51000, 'Unreviewed OrderID foreign-key dependency', 1;
    IF EXISTS(SELECT 1 FROM sys.check_constraints WHERE parent_object_id=@table)
        THROW 51000, 'Unreviewed audit-table CHECK constraint', 1;
    IF EXISTS(SELECT 1 FROM sys.computed_columns WHERE object_id=@table)
        THROW 51000, 'Unreviewed audit-table computed column', 1;
    IF EXISTS(SELECT 1 FROM sys.fulltext_index_columns WHERE object_id=@table AND column_id=@column)
        THROW 51000, 'Unreviewed OrderID full-text dependency', 1;

    DECLARE @writer int=OBJECT_ID(N'dbo.trg_ChangeOrder',N'TR');
    IF @writer IS NULL OR NOT EXISTS(SELECT 1 FROM sys.triggers
        WHERE object_id=@writer AND parent_id=OBJECT_ID(N'dbo.Uzsakymai',N'U') AND is_disabled=0)
        THROW 51000, 'Reviewed cancellation audit writer is missing or disabled', 1;
    IF EXISTS(SELECT 1 FROM sys.sql_expression_dependencies
        WHERE referenced_id=@table AND (is_schema_bound_reference=1 OR referencing_id<>@writer))
        THROW 51000, 'Unreviewed audit-table SQL reader or schema-bound dependency', 1;
    -- Also reject a newly introduced literal SQL reader that catalog dependencies omit.
    IF EXISTS(SELECT 1 FROM sys.sql_modules WHERE object_id<>@writer
        AND definition LIKE N'%AUDIT[_]Anuliavimai%')
        THROW 51000, 'Unreviewed audit-table SQL module reference', 1;

    EXEC(N'ALTER TABLE dbo.AUDIT_Anuliavimai ALTER COLUMN OrderID bigint NOT NULL');
    IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE object_id=@table AND column_id=@column
        AND system_type_id=127 AND user_type_id=127 AND is_nullable=0)
        THROW 51000, 'Cancellation audit widening verification failed', 1;
    SELECT @rowsAfter=COUNT_BIG(*) FROM dbo.AUDIT_Anuliavimai;
    IF @rowsAfter<>@rowsBefore THROW 51000, 'Audit row count changed unexpectedly', 1;
    COMMIT TRANSACTION;
    SELECT N'OrderID widened to bigint; existing audit rows preserved' AS Result,@rowsAfter AS PreservedRows;
END TRY
BEGIN CATCH
    IF XACT_STATE()<>0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
