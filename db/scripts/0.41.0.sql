START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261118011349_V0_41_0_ChuKyLogoNhatKyIn') THEN
    ALTER TABLE "User" ADD "Honorific" character varying(30);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261118011349_V0_41_0_ChuKyLogoNhatKyIn') THEN
    CREATE TABLE "CompanyLogo" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "Image" bytea NOT NULL,
        "ContentType" character varying(20) NOT NULL,
        "Width" integer NOT NULL,
        "Height" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_CompanyLogo" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CompanyLogo_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261118011349_V0_41_0_ChuKyLogoNhatKyIn') THEN
    CREATE TABLE "DocumentPrintLog" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "DocType" character varying(10) NOT NULL,
        "DocumentId" uuid NOT NULL,
        "PrintNo" integer NOT NULL,
        "Kind" character varying(20) NOT NULL,
        "PrintedBy" uuid NOT NULL,
        "PrintedAt" timestamp with time zone NOT NULL,
        "ApprovalStatus" character varying(200) NOT NULL,
        CONSTRAINT "PK_DocumentPrintLog" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_DocumentPrintLog_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261118011349_V0_41_0_ChuKyLogoNhatKyIn') THEN
    CREATE TABLE "UserSignature" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "VersionNo" integer NOT NULL,
        "Image" bytea NOT NULL,
        "Width" integer NOT NULL,
        "Height" integer NOT NULL,
        "ValidFrom" timestamp with time zone NOT NULL,
        "ValidTo" timestamp with time zone,
        "UploadedBy" uuid NOT NULL,
        "ByAdmin" boolean NOT NULL,
        "Reason" character varying(500),
        "RetiredBy" uuid,
        "RetireReason" character varying(500),
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_UserSignature" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_UserSignature_User_UserId" FOREIGN KEY ("UserId") REFERENCES "User" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261118011349_V0_41_0_ChuKyLogoNhatKyIn') THEN
    CREATE UNIQUE INDEX "IX_CompanyLogo_CompanyId" ON "CompanyLogo" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261118011349_V0_41_0_ChuKyLogoNhatKyIn') THEN
    CREATE INDEX "IX_DocumentPrintLog_CompanyId" ON "DocumentPrintLog" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261118011349_V0_41_0_ChuKyLogoNhatKyIn') THEN
    CREATE UNIQUE INDEX "IX_DocumentPrintLog_DocType_DocumentId_PrintNo" ON "DocumentPrintLog" ("DocType", "DocumentId", "PrintNo");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261118011349_V0_41_0_ChuKyLogoNhatKyIn') THEN
    CREATE UNIQUE INDEX "IX_UserSignature_UserId_Current" ON "UserSignature" ("UserId") WHERE "ValidTo" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261118011349_V0_41_0_ChuKyLogoNhatKyIn') THEN
    CREATE UNIQUE INDEX "IX_UserSignature_UserId_VersionNo" ON "UserSignature" ("UserId", "VersionNo");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261118011349_V0_41_0_ChuKyLogoNhatKyIn') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261118011349_V0_41_0_ChuKyLogoNhatKyIn', '10.0.14');
    END IF;
END $EF$;
COMMIT;

