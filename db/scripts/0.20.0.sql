START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107004825_V0_20_0_MaHangNccTrenPo') THEN
    ALTER TABLE "PurchaseOrderLine" ADD "SupplierItemCode" character varying(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107004825_V0_20_0_MaHangNccTrenPo') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261107004825_V0_20_0_MaHangNccTrenPo', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107021218_V0_20_0_NganhHangNcc') THEN
    CREATE TABLE "SupplierCategory" (
        "Id" uuid NOT NULL,
        "SupplierId" uuid NOT NULL,
        "ItemCategoryId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_SupplierCategory" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SupplierCategory_ItemCategory_ItemCategoryId" FOREIGN KEY ("ItemCategoryId") REFERENCES "ItemCategory" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_SupplierCategory_Supplier_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107021218_V0_20_0_NganhHangNcc') THEN
    CREATE INDEX "IX_SupplierCategory_ItemCategoryId" ON "SupplierCategory" ("ItemCategoryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107021218_V0_20_0_NganhHangNcc') THEN
    CREATE UNIQUE INDEX "IX_SupplierCategory_SupplierId_ItemCategoryId" ON "SupplierCategory" ("SupplierId", "ItemCategoryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107021218_V0_20_0_NganhHangNcc') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261107021218_V0_20_0_NganhHangNcc', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107031744_V0_20_0_KeHoachNhuCau') THEN
    CREATE TABLE "DemandPlan" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "No" character varying(50) NOT NULL,
        "Year" integer NOT NULL,
        "ItemCategoryId" uuid NOT NULL,
        "Note" character varying(1000),
        "Status" integer NOT NULL,
        "ReplacesId" uuid,
        "ConfirmedAt" timestamp with time zone,
        "ConfirmedBy" uuid,
        "CancelledOn" date,
        "CancelReason" character varying(1000),
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_DemandPlan" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_DemandPlan_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_DemandPlan_DemandPlan_ReplacesId" FOREIGN KEY ("ReplacesId") REFERENCES "DemandPlan" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_DemandPlan_ItemCategory_ItemCategoryId" FOREIGN KEY ("ItemCategoryId") REFERENCES "ItemCategory" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107031744_V0_20_0_KeHoachNhuCau') THEN
    CREATE TABLE "DemandPlanLine" (
        "Id" uuid NOT NULL,
        "PlanId" uuid NOT NULL,
        "LineNo" integer NOT NULL,
        "ItemId" uuid NOT NULL,
        "UomId" uuid NOT NULL,
        "UomFactor" numeric(18,6) NOT NULL,
        "Qty01" numeric(18,4) NOT NULL,
        "Qty02" numeric(18,4) NOT NULL,
        "Qty03" numeric(18,4) NOT NULL,
        "Qty04" numeric(18,4) NOT NULL,
        "Qty05" numeric(18,4) NOT NULL,
        "Qty06" numeric(18,4) NOT NULL,
        "Qty07" numeric(18,4) NOT NULL,
        "Qty08" numeric(18,4) NOT NULL,
        "Qty09" numeric(18,4) NOT NULL,
        "Qty10" numeric(18,4) NOT NULL,
        "Qty11" numeric(18,4) NOT NULL,
        "Qty12" numeric(18,4) NOT NULL,
        "Note" character varying(500),
        CONSTRAINT "PK_DemandPlanLine" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_DemandPlanLine_DemandPlan_PlanId" FOREIGN KEY ("PlanId") REFERENCES "DemandPlan" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_DemandPlanLine_Item_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Item" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_DemandPlanLine_Uom_UomId" FOREIGN KEY ("UomId") REFERENCES "Uom" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107031744_V0_20_0_KeHoachNhuCau') THEN
    CREATE UNIQUE INDEX "IX_DemandPlan_CompanyId_No" ON "DemandPlan" ("CompanyId", "No");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107031744_V0_20_0_KeHoachNhuCau') THEN
    CREATE INDEX "IX_DemandPlan_ItemCategoryId" ON "DemandPlan" ("ItemCategoryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107031744_V0_20_0_KeHoachNhuCau') THEN
    CREATE INDEX "IX_DemandPlan_ReplacesId" ON "DemandPlan" ("ReplacesId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107031744_V0_20_0_KeHoachNhuCau') THEN
    CREATE INDEX "IX_DemandPlan_Year_Status" ON "DemandPlan" ("Year", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107031744_V0_20_0_KeHoachNhuCau') THEN
    CREATE INDEX "IX_DemandPlanLine_ItemId" ON "DemandPlanLine" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107031744_V0_20_0_KeHoachNhuCau') THEN
    CREATE INDEX "IX_DemandPlanLine_PlanId" ON "DemandPlanLine" ("PlanId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107031744_V0_20_0_KeHoachNhuCau') THEN
    CREATE INDEX "IX_DemandPlanLine_UomId" ON "DemandPlanLine" ("UomId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107031744_V0_20_0_KeHoachNhuCau') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261107031744_V0_20_0_KeHoachNhuCau', '10.0.14');
    END IF;
END $EF$;
COMMIT;

