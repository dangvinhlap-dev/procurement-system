START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261119093701_V0_45_0_GopBuocTruongPhong') THEN
    ALTER TABLE "ApprovalStep" ADD "MergedWithCreator" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261119093701_V0_45_0_GopBuocTruongPhong') THEN
    CREATE TABLE "DepartmentDeputy" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "DepartmentId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_DepartmentDeputy" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_DepartmentDeputy_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_DepartmentDeputy_Department_DepartmentId_CompanyId" FOREIGN KEY ("DepartmentId", "CompanyId") REFERENCES "Department" ("Id", "CompanyId") ON DELETE RESTRICT,
        CONSTRAINT "FK_DepartmentDeputy_User_UserId" FOREIGN KEY ("UserId") REFERENCES "User" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261119093701_V0_45_0_GopBuocTruongPhong') THEN
    CREATE INDEX "IX_DepartmentDeputy_CompanyId" ON "DepartmentDeputy" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261119093701_V0_45_0_GopBuocTruongPhong') THEN
    CREATE INDEX "IX_DepartmentDeputy_DepartmentId_CompanyId" ON "DepartmentDeputy" ("DepartmentId", "CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261119093701_V0_45_0_GopBuocTruongPhong') THEN
    CREATE UNIQUE INDEX "IX_DepartmentDeputy_DepartmentId_UserId" ON "DepartmentDeputy" ("DepartmentId", "UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261119093701_V0_45_0_GopBuocTruongPhong') THEN
    CREATE INDEX "IX_DepartmentDeputy_UserId" ON "DepartmentDeputy" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261119093701_V0_45_0_GopBuocTruongPhong') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261119093701_V0_45_0_GopBuocTruongPhong', '10.0.14');
    END IF;
END $EF$;
COMMIT;

