START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101084519_V0_11_0_DanhMucBoPhan') THEN
    ALTER TABLE "PurchaseRequest" ADD "DepartmentId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101084519_V0_11_0_DanhMucBoPhan') THEN
    CREATE TABLE "Department" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "Code" character varying(20) NOT NULL,
        "Name" character varying(200) NOT NULL,
        "ManagerName" character varying(200),
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_Department" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_Department_Id_CompanyId" UNIQUE ("Id", "CompanyId"),
        CONSTRAINT "FK_Department_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101084519_V0_11_0_DanhMucBoPhan') THEN
    CREATE INDEX "IX_PurchaseRequest_DepartmentId_CompanyId" ON "PurchaseRequest" ("DepartmentId", "CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101084519_V0_11_0_DanhMucBoPhan') THEN
    CREATE UNIQUE INDEX "IX_Department_CompanyId_Code" ON "Department" ("CompanyId", "Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101084519_V0_11_0_DanhMucBoPhan') THEN
    ALTER TABLE "PurchaseRequest" ADD CONSTRAINT "FK_PurchaseRequest_Department_DepartmentId_CompanyId" FOREIGN KEY ("DepartmentId", "CompanyId") REFERENCES "Department" ("Id", "CompanyId") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101084519_V0_11_0_DanhMucBoPhan') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261101084519_V0_11_0_DanhMucBoPhan', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101094130_V0_11_0_DuyetNhaCungCap') THEN
    ALTER TABLE "ApprovalStep" ADD "Stage" integer NOT NULL DEFAULT 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101094130_V0_11_0_DuyetNhaCungCap') THEN
    CREATE TABLE "SupplierChangeRequest" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "No" character varying(50) NOT NULL,
        "SupplierId" uuid NOT NULL,
        "Kind" integer NOT NULL,
        "NewStatus" integer,
        "StatusNote" character varying(500),
        "BankAccountsJson" jsonb,
        "CurrentSummary" character varying(1000) NOT NULL,
        "ProposedSummary" character varying(1100) NOT NULL,
        "Status" integer NOT NULL,
        "ApprovalRound" integer NOT NULL,
        "RejectReason" character varying(500),
        "ApprovedAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_SupplierChangeRequest" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SupplierChangeRequest_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_SupplierChangeRequest_Supplier_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101094130_V0_11_0_DuyetNhaCungCap') THEN
    CREATE INDEX "IX_SupplierChangeRequest_CompanyId" ON "SupplierChangeRequest" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101094130_V0_11_0_DuyetNhaCungCap') THEN
    CREATE UNIQUE INDEX "IX_SupplierChangeRequest_No" ON "SupplierChangeRequest" ("No");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101094130_V0_11_0_DuyetNhaCungCap') THEN
    CREATE INDEX "IX_SupplierChangeRequest_SupplierId_Status" ON "SupplierChangeRequest" ("SupplierId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101094130_V0_11_0_DuyetNhaCungCap') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261101094130_V0_11_0_DuyetNhaCungCap', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101101508_V0_11_0_DuyetBangGiaNcc') THEN
    ALTER TABLE "SupplierPriceList" ADD "ApprovalRound" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101101508_V0_11_0_DuyetBangGiaNcc') THEN
    ALTER TABLE "SupplierPriceList" ADD "RejectReason" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101101508_V0_11_0_DuyetBangGiaNcc') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261101101508_V0_11_0_DuyetBangGiaNcc', '10.0.14');
    END IF;
END $EF$;
COMMIT;

