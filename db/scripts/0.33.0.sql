START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114075445_V0_31_0_GoPhanHeKho') THEN
    DELETE FROM "RolePermission" WHERE "PermissionCode" IN (
        'Inventory.Document.View', 'Inventory.Document.Create', 'Inventory.Document.Post', 'Inventory.Document.Unpost',
        'Inventory.Adjustment.Create', 'Inventory.Adjustment.Post', 'Inventory.Receipt.Accept', 'Inventory.Stock.View',
        'Inventory.Period.View', 'Inventory.Period.Open', 'Inventory.Period.Close', 'Inventory.Period.Reopen',
        'Report.Stock.View', 'Report.Stock.ViewAllCompanies', 'Report.Accounting.Export');
    DELETE FROM "Attachment" WHERE "OwnerType" = 'InventoryDocument';
    DELETE FROM "DocumentSequence" WHERE "DocType" IN ('GRN', 'RTV', 'TRF', 'ICS', 'ICR', 'ADJ');
    DELETE FROM "DocumentNumbering" WHERE "DocType" IN ('GRN', 'RTV', 'TRF', 'ICS', 'ICR', 'ADJ');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114075445_V0_31_0_GoPhanHeKho') THEN
    DROP TABLE "ItemPeriodCost";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114075445_V0_31_0_GoPhanHeKho') THEN
    DROP TABLE "StockLedger";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114075445_V0_31_0_GoPhanHeKho') THEN
    DROP TABLE "StockOnHand";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114075445_V0_31_0_GoPhanHeKho') THEN
    DROP TABLE "StockPeriodBalance";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114075445_V0_31_0_GoPhanHeKho') THEN
    DROP TABLE "InventoryDocumentLine";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114075445_V0_31_0_GoPhanHeKho') THEN
    DROP TABLE "InventoryDocument";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114075445_V0_31_0_GoPhanHeKho') THEN
    DROP TABLE "InventoryPeriod";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114075445_V0_31_0_GoPhanHeKho') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261114075445_V0_31_0_GoPhanHeKho', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114095551_V0_32_0_NhatKyVanHanh') THEN
    CREATE TABLE "OperationLog" (
        "Id" uuid NOT NULL,
        "OccurredAt" timestamp with time zone NOT NULL,
        "Environment" character varying(10) NOT NULL,
        "UserId" uuid,
        "UserName" character varying(256),
        "Kind" character varying(50) NOT NULL,
        "Target" character varying(500),
        "Reason" character varying(500),
        "Result" character varying(20) NOT NULL,
        "Detail" character varying(4000),
        CONSTRAINT "PK_OperationLog" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114095551_V0_32_0_NhatKyVanHanh') THEN
    CREATE INDEX "IX_OperationLog_Kind_OccurredAt" ON "OperationLog" ("Kind", "OccurredAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114095551_V0_32_0_NhatKyVanHanh') THEN
    CREATE INDEX "IX_OperationLog_OccurredAt" ON "OperationLog" ("OccurredAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114095551_V0_32_0_NhatKyVanHanh') THEN
    CREATE TRIGGER "TR_OperationLog_ChiThem" BEFORE UPDATE OR DELETE ON "OperationLog"
        FOR EACH ROW EXECUTE FUNCTION log_prevent_change();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114095551_V0_32_0_NhatKyVanHanh') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261114095551_V0_32_0_NhatKyVanHanh', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115002929_V0_32_0_NhatKyLoi') THEN
    ALTER TABLE "CompanyParameter" ADD "ErrorLogKeepDays" integer NOT NULL DEFAULT 90;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115002929_V0_32_0_NhatKyLoi') THEN
    CREATE TABLE "ErrorGroup" (
        "Id" uuid NOT NULL,
        "Fingerprint" character varying(64) NOT NULL,
        "Source" character varying(10) NOT NULL,
        "ErrorType" character varying(200) NOT NULL,
        "Message" character varying(1000) NOT NULL,
        "Location" character varying(1000),
        "Path" character varying(500),
        "FirstSeenAt" timestamp with time zone NOT NULL,
        "LastSeenAt" timestamp with time zone NOT NULL,
        "OccurrenceCount" integer NOT NULL,
        "Status" character varying(20) NOT NULL,
        "HandledByName" character varying(256),
        "HandledAt" timestamp with time zone,
        "Note" character varying(2000),
        "ReopenCount" integer NOT NULL,
        CONSTRAINT "PK_ErrorGroup" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115002929_V0_32_0_NhatKyLoi') THEN
    CREATE TABLE "ErrorOccurrence" (
        "Id" uuid NOT NULL,
        "GroupId" uuid NOT NULL,
        "OccurredAt" timestamp with time zone NOT NULL,
        "TrackingCode" character varying(20) NOT NULL,
        "Source" character varying(10) NOT NULL,
        "UserId" uuid,
        "UserName" character varying(256),
        "CompanyId" uuid,
        "CompanyCode" character varying(50),
        "Path" character varying(500),
        "Method" character varying(10),
        "Message" character varying(1000) NOT NULL,
        "Detail" character varying(16384),
        "Version" character varying(50),
        "Environment" character varying(10),
        "UserAgent" character varying(500),
        CONSTRAINT "PK_ErrorOccurrence" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ErrorOccurrence_ErrorGroup_GroupId" FOREIGN KEY ("GroupId") REFERENCES "ErrorGroup" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115002929_V0_32_0_NhatKyLoi') THEN
    CREATE UNIQUE INDEX "IX_ErrorGroup_Fingerprint" ON "ErrorGroup" ("Fingerprint");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115002929_V0_32_0_NhatKyLoi') THEN
    CREATE INDEX "IX_ErrorGroup_LastSeenAt" ON "ErrorGroup" ("LastSeenAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115002929_V0_32_0_NhatKyLoi') THEN
    CREATE INDEX "IX_ErrorOccurrence_GroupId_OccurredAt" ON "ErrorOccurrence" ("GroupId", "OccurredAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115002929_V0_32_0_NhatKyLoi') THEN
    CREATE INDEX "IX_ErrorOccurrence_OccurredAt" ON "ErrorOccurrence" ("OccurredAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115002929_V0_32_0_NhatKyLoi') THEN
    CREATE INDEX "IX_ErrorOccurrence_TrackingCode" ON "ErrorOccurrence" ("TrackingCode");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115002929_V0_32_0_NhatKyLoi') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261115002929_V0_32_0_NhatKyLoi', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115025307_V0_33_0_SaoLuu') THEN
    ALTER TABLE "CompanyParameter" ADD "BackupFullDay" integer NOT NULL DEFAULT 7;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115025307_V0_33_0_SaoLuu') THEN
    ALTER TABLE "CompanyParameter" ADD "BackupKeepWeeks" integer NOT NULL DEFAULT 4;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115025307_V0_33_0_SaoLuu') THEN
    ALTER TABLE "CompanyParameter" ADD "BackupTime" time without time zone NOT NULL DEFAULT TIME '01:00:00';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115025307_V0_33_0_SaoLuu') THEN
    ALTER TABLE "CompanyParameter" ADD "DumpKeepDays" integer NOT NULL DEFAULT 14;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115025307_V0_33_0_SaoLuu') THEN
    CREATE TABLE "BackupRun" (
        "Id" uuid NOT NULL,
        "Kind" character varying(20) NOT NULL,
        "Trigger" character varying(20) NOT NULL,
        "Status" character varying(20) NOT NULL,
        "ScheduledDate" date,
        "RequestedByName" character varying(256),
        "QueuedAt" timestamp with time zone NOT NULL,
        "StartedAt" timestamp with time zone,
        "FinishedAt" timestamp with time zone,
        "RelativePath" character varying(500),
        "SizeBytes" bigint,
        "FileCount" integer,
        "Sha256" character varying(64),
        "BaseFullRunId" uuid,
        "StartWalFile" character varying(24),
        "Verified" boolean,
        "VerifyMessage" character varying(4000),
        "SecondaryCopied" boolean,
        "SecondaryMessage" character varying(4000),
        "ErrorMessage" character varying(4000),
        "Note" character varying(4000),
        "PurgedAt" timestamp with time zone,
        CONSTRAINT "PK_BackupRun" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115025307_V0_33_0_SaoLuu') THEN
    CREATE UNIQUE INDEX "IX_BackupRun_Kind_ScheduledDate" ON "BackupRun" ("Kind", "ScheduledDate") WHERE "ScheduledDate" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115025307_V0_33_0_SaoLuu') THEN
    CREATE INDEX "IX_BackupRun_QueuedAt" ON "BackupRun" ("QueuedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115025307_V0_33_0_SaoLuu') THEN
    CREATE INDEX "IX_BackupRun_Status_QueuedAt" ON "BackupRun" ("Status", "QueuedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115025307_V0_33_0_SaoLuu') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261115025307_V0_33_0_SaoLuu', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115034711_V0_33_0_BaoTriCsdl') THEN
    ALTER TABLE "CompanyParameter" ADD "DbMaintenanceDay" integer NOT NULL DEFAULT 7;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115034711_V0_33_0_BaoTriCsdl') THEN
    ALTER TABLE "CompanyParameter" ADD "DbMaintenanceTime" time without time zone NOT NULL DEFAULT TIME '03:00:00';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115034711_V0_33_0_BaoTriCsdl') THEN
    CREATE TABLE "MaintenanceRun" (
        "Id" uuid NOT NULL,
        "Kind" character varying(30) NOT NULL,
        "Trigger" character varying(20) NOT NULL,
        "Status" character varying(20) NOT NULL,
        "Target" character varying(300),
        "SessionPid" integer,
        "Reason" character varying(500),
        "ScheduledDate" date,
        "RequestedByName" character varying(256),
        "QueuedAt" timestamp with time zone NOT NULL,
        "StartedAt" timestamp with time zone,
        "FinishedAt" timestamp with time zone,
        "SizeBeforeBytes" bigint,
        "SizeAfterBytes" bigint,
        "Note" character varying(4000),
        "ErrorMessage" character varying(4000),
        CONSTRAINT "PK_MaintenanceRun" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115034711_V0_33_0_BaoTriCsdl') THEN
    CREATE UNIQUE INDEX "IX_MaintenanceRun_Kind_ScheduledDate" ON "MaintenanceRun" ("Kind", "ScheduledDate") WHERE "ScheduledDate" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115034711_V0_33_0_BaoTriCsdl') THEN
    CREATE INDEX "IX_MaintenanceRun_QueuedAt" ON "MaintenanceRun" ("QueuedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115034711_V0_33_0_BaoTriCsdl') THEN
    CREATE INDEX "IX_MaintenanceRun_Status_QueuedAt" ON "MaintenanceRun" ("Status", "QueuedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115034711_V0_33_0_BaoTriCsdl') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261115034711_V0_33_0_BaoTriCsdl', '10.0.14');
    END IF;
END $EF$;
COMMIT;

