START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115045740_V0_34_0_BoThucThi') THEN
    CREATE TABLE "AgentEnvironment" (
        "Id" uuid NOT NULL,
        "Code" character varying(10) NOT NULL,
        "Name" character varying(100) NOT NULL,
        "Address" character varying(300),
        "IsActive" boolean NOT NULL,
        "AgentKeyHash" character(64),
        "AgentKeyHint" character varying(8),
        "KeyCreatedAt" timestamp with time zone,
        "KeyCreatedByName" character varying(256),
        "LastSeenAt" timestamp with time zone,
        "AgentVersion" character varying(100),
        "AppVersion" character varying(100),
        "HostName" character varying(100),
        CONSTRAINT "PK_AgentEnvironment" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115045740_V0_34_0_BoThucThi') THEN
    CREATE TABLE "MaintenanceModeState" (
        "Id" uuid NOT NULL,
        "IsOn" boolean NOT NULL,
        "Message" character varying(500),
        "Reason" character varying(500),
        "StartedAt" timestamp with time zone,
        "ChangedByName" character varying(256),
        "ChangedAt" timestamp with time zone,
        CONSTRAINT "PK_MaintenanceModeState" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115045740_V0_34_0_BoThucThi') THEN
    CREATE TABLE "AgentCommand" (
        "Id" uuid NOT NULL,
        "EnvironmentId" uuid NOT NULL,
        "Kind" character varying(30) NOT NULL,
        "Parameters" character varying(4000),
        "Status" character varying(20) NOT NULL,
        "Reason" character varying(500) NOT NULL,
        "CreatedByName" character varying(256),
        "CreatedAt" timestamp with time zone NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "StartedAt" timestamp with time zone,
        "FinishedAt" timestamp with time zone,
        "ResultMessage" character varying(4000),
        "Journal" character varying(64000),
        "CancelledByName" character varying(256),
        CONSTRAINT "PK_AgentCommand" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AgentCommand_AgentEnvironment_EnvironmentId" FOREIGN KEY ("EnvironmentId") REFERENCES "AgentEnvironment" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115045740_V0_34_0_BoThucThi') THEN
    CREATE INDEX "IX_AgentCommand_CreatedAt" ON "AgentCommand" ("CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115045740_V0_34_0_BoThucThi') THEN
    CREATE INDEX "IX_AgentCommand_EnvironmentId_Status_CreatedAt" ON "AgentCommand" ("EnvironmentId", "Status", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115045740_V0_34_0_BoThucThi') THEN
    CREATE UNIQUE INDEX "IX_AgentCommand_OneRunning" ON "AgentCommand" ("EnvironmentId") WHERE "Status" = 'Running';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115045740_V0_34_0_BoThucThi') THEN
    CREATE UNIQUE INDEX "IX_AgentEnvironment_AgentKeyHash" ON "AgentEnvironment" ("AgentKeyHash") WHERE "AgentKeyHash" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115045740_V0_34_0_BoThucThi') THEN
    CREATE UNIQUE INDEX "IX_AgentEnvironment_Code" ON "AgentEnvironment" ("Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115045740_V0_34_0_BoThucThi') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261115045740_V0_34_0_BoThucThi', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115061913_V0_34_0_PhucHoi') THEN
    CREATE TABLE "RestoreRun" (
        "Id" uuid NOT NULL,
        "Kind" character varying(20) NOT NULL,
        "Status" character varying(20) NOT NULL,
        "DumpRunId" uuid,
        "FullRunId" uuid,
        "DiffRunId" uuid,
        "TargetTime" timestamp with time zone,
        "LiveDatabase" character varying(63) NOT NULL,
        "RestoreDatabase" character varying(63) NOT NULL,
        "OldDatabase" character varying(500),
        "RelativePath" character varying(500),
        "TempPort" integer,
        "Reason" character varying(500) NOT NULL,
        "RequestedByName" character varying(256),
        "QueuedAt" timestamp with time zone NOT NULL,
        "StartedAt" timestamp with time zone,
        "ReadyAt" timestamp with time zone,
        "SwapReason" character varying(500),
        "SwapRequestedByName" character varying(256),
        "SwapRequestedAt" timestamp with time zone,
        "FinishedAt" timestamp with time zone,
        "DiscardedByName" character varying(256),
        "AgentCommandId" uuid,
        "PlanJson" character varying(200000),
        "ComparisonJson" character varying(200000),
        "CanSwap" boolean,
        "Note" character varying(4000),
        "ErrorMessage" character varying(4000),
        CONSTRAINT "PK_RestoreRun" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115061913_V0_34_0_PhucHoi') THEN
    CREATE INDEX "IX_RestoreRun_AgentCommandId" ON "RestoreRun" ("AgentCommandId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115061913_V0_34_0_PhucHoi') THEN
    CREATE INDEX "IX_RestoreRun_QueuedAt" ON "RestoreRun" ("QueuedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115061913_V0_34_0_PhucHoi') THEN
    CREATE INDEX "IX_RestoreRun_Status" ON "RestoreRun" ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115061913_V0_34_0_PhucHoi') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261115061913_V0_34_0_PhucHoi', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115080346_V0_35_0_CauHinhTruyCap') THEN
    CREATE TABLE "AccessPolicy" (
        "Id" uuid NOT NULL,
        "MinPasswordLength" integer NOT NULL,
        "BlockCommonPasswords" boolean NOT NULL,
        "ExtraBlockedPasswords" character varying(4000),
        "MaxFailedLogins" integer NOT NULL,
        "LockoutMinutes" integer NOT NULL,
        "SessionMinutes" integer NOT NULL,
        "OpsAllowedIpRanges" character varying(2000),
        "LoginPerMinutePerIp" integer NOT NULL,
        "ApiPerMinutePerUser" integer NOT NULL,
        "HeavyConcurrentPerUser" integer NOT NULL,
        "SessionsRevokedAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_AccessPolicy" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115080346_V0_35_0_CauHinhTruyCap') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261115080346_V0_35_0_CauHinhTruyCap', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115092600_V0_36_0_TrungTamPhatHanh') THEN
    CREATE TABLE "ReleaseTicket" (
        "Id" uuid NOT NULL,
        "Version" character varying(32) NOT NULL,
        "Status" character varying(20) NOT NULL,
        "Source" character varying(20) NOT NULL,
        "SourceUrl" character varying(500),
        "PackageFileName" character varying(260) NOT NULL,
        "PackageSize" bigint NOT NULL,
        "Sha256" character varying(64) NOT NULL,
        "Changelog" character varying(20000),
        "DatabaseChanges" character varying(4000),
        "MinRollbackLevel" character varying(20) NOT NULL,
        "Risks" character varying(4000),
        "RollbackPlan" character varying(4000),
        "Note" character varying(4000),
        "ReceivedByName" character varying(256),
        "ReceivedAt" timestamp with time zone NOT NULL,
        "UatSignedByName" character varying(256),
        "UatSignedAt" timestamp with time zone,
        "UatComment" character varying(4000),
        "UatWithoutTest" boolean NOT NULL,
        "ProdInstalledAt" timestamp with time zone,
        "EndedAt" timestamp with time zone,
        "EndedByName" character varying(256),
        "EndReason" character varying(4000),
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_ReleaseTicket" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115092600_V0_36_0_TrungTamPhatHanh') THEN
    CREATE TABLE "ReleaseDefect" (
        "Id" uuid NOT NULL,
        "TicketId" uuid NOT NULL,
        "Title" character varying(300) NOT NULL,
        "Description" character varying(4000),
        "Screen" character varying(200),
        "ReporterName" character varying(256),
        "ReportedAt" timestamp with time zone NOT NULL,
        "Status" character varying(20) NOT NULL,
        "Resolution" character varying(4000),
        "ResolvedByName" character varying(256),
        "ResolvedAt" timestamp with time zone,
        CONSTRAINT "PK_ReleaseDefect" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ReleaseDefect_ReleaseTicket_TicketId" FOREIGN KEY ("TicketId") REFERENCES "ReleaseTicket" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115092600_V0_36_0_TrungTamPhatHanh') THEN
    CREATE TABLE "ReleaseInstall" (
        "Id" uuid NOT NULL,
        "TicketId" uuid NOT NULL,
        "Environment" character varying(10) NOT NULL,
        "Kind" character varying(20) NOT NULL,
        "AgentCommandId" uuid NOT NULL,
        "PreviousVersion" character varying(32),
        "TargetVersion" character varying(32),
        "Level" character varying(20),
        "Status" character varying(20) NOT NULL,
        "InstalledSha256" character varying(64),
        "Reason" character varying(4000),
        "ByName" character varying(256),
        "StartedAt" timestamp with time zone NOT NULL,
        "FinishedAt" timestamp with time zone,
        "Message" character varying(4000),
        CONSTRAINT "PK_ReleaseInstall" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ReleaseInstall_ReleaseTicket_TicketId" FOREIGN KEY ("TicketId") REFERENCES "ReleaseTicket" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115092600_V0_36_0_TrungTamPhatHanh') THEN
    CREATE TABLE "ReleaseTestItem" (
        "Id" uuid NOT NULL,
        "TicketId" uuid NOT NULL,
        "Seq" integer NOT NULL,
        "Section" character varying(100) NOT NULL,
        "Content" character varying(2000) NOT NULL,
        "Result" character varying(20) NOT NULL,
        "TesterName" character varying(256),
        "TestedAt" timestamp with time zone,
        "Comment" character varying(4000),
        CONSTRAINT "PK_ReleaseTestItem" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ReleaseTestItem_ReleaseTicket_TicketId" FOREIGN KEY ("TicketId") REFERENCES "ReleaseTicket" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115092600_V0_36_0_TrungTamPhatHanh') THEN
    CREATE INDEX "IX_ReleaseDefect_TicketId" ON "ReleaseDefect" ("TicketId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115092600_V0_36_0_TrungTamPhatHanh') THEN
    CREATE UNIQUE INDEX "IX_ReleaseInstall_AgentCommandId" ON "ReleaseInstall" ("AgentCommandId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115092600_V0_36_0_TrungTamPhatHanh') THEN
    CREATE INDEX "IX_ReleaseInstall_TicketId" ON "ReleaseInstall" ("TicketId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115092600_V0_36_0_TrungTamPhatHanh') THEN
    CREATE INDEX "IX_ReleaseTestItem_TicketId" ON "ReleaseTestItem" ("TicketId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115092600_V0_36_0_TrungTamPhatHanh') THEN
    CREATE UNIQUE INDEX "IX_ReleaseTicket_Version" ON "ReleaseTicket" ("Version") WHERE "Status" <> 'Cancelled';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261115092600_V0_36_0_TrungTamPhatHanh') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261115092600_V0_36_0_TrungTamPhatHanh', '10.0.14');
    END IF;
END $EF$;
COMMIT;

