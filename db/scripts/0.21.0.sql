START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108005757_V0_20_0_ChietKhauSanLuongGop') THEN
    CREATE TABLE "VolumeRebateAgreement" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "No" character varying(50) NOT NULL,
        "Title" character varying(200) NOT NULL,
        "SupplierId" uuid NOT NULL,
        "ItemCategoryId" uuid NOT NULL,
        "ValidFrom" date NOT NULL,
        "ValidTo" date NOT NULL,
        "Period" integer NOT NULL,
        "Basis" integer NOT NULL,
        "TargetTierNo" integer,
        "Note" character varying(1000),
        "Status" integer NOT NULL,
        "ApprovalRound" integer NOT NULL,
        "RejectReason" character varying(500),
        "ApprovedAt" timestamp with time zone,
        "CancelledOn" date,
        "CancelReason" character varying(1000),
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_VolumeRebateAgreement" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_VolumeRebateAgreement_Validity" CHECK ("ValidTo" >= "ValidFrom"),
        CONSTRAINT "FK_VolumeRebateAgreement_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_VolumeRebateAgreement_ItemCategory_ItemCategoryId" FOREIGN KEY ("ItemCategoryId") REFERENCES "ItemCategory" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_VolumeRebateAgreement_Supplier_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108005757_V0_20_0_ChietKhauSanLuongGop') THEN
    CREATE TABLE "VolumeRebateCompany" (
        "Id" uuid NOT NULL,
        "AgreementId" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        CONSTRAINT "PK_VolumeRebateCompany" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_VolumeRebateCompany_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_VolumeRebateCompany_VolumeRebateAgreement_AgreementId" FOREIGN KEY ("AgreementId") REFERENCES "VolumeRebateAgreement" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108005757_V0_20_0_ChietKhauSanLuongGop') THEN
    CREATE TABLE "VolumeRebateLine" (
        "Id" uuid NOT NULL,
        "AgreementId" uuid NOT NULL,
        "LineNo" integer NOT NULL,
        "ItemId" uuid NOT NULL,
        "UomId" uuid NOT NULL,
        "UomFactor" numeric(18,6) NOT NULL,
        "Note" character varying(500),
        CONSTRAINT "PK_VolumeRebateLine" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_VolumeRebateLine_Item_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Item" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_VolumeRebateLine_Uom_UomId" FOREIGN KEY ("UomId") REFERENCES "Uom" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_VolumeRebateLine_VolumeRebateAgreement_AgreementId" FOREIGN KEY ("AgreementId") REFERENCES "VolumeRebateAgreement" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108005757_V0_20_0_ChietKhauSanLuongGop') THEN
    CREATE TABLE "VolumeRebateTier" (
        "Id" uuid NOT NULL,
        "AgreementId" uuid NOT NULL,
        "TierNo" integer NOT NULL,
        "FromValue" numeric(19,4) NOT NULL,
        "Percent" numeric(9,4) NOT NULL,
        CONSTRAINT "PK_VolumeRebateTier" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_VolumeRebateTier_VolumeRebateAgreement_AgreementId" FOREIGN KEY ("AgreementId") REFERENCES "VolumeRebateAgreement" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108005757_V0_20_0_ChietKhauSanLuongGop') THEN
    CREATE UNIQUE INDEX "IX_VolumeRebateAgreement_CompanyId_No" ON "VolumeRebateAgreement" ("CompanyId", "No");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108005757_V0_20_0_ChietKhauSanLuongGop') THEN
    CREATE INDEX "IX_VolumeRebateAgreement_ItemCategoryId" ON "VolumeRebateAgreement" ("ItemCategoryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108005757_V0_20_0_ChietKhauSanLuongGop') THEN
    CREATE INDEX "IX_VolumeRebateAgreement_SupplierId_Status" ON "VolumeRebateAgreement" ("SupplierId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108005757_V0_20_0_ChietKhauSanLuongGop') THEN
    CREATE UNIQUE INDEX "IX_VolumeRebateCompany_AgreementId_CompanyId" ON "VolumeRebateCompany" ("AgreementId", "CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108005757_V0_20_0_ChietKhauSanLuongGop') THEN
    CREATE INDEX "IX_VolumeRebateCompany_CompanyId" ON "VolumeRebateCompany" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108005757_V0_20_0_ChietKhauSanLuongGop') THEN
    CREATE UNIQUE INDEX "IX_VolumeRebateLine_AgreementId_ItemId" ON "VolumeRebateLine" ("AgreementId", "ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108005757_V0_20_0_ChietKhauSanLuongGop') THEN
    CREATE INDEX "IX_VolumeRebateLine_ItemId" ON "VolumeRebateLine" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108005757_V0_20_0_ChietKhauSanLuongGop') THEN
    CREATE INDEX "IX_VolumeRebateLine_UomId" ON "VolumeRebateLine" ("UomId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108005757_V0_20_0_ChietKhauSanLuongGop') THEN
    CREATE UNIQUE INDEX "IX_VolumeRebateTier_AgreementId_TierNo" ON "VolumeRebateTier" ("AgreementId", "TierNo");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108005757_V0_20_0_ChietKhauSanLuongGop') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261108005757_V0_20_0_ChietKhauSanLuongGop', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108035058_V0_20_0_ThucNhanChietKhauSanLuong') THEN
    CREATE TABLE "VolumeRebateReceipt" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "AgreementId" uuid NOT NULL,
        "PeriodFrom" date NOT NULL,
        "ReceivedOn" date NOT NULL,
        "Amount" numeric(19,2) NOT NULL,
        "Form" integer NOT NULL,
        "RefNo" character varying(100),
        "Note" character varying(500),
        "IsCancelled" boolean NOT NULL,
        "CancelReason" character varying(500),
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_VolumeRebateReceipt" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_VolumeRebateReceipt_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_VolumeRebateReceipt_VolumeRebateAgreement_AgreementId" FOREIGN KEY ("AgreementId") REFERENCES "VolumeRebateAgreement" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108035058_V0_20_0_ThucNhanChietKhauSanLuong') THEN
    CREATE TABLE "VolumeRebateReceiptShare" (
        "Id" uuid NOT NULL,
        "ReceiptId" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "PurchaseAmount" numeric(19,2) NOT NULL,
        "SharePercent" numeric(9,4) NOT NULL,
        "Amount" numeric(19,2) NOT NULL,
        CONSTRAINT "PK_VolumeRebateReceiptShare" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_VolumeRebateReceiptShare_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_VolumeRebateReceiptShare_VolumeRebateReceipt_ReceiptId" FOREIGN KEY ("ReceiptId") REFERENCES "VolumeRebateReceipt" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108035058_V0_20_0_ThucNhanChietKhauSanLuong') THEN
    CREATE INDEX "IX_VolumeRebateReceipt_AgreementId_PeriodFrom" ON "VolumeRebateReceipt" ("AgreementId", "PeriodFrom");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108035058_V0_20_0_ThucNhanChietKhauSanLuong') THEN
    CREATE INDEX "IX_VolumeRebateReceipt_CompanyId" ON "VolumeRebateReceipt" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108035058_V0_20_0_ThucNhanChietKhauSanLuong') THEN
    CREATE INDEX "IX_VolumeRebateReceiptShare_CompanyId" ON "VolumeRebateReceiptShare" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108035058_V0_20_0_ThucNhanChietKhauSanLuong') THEN
    CREATE UNIQUE INDEX "IX_VolumeRebateReceiptShare_ReceiptId_CompanyId" ON "VolumeRebateReceiptShare" ("ReceiptId", "CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108035058_V0_20_0_ThucNhanChietKhauSanLuong') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261108035058_V0_20_0_ThucNhanChietKhauSanLuong', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108052813_V0_20_0_DatHangGop') THEN
    ALTER TABLE "CompanyParameter" ADD "PooledMarginPercent" numeric(9,4) NOT NULL DEFAULT 10.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108052813_V0_20_0_DatHangGop') THEN
    CREATE TABLE "PooledOrder" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "No" character varying(50) NOT NULL,
        "OrderDate" date NOT NULL,
        "MonthlyApprovalId" uuid NOT NULL,
        "ItemCategoryId" uuid NOT NULL,
        "SupplierId" uuid NOT NULL,
        "ExpectedDate" date,
        "Note" character varying(1000),
        "Status" integer NOT NULL,
        "MarginPercent" numeric(9,4),
        "OrderedAt" timestamp with time zone,
        "OrderedBy" uuid,
        "CancelledOn" date,
        "CancelReason" character varying(1000),
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_PooledOrder" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PooledOrder_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PooledOrder_ItemCategory_ItemCategoryId" FOREIGN KEY ("ItemCategoryId") REFERENCES "ItemCategory" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PooledOrder_MonthlySupplierApproval_MonthlyApprovalId" FOREIGN KEY ("MonthlyApprovalId") REFERENCES "MonthlySupplierApproval" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PooledOrder_Supplier_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108052813_V0_20_0_DatHangGop') THEN
    CREATE TABLE "PooledOrderCompany" (
        "Id" uuid NOT NULL,
        "PooledOrderId" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "WarehouseId" uuid NOT NULL,
        "DepartmentId" uuid,
        "PurchaseOrderId" uuid,
        CONSTRAINT "PK_PooledOrderCompany" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PooledOrderCompany_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PooledOrderCompany_Department_DepartmentId" FOREIGN KEY ("DepartmentId") REFERENCES "Department" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PooledOrderCompany_PooledOrder_PooledOrderId" FOREIGN KEY ("PooledOrderId") REFERENCES "PooledOrder" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_PooledOrderCompany_PurchaseOrder_PurchaseOrderId" FOREIGN KEY ("PurchaseOrderId") REFERENCES "PurchaseOrder" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PooledOrderCompany_Warehouse_WarehouseId" FOREIGN KEY ("WarehouseId") REFERENCES "Warehouse" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108052813_V0_20_0_DatHangGop') THEN
    CREATE TABLE "PooledOrderLine" (
        "Id" uuid NOT NULL,
        "PooledOrderId" uuid NOT NULL,
        "LineNo" integer NOT NULL,
        "MonthlyApprovalLineId" uuid NOT NULL,
        "ItemId" uuid NOT NULL,
        "UomId" uuid NOT NULL,
        "UomFactor" numeric(18,6) NOT NULL,
        "UnitPrice" numeric(19,6) NOT NULL,
        "AllocationReason" character varying(500),
        "Note" character varying(500),
        CONSTRAINT "PK_PooledOrderLine" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PooledOrderLine_Item_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Item" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PooledOrderLine_MonthlySupplierApprovalLine_MonthlyApproval~" FOREIGN KEY ("MonthlyApprovalLineId") REFERENCES "MonthlySupplierApprovalLine" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PooledOrderLine_PooledOrder_PooledOrderId" FOREIGN KEY ("PooledOrderId") REFERENCES "PooledOrder" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_PooledOrderLine_Uom_UomId" FOREIGN KEY ("UomId") REFERENCES "Uom" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108052813_V0_20_0_DatHangGop') THEN
    CREATE TABLE "PooledOrderAllocation" (
        "Id" uuid NOT NULL,
        "LineId" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "Qty" numeric(18,4) NOT NULL,
        CONSTRAINT "PK_PooledOrderAllocation" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_PooledOrderAllocation_Qty" CHECK ("Qty" > 0),
        CONSTRAINT "FK_PooledOrderAllocation_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PooledOrderAllocation_PooledOrderLine_LineId" FOREIGN KEY ("LineId") REFERENCES "PooledOrderLine" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108052813_V0_20_0_DatHangGop') THEN
    CREATE UNIQUE INDEX "IX_PooledOrder_CompanyId_No" ON "PooledOrder" ("CompanyId", "No");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108052813_V0_20_0_DatHangGop') THEN
    CREATE INDEX "IX_PooledOrder_ItemCategoryId" ON "PooledOrder" ("ItemCategoryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108052813_V0_20_0_DatHangGop') THEN
    CREATE INDEX "IX_PooledOrder_MonthlyApprovalId" ON "PooledOrder" ("MonthlyApprovalId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108052813_V0_20_0_DatHangGop') THEN
    CREATE INDEX "IX_PooledOrder_OrderDate_Status" ON "PooledOrder" ("OrderDate", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108052813_V0_20_0_DatHangGop') THEN
    CREATE INDEX "IX_PooledOrder_SupplierId" ON "PooledOrder" ("SupplierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108052813_V0_20_0_DatHangGop') THEN
    CREATE INDEX "IX_PooledOrderAllocation_CompanyId" ON "PooledOrderAllocation" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108052813_V0_20_0_DatHangGop') THEN
    CREATE UNIQUE INDEX "IX_PooledOrderAllocation_LineId_CompanyId" ON "PooledOrderAllocation" ("LineId", "CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108052813_V0_20_0_DatHangGop') THEN
    CREATE INDEX "IX_PooledOrderCompany_CompanyId" ON "PooledOrderCompany" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108052813_V0_20_0_DatHangGop') THEN
    CREATE INDEX "IX_PooledOrderCompany_DepartmentId" ON "PooledOrderCompany" ("DepartmentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108052813_V0_20_0_DatHangGop') THEN
    CREATE UNIQUE INDEX "IX_PooledOrderCompany_PooledOrderId_CompanyId" ON "PooledOrderCompany" ("PooledOrderId", "CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108052813_V0_20_0_DatHangGop') THEN
    CREATE INDEX "IX_PooledOrderCompany_PurchaseOrderId" ON "PooledOrderCompany" ("PurchaseOrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108052813_V0_20_0_DatHangGop') THEN
    CREATE INDEX "IX_PooledOrderCompany_WarehouseId" ON "PooledOrderCompany" ("WarehouseId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108052813_V0_20_0_DatHangGop') THEN
    CREATE INDEX "IX_PooledOrderLine_ItemId" ON "PooledOrderLine" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108052813_V0_20_0_DatHangGop') THEN
    CREATE INDEX "IX_PooledOrderLine_MonthlyApprovalLineId" ON "PooledOrderLine" ("MonthlyApprovalLineId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108052813_V0_20_0_DatHangGop') THEN
    CREATE UNIQUE INDEX "IX_PooledOrderLine_PooledOrderId_MonthlyApprovalLineId" ON "PooledOrderLine" ("PooledOrderId", "MonthlyApprovalLineId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108052813_V0_20_0_DatHangGop') THEN
    CREATE INDEX "IX_PooledOrderLine_UomId" ON "PooledOrderLine" ("UomId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108052813_V0_20_0_DatHangGop') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261108052813_V0_20_0_DatHangGop', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109004152_V0_20_0_YKienThamDinh') THEN
    CREATE TABLE "AppraisalOpinion" (
        "Id" uuid NOT NULL,
        "OwnerType" character varying(30) NOT NULL,
        "OwnerId" uuid NOT NULL,
        "CompanyId" uuid,
        "Aspect" integer NOT NULL,
        "Result" integer NOT NULL,
        "Content" character varying(2000) NOT NULL,
        "IsWithdrawn" boolean NOT NULL,
        "WithdrawReason" character varying(500),
        "WithdrawnAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_AppraisalOpinion" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AppraisalOpinion_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109004152_V0_20_0_YKienThamDinh') THEN
    CREATE INDEX "IX_AppraisalOpinion_CompanyId" ON "AppraisalOpinion" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109004152_V0_20_0_YKienThamDinh') THEN
    CREATE INDEX "IX_AppraisalOpinion_OwnerType_OwnerId" ON "AppraisalOpinion" ("OwnerType", "OwnerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109004152_V0_20_0_YKienThamDinh') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261109004152_V0_20_0_YKienThamDinh', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109011411_V0_20_0_GthNgoaiLe') THEN
    ALTER TABLE "MonthlySupplierApproval" ADD "ExceptionNote" character varying(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109011411_V0_20_0_GthNgoaiLe') THEN
    ALTER TABLE "MonthlySupplierApproval" ADD "HasException" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109011411_V0_20_0_GthNgoaiLe') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261109011411_V0_20_0_GthNgoaiLe', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109020954_V0_20_0_XacNhanNghiemThu') THEN
    ALTER TABLE "InventoryDocument" ADD "Acceptance" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109020954_V0_20_0_XacNhanNghiemThu') THEN
    ALTER TABLE "InventoryDocument" ADD "AcceptanceNote" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109020954_V0_20_0_XacNhanNghiemThu') THEN
    ALTER TABLE "InventoryDocument" ADD "AcceptedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109020954_V0_20_0_XacNhanNghiemThu') THEN
    ALTER TABLE "InventoryDocument" ADD "AcceptedBy" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109020954_V0_20_0_XacNhanNghiemThu') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261109020954_V0_20_0_XacNhanNghiemThu', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109062314_V0_20_0_HoSoDanhGiaNccMoi') THEN
    ALTER TABLE "Supplier" ADD "RequiresOnboarding" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109062314_V0_20_0_HoSoDanhGiaNccMoi') THEN
    ALTER TABLE "CompanyParameter" ADD "AllowSupplierWithoutOnboarding" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109062314_V0_20_0_HoSoDanhGiaNccMoi') THEN
    CREATE TABLE "SupplierOnboarding" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "No" character varying(50) NOT NULL,
        "SupplierId" uuid NOT NULL,
        "CategoryId" uuid NOT NULL,
        "Date" date NOT NULL,
        "Source" character varying(500),
        "DocumentsJson" jsonb NOT NULL,
        "PriceScore" integer,
        "SampleQualityScore" integer,
        "CapacityScore" integer,
        "DeliveryScore" integer,
        "PaymentScore" integer,
        "LegalScore" integer,
        "Score" numeric(5,2),
        "Result" integer,
        "Comment" character varying(2000),
        "Note" character varying(2000),
        "Status" integer NOT NULL,
        "EvaluatedAt" timestamp with time zone,
        "EvaluatedBy" uuid,
        "Recommended" boolean,
        "ProposalComment" character varying(2000),
        "ProposedAt" timestamp with time zone,
        "ProposedBy" uuid,
        "ChangeRequestId" uuid,
        "ApprovedAt" timestamp with time zone,
        "RejectReason" character varying(500),
        "CancelReason" character varying(2000),
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_SupplierOnboarding" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SupplierOnboarding_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_SupplierOnboarding_ItemCategory_CategoryId" FOREIGN KEY ("CategoryId") REFERENCES "ItemCategory" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_SupplierOnboarding_SupplierChangeRequest_ChangeRequestId" FOREIGN KEY ("ChangeRequestId") REFERENCES "SupplierChangeRequest" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_SupplierOnboarding_Supplier_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109062314_V0_20_0_HoSoDanhGiaNccMoi') THEN
    CREATE INDEX "IX_SupplierOnboarding_CategoryId" ON "SupplierOnboarding" ("CategoryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109062314_V0_20_0_HoSoDanhGiaNccMoi') THEN
    CREATE INDEX "IX_SupplierOnboarding_ChangeRequestId" ON "SupplierOnboarding" ("ChangeRequestId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109062314_V0_20_0_HoSoDanhGiaNccMoi') THEN
    CREATE UNIQUE INDEX "IX_SupplierOnboarding_CompanyId_No" ON "SupplierOnboarding" ("CompanyId", "No");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109062314_V0_20_0_HoSoDanhGiaNccMoi') THEN
    CREATE UNIQUE INDEX "UX_SupplierOnboarding_Open" ON "SupplierOnboarding" ("SupplierId") WHERE "Status" IN (1, 2, 3);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109062314_V0_20_0_HoSoDanhGiaNccMoi') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261109062314_V0_20_0_HoSoDanhGiaNccMoi', '10.0.14');
    END IF;
END $EF$;
COMMIT;

