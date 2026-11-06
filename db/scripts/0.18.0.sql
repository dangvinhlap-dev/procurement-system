START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261105061651_V0_18_0_NganSachBoPhan') THEN
    ALTER TABLE "PurchaseRequest" ADD "BudgetAmount" numeric(19,2);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261105061651_V0_18_0_NganSachBoPhan') THEN
    ALTER TABLE "PurchaseRequest" ADD "BudgetUsedAmount" numeric(19,2);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261105061651_V0_18_0_NganSachBoPhan') THEN
    ALTER TABLE "PurchaseRequest" ADD "IsOverBudget" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261105061651_V0_18_0_NganSachBoPhan') THEN
    ALTER TABLE "PurchaseOrder" ADD "BudgetAmount" numeric(19,2);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261105061651_V0_18_0_NganSachBoPhan') THEN
    ALTER TABLE "PurchaseOrder" ADD "BudgetUsedAmount" numeric(19,2);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261105061651_V0_18_0_NganSachBoPhan') THEN
    ALTER TABLE "PurchaseOrder" ADD "IsOverBudget" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261105061651_V0_18_0_NganSachBoPhan') THEN
    CREATE TABLE "DepartmentBudget" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "DepartmentId" uuid NOT NULL,
        "Year" integer NOT NULL,
        "Month" integer NOT NULL,
        "Amount" numeric(19,2) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_DepartmentBudget" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_DepartmentBudget_Department_DepartmentId_CompanyId" FOREIGN KEY ("DepartmentId", "CompanyId") REFERENCES "Department" ("Id", "CompanyId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261105061651_V0_18_0_NganSachBoPhan') THEN
    CREATE UNIQUE INDEX "IX_DepartmentBudget_CompanyId_DepartmentId_Year_Month" ON "DepartmentBudget" ("CompanyId", "DepartmentId", "Year", "Month");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261105061651_V0_18_0_NganSachBoPhan') THEN
    CREATE INDEX "IX_DepartmentBudget_DepartmentId_CompanyId" ON "DepartmentBudget" ("DepartmentId", "CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261105061651_V0_18_0_NganSachBoPhan') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261105061651_V0_18_0_NganSachBoPhan', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261105063638_V0_18_0_BatBuocHoSoTrinh') THEN
    ALTER TABLE "CompanyParameter" ADD "RequireRfqDossierComplete" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261105063638_V0_18_0_BatBuocHoSoTrinh') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261105063638_V0_18_0_BatBuocHoSoTrinh', '10.0.14');
    END IF;
END $EF$;
COMMIT;

