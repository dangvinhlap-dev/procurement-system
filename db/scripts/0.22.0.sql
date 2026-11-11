START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110065054_V0_22_0_XacNhanNhanHang') THEN
    CREATE TABLE "GoodsReceiptConfirmation" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "No" character varying(50) NOT NULL,
        "Date" date NOT NULL,
        "Status" integer NOT NULL,
        "PurchaseOrderId" uuid NOT NULL,
        "SupplierId" uuid NOT NULL,
        "WarehouseId" uuid NOT NULL,
        "DeliveryNoteNo" character varying(100),
        "Deliverer" character varying(200),
        "InvoiceNo" character varying(50),
        "InvoiceSeries" character varying(50),
        "InvoiceDate" date,
        "InvoiceAmount" numeric(19,2),
        "Note" character varying(1000),
        "CancelReason" character varying(500),
        "ConfirmedAt" timestamp with time zone,
        "ConfirmedBy" uuid,
        "Acceptance" integer NOT NULL,
        "AcceptanceNote" character varying(500),
        "AcceptedBy" uuid,
        "AcceptedAt" timestamp with time zone,
        "TotalAmount" numeric(19,2) NOT NULL,
        "TotalVat" numeric(19,2) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_GoodsReceiptConfirmation" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_GoodsReceiptConfirmation_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_GoodsReceiptConfirmation_PurchaseOrder_PurchaseOrderId" FOREIGN KEY ("PurchaseOrderId") REFERENCES "PurchaseOrder" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_GoodsReceiptConfirmation_Supplier_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_GoodsReceiptConfirmation_Warehouse_WarehouseId_CompanyId" FOREIGN KEY ("WarehouseId", "CompanyId") REFERENCES "Warehouse" ("Id", "CompanyId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110065054_V0_22_0_XacNhanNhanHang') THEN
    CREATE TABLE "GoodsReceiptConfirmationLine" (
        "Id" uuid NOT NULL,
        "ConfirmationId" uuid NOT NULL,
        "LineNo" integer NOT NULL,
        "PurchaseOrderLineId" uuid NOT NULL,
        "ItemId" uuid NOT NULL,
        "UomId" uuid NOT NULL,
        "UomFactor" numeric(18,6) NOT NULL,
        "DeliveredQty" numeric(18,4) NOT NULL,
        "AcceptedQty" numeric(18,4) NOT NULL,
        "RejectedQty" numeric(18,4) NOT NULL,
        "AdjustedQty" numeric(18,4) NOT NULL,
        "ReceivedQty" numeric(18,4) NOT NULL,
        "ReceivedBaseQty" numeric(18,4) NOT NULL,
        "UnitPrice" numeric(19,6) NOT NULL,
        "VatCode" character varying(5),
        "Amount" numeric(19,2) NOT NULL,
        "VatAmount" numeric(19,2) NOT NULL,
        "CheckResult" integer NOT NULL,
        "CheckNote" character varying(500),
        "Note" character varying(500),
        CONSTRAINT "PK_GoodsReceiptConfirmationLine" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_GoodsReceiptConfirmationLine_GoodsReceiptConfirmation_Confi~" FOREIGN KEY ("ConfirmationId") REFERENCES "GoodsReceiptConfirmation" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_GoodsReceiptConfirmationLine_Item_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Item" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_GoodsReceiptConfirmationLine_PurchaseOrderLine_PurchaseOrde~" FOREIGN KEY ("PurchaseOrderLineId") REFERENCES "PurchaseOrderLine" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_GoodsReceiptConfirmationLine_Uom_UomId" FOREIGN KEY ("UomId") REFERENCES "Uom" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110065054_V0_22_0_XacNhanNhanHang') THEN
    CREATE TABLE "GoodsReceiptAdjustment" (
        "Id" uuid NOT NULL,
        "ConfirmationId" uuid NOT NULL,
        "LineId" uuid NOT NULL,
        "LineNo" integer NOT NULL,
        "Qty" numeric(18,4) NOT NULL,
        "Reason" character varying(500) NOT NULL,
        "AdjustedBy" uuid NOT NULL,
        "AdjustedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_GoodsReceiptAdjustment" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_GoodsReceiptAdjustment_GoodsReceiptConfirmationLine_LineId" FOREIGN KEY ("LineId") REFERENCES "GoodsReceiptConfirmationLine" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_GoodsReceiptAdjustment_GoodsReceiptConfirmation_Confirmatio~" FOREIGN KEY ("ConfirmationId") REFERENCES "GoodsReceiptConfirmation" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110065054_V0_22_0_XacNhanNhanHang') THEN
    CREATE INDEX "IX_GoodsReceiptAdjustment_ConfirmationId" ON "GoodsReceiptAdjustment" ("ConfirmationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110065054_V0_22_0_XacNhanNhanHang') THEN
    CREATE INDEX "IX_GoodsReceiptAdjustment_LineId" ON "GoodsReceiptAdjustment" ("LineId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110065054_V0_22_0_XacNhanNhanHang') THEN
    CREATE INDEX "IX_GoodsReceiptConfirmation_CompanyId_Date" ON "GoodsReceiptConfirmation" ("CompanyId", "Date");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110065054_V0_22_0_XacNhanNhanHang') THEN
    CREATE UNIQUE INDEX "IX_GoodsReceiptConfirmation_CompanyId_No" ON "GoodsReceiptConfirmation" ("CompanyId", "No");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110065054_V0_22_0_XacNhanNhanHang') THEN
    CREATE INDEX "IX_GoodsReceiptConfirmation_PurchaseOrderId" ON "GoodsReceiptConfirmation" ("PurchaseOrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110065054_V0_22_0_XacNhanNhanHang') THEN
    CREATE INDEX "IX_GoodsReceiptConfirmation_SupplierId" ON "GoodsReceiptConfirmation" ("SupplierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110065054_V0_22_0_XacNhanNhanHang') THEN
    CREATE INDEX "IX_GoodsReceiptConfirmation_WarehouseId_CompanyId" ON "GoodsReceiptConfirmation" ("WarehouseId", "CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110065054_V0_22_0_XacNhanNhanHang') THEN
    CREATE INDEX "IX_GoodsReceiptConfirmationLine_ConfirmationId_LineNo" ON "GoodsReceiptConfirmationLine" ("ConfirmationId", "LineNo");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110065054_V0_22_0_XacNhanNhanHang') THEN
    CREATE INDEX "IX_GoodsReceiptConfirmationLine_ItemId" ON "GoodsReceiptConfirmationLine" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110065054_V0_22_0_XacNhanNhanHang') THEN
    CREATE INDEX "IX_GoodsReceiptConfirmationLine_PurchaseOrderLineId" ON "GoodsReceiptConfirmationLine" ("PurchaseOrderLineId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110065054_V0_22_0_XacNhanNhanHang') THEN
    CREATE INDEX "IX_GoodsReceiptConfirmationLine_UomId" ON "GoodsReceiptConfirmationLine" ("UomId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110065054_V0_22_0_XacNhanNhanHang') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261110065054_V0_22_0_XacNhanNhanHang', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110081429_V0_22_0_KhoiPhongBan') THEN
    ALTER TABLE "Department" ADD "DivisionId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110081429_V0_22_0_KhoiPhongBan') THEN
    ALTER TABLE "Department" ADD "ManagerUserId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110081429_V0_22_0_KhoiPhongBan') THEN
    CREATE TABLE "Division" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "Code" character varying(20) NOT NULL,
        "Name" character varying(200) NOT NULL,
        "DirectorUserId" uuid,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_Division" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_Division_Id_CompanyId" UNIQUE ("Id", "CompanyId"),
        CONSTRAINT "FK_Division_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Division_User_DirectorUserId" FOREIGN KEY ("DirectorUserId") REFERENCES "User" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110081429_V0_22_0_KhoiPhongBan') THEN
    CREATE TABLE "UserDepartment" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "DepartmentId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_UserDepartment" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_UserDepartment_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_UserDepartment_Department_DepartmentId_CompanyId" FOREIGN KEY ("DepartmentId", "CompanyId") REFERENCES "Department" ("Id", "CompanyId") ON DELETE RESTRICT,
        CONSTRAINT "FK_UserDepartment_User_UserId" FOREIGN KEY ("UserId") REFERENCES "User" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110081429_V0_22_0_KhoiPhongBan') THEN
    CREATE INDEX "IX_Department_DivisionId_CompanyId" ON "Department" ("DivisionId", "CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110081429_V0_22_0_KhoiPhongBan') THEN
    CREATE INDEX "IX_Department_ManagerUserId" ON "Department" ("ManagerUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110081429_V0_22_0_KhoiPhongBan') THEN
    CREATE UNIQUE INDEX "IX_Division_CompanyId_Code" ON "Division" ("CompanyId", "Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110081429_V0_22_0_KhoiPhongBan') THEN
    CREATE INDEX "IX_Division_DirectorUserId" ON "Division" ("DirectorUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110081429_V0_22_0_KhoiPhongBan') THEN
    CREATE INDEX "IX_UserDepartment_CompanyId" ON "UserDepartment" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110081429_V0_22_0_KhoiPhongBan') THEN
    CREATE INDEX "IX_UserDepartment_DepartmentId_CompanyId" ON "UserDepartment" ("DepartmentId", "CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110081429_V0_22_0_KhoiPhongBan') THEN
    CREATE UNIQUE INDEX "IX_UserDepartment_UserId_CompanyId" ON "UserDepartment" ("UserId", "CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110081429_V0_22_0_KhoiPhongBan') THEN
    ALTER TABLE "Department" ADD CONSTRAINT "FK_Department_Division_DivisionId_CompanyId" FOREIGN KEY ("DivisionId", "CompanyId") REFERENCES "Division" ("Id", "CompanyId") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110081429_V0_22_0_KhoiPhongBan') THEN
    ALTER TABLE "Department" ADD CONSTRAINT "FK_Department_User_ManagerUserId" FOREIGN KEY ("ManagerUserId") REFERENCES "User" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110081429_V0_22_0_KhoiPhongBan') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261110081429_V0_22_0_KhoiPhongBan', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110083044_V0_22_0_DuyetDong') THEN
    ALTER TABLE "ApprovalStep" ADD "DepartmentId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110083044_V0_22_0_DuyetDong') THEN
    ALTER TABLE "ApprovalStep" ADD "Kind" integer NOT NULL DEFAULT 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110083044_V0_22_0_DuyetDong') THEN
    ALTER TABLE "ApprovalRule" ALTER COLUMN "RoleId" DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110083044_V0_22_0_DuyetDong') THEN
    ALTER TABLE "ApprovalRule" ADD "Kind" integer NOT NULL DEFAULT 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110083044_V0_22_0_DuyetDong') THEN
    CREATE INDEX "IX_ApprovalStep_DepartmentId" ON "ApprovalStep" ("DepartmentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110083044_V0_22_0_DuyetDong') THEN
    ALTER TABLE "ApprovalStep" ADD CONSTRAINT "FK_ApprovalStep_Department_DepartmentId" FOREIGN KEY ("DepartmentId") REFERENCES "Department" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110083044_V0_22_0_DuyetDong') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261110083044_V0_22_0_DuyetDong', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110090125_V0_22_0_UyQuyenDuyet') THEN
    ALTER TABLE "ApprovalStep" ADD "OnBehalfOf" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110090125_V0_22_0_UyQuyenDuyet') THEN
    CREATE TABLE "ApprovalDelegation" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "DelegatorUserId" uuid NOT NULL,
        "DelegateUserId" uuid NOT NULL,
        "DocType" character varying(10),
        "ValidFrom" date NOT NULL,
        "ValidTo" date NOT NULL,
        "Reason" character varying(500) NOT NULL,
        "Note" character varying(500),
        "RevokedAt" timestamp with time zone,
        "RevokedBy" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_ApprovalDelegation" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ApprovalDelegation_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ApprovalDelegation_User_DelegateUserId" FOREIGN KEY ("DelegateUserId") REFERENCES "User" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ApprovalDelegation_User_DelegatorUserId" FOREIGN KEY ("DelegatorUserId") REFERENCES "User" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ApprovalDelegation_User_RevokedBy" FOREIGN KEY ("RevokedBy") REFERENCES "User" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110090125_V0_22_0_UyQuyenDuyet') THEN
    CREATE INDEX "IX_ApprovalStep_OnBehalfOf" ON "ApprovalStep" ("OnBehalfOf");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110090125_V0_22_0_UyQuyenDuyet') THEN
    CREATE INDEX "IX_ApprovalDelegation_CompanyId_DelegateUserId" ON "ApprovalDelegation" ("CompanyId", "DelegateUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110090125_V0_22_0_UyQuyenDuyet') THEN
    CREATE INDEX "IX_ApprovalDelegation_CompanyId_DelegatorUserId" ON "ApprovalDelegation" ("CompanyId", "DelegatorUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110090125_V0_22_0_UyQuyenDuyet') THEN
    CREATE INDEX "IX_ApprovalDelegation_DelegateUserId" ON "ApprovalDelegation" ("DelegateUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110090125_V0_22_0_UyQuyenDuyet') THEN
    CREATE INDEX "IX_ApprovalDelegation_DelegatorUserId" ON "ApprovalDelegation" ("DelegatorUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110090125_V0_22_0_UyQuyenDuyet') THEN
    CREATE INDEX "IX_ApprovalDelegation_RevokedBy" ON "ApprovalDelegation" ("RevokedBy");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110090125_V0_22_0_UyQuyenDuyet') THEN
    ALTER TABLE "ApprovalStep" ADD CONSTRAINT "FK_ApprovalStep_User_OnBehalfOf" FOREIGN KEY ("OnBehalfOf") REFERENCES "User" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110090125_V0_22_0_UyQuyenDuyet') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261110090125_V0_22_0_UyQuyenDuyet', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110093636_V0_22_0_EmailThongBao') THEN
    ALTER TABLE "Notification" ADD "EmailAttempts" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110093636_V0_22_0_EmailThongBao') THEN
    ALTER TABLE "Notification" ADD "EmailNote" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110093636_V0_22_0_EmailThongBao') THEN
    ALTER TABLE "Notification" ADD "EmailState" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110093636_V0_22_0_EmailThongBao') THEN
    ALTER TABLE "Notification" ADD "EmailedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110093636_V0_22_0_EmailThongBao') THEN
    UPDATE "Notification" SET "EmailState" = 2, "EmailNote" = 'Thông báo có trước bản 0.22.0.';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110093636_V0_22_0_EmailThongBao') THEN
    CREATE INDEX "IX_Notification_EmailPending" ON "Notification" ("CreatedAt") WHERE "EmailState" = 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110093636_V0_22_0_EmailThongBao') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261110093636_V0_22_0_EmailThongBao', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110100424_V0_22_0_NhacQuaHanDuyet') THEN
    CREATE TABLE "ApprovalReminder" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "ApprovalStepId" uuid NOT NULL,
        "Sequence" integer NOT NULL,
        "RemindedAt" timestamp with time zone NOT NULL,
        "RecipientCount" integer NOT NULL,
        CONSTRAINT "PK_ApprovalReminder" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ApprovalReminder_ApprovalStep_ApprovalStepId" FOREIGN KEY ("ApprovalStepId") REFERENCES "ApprovalStep" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ApprovalReminder_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110100424_V0_22_0_NhacQuaHanDuyet') THEN
    CREATE UNIQUE INDEX "IX_ApprovalReminder_ApprovalStepId_Sequence" ON "ApprovalReminder" ("ApprovalStepId", "Sequence");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110100424_V0_22_0_NhacQuaHanDuyet') THEN
    CREATE INDEX "IX_ApprovalReminder_CompanyId" ON "ApprovalReminder" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110100424_V0_22_0_NhacQuaHanDuyet') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261110100424_V0_22_0_NhacQuaHanDuyet', '10.0.14');
    END IF;
END $EF$;
COMMIT;

