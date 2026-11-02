START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102015816_V0_13_0_BoPhanPoDocQuyen') THEN
    ALTER TABLE "PurchaseOrderLine" ADD "SoleSourceApprovalId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102015816_V0_13_0_BoPhanPoDocQuyen') THEN
    ALTER TABLE "PurchaseOrder" ADD "DepartmentId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102015816_V0_13_0_BoPhanPoDocQuyen') THEN
    CREATE INDEX "IX_PurchaseOrderLine_SoleSourceApprovalId" ON "PurchaseOrderLine" ("SoleSourceApprovalId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102015816_V0_13_0_BoPhanPoDocQuyen') THEN
    CREATE INDEX "IX_PurchaseOrder_DepartmentId" ON "PurchaseOrder" ("DepartmentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102015816_V0_13_0_BoPhanPoDocQuyen') THEN
    ALTER TABLE "PurchaseOrder" ADD CONSTRAINT "FK_PurchaseOrder_Department_DepartmentId" FOREIGN KEY ("DepartmentId") REFERENCES "Department" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102015816_V0_13_0_BoPhanPoDocQuyen') THEN
    ALTER TABLE "PurchaseOrderLine" ADD CONSTRAINT "FK_PurchaseOrderLine_SoleSourceApproval_SoleSourceApprovalId" FOREIGN KEY ("SoleSourceApprovalId") REFERENCES "SoleSourceApproval" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102015816_V0_13_0_BoPhanPoDocQuyen') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261102015816_V0_13_0_BoPhanPoDocQuyen', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102021749_V0_13_0_MuaKhanCap') THEN
    ALTER TABLE "PurchaseOrder" ADD "DocsCompletedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102021749_V0_13_0_MuaKhanCap') THEN
    ALTER TABLE "PurchaseOrder" ADD "DocsCompletedBy" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102021749_V0_13_0_MuaKhanCap') THEN
    ALTER TABLE "PurchaseOrder" ADD "DocsCompletionNote" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102021749_V0_13_0_MuaKhanCap') THEN
    ALTER TABLE "PurchaseOrder" ADD "DocsDueDate" date;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102021749_V0_13_0_MuaKhanCap') THEN
    ALTER TABLE "PurchaseOrder" ADD "EmergencyReason" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102021749_V0_13_0_MuaKhanCap') THEN
    ALTER TABLE "PurchaseOrder" ADD "PurchaseType" integer NOT NULL DEFAULT 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102021749_V0_13_0_MuaKhanCap') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261102021749_V0_13_0_MuaKhanCap', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102023619_V0_13_0_HangMoiChuaCoMa') THEN
    CREATE TABLE "PurchaseRequestNewItem" (
        "Id" uuid NOT NULL,
        "RequestId" uuid NOT NULL,
        "LineNo" integer NOT NULL,
        "Description" character varying(500) NOT NULL,
        "Specification" character varying(1000),
        "UomId" uuid NOT NULL,
        "Qty" numeric(18,4) NOT NULL,
        "EstPrice" numeric(19,6) NOT NULL,
        "Amount" numeric(19,2) NOT NULL,
        "NeededDate" date,
        "Note" character varying(500),
        "SourcingRequestId" uuid,
        "ItemId" uuid,
        "LinkedLineId" uuid,
        CONSTRAINT "PK_PurchaseRequestNewItem" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PurchaseRequestNewItem_Item_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Item" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseRequestNewItem_PurchaseRequestLine_LinkedLineId" FOREIGN KEY ("LinkedLineId") REFERENCES "PurchaseRequestLine" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseRequestNewItem_PurchaseRequest_RequestId" FOREIGN KEY ("RequestId") REFERENCES "PurchaseRequest" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_PurchaseRequestNewItem_SourcingRequest_SourcingRequestId" FOREIGN KEY ("SourcingRequestId") REFERENCES "SourcingRequest" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseRequestNewItem_Uom_UomId" FOREIGN KEY ("UomId") REFERENCES "Uom" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102023619_V0_13_0_HangMoiChuaCoMa') THEN
    CREATE INDEX "IX_PurchaseRequestNewItem_ItemId" ON "PurchaseRequestNewItem" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102023619_V0_13_0_HangMoiChuaCoMa') THEN
    CREATE INDEX "IX_PurchaseRequestNewItem_LinkedLineId" ON "PurchaseRequestNewItem" ("LinkedLineId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102023619_V0_13_0_HangMoiChuaCoMa') THEN
    CREATE INDEX "IX_PurchaseRequestNewItem_RequestId_LineNo" ON "PurchaseRequestNewItem" ("RequestId", "LineNo");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102023619_V0_13_0_HangMoiChuaCoMa') THEN
    CREATE INDEX "IX_PurchaseRequestNewItem_SourcingRequestId" ON "PurchaseRequestNewItem" ("SourcingRequestId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102023619_V0_13_0_HangMoiChuaCoMa') THEN
    CREATE INDEX "IX_PurchaseRequestNewItem_UomId" ON "PurchaseRequestNewItem" ("UomId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102023619_V0_13_0_HangMoiChuaCoMa') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261102023619_V0_13_0_HangMoiChuaCoMa', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102032148_V0_13_0_ThamSoSla') THEN
    ALTER TABLE "CompanyParameter" ADD "SlaPricedDays" integer NOT NULL DEFAULT 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102032148_V0_13_0_ThamSoSla') THEN
    ALTER TABLE "CompanyParameter" ADD "SlaSourcingDays" integer NOT NULL DEFAULT 5;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102032148_V0_13_0_ThamSoSla') THEN
    ALTER TABLE "CompanyParameter" ADD "SlaUrgentDays" integer NOT NULL DEFAULT 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102032148_V0_13_0_ThamSoSla') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261102032148_V0_13_0_ThamSoSla', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102040918_V0_13_0_KiemTraNhanHang') THEN
    ALTER TABLE "InventoryDocumentLine" ADD "DeliveredQty" numeric(18,4);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102040918_V0_13_0_KiemTraNhanHang') THEN
    ALTER TABLE "InventoryDocumentLine" ADD "QcNote" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102040918_V0_13_0_KiemTraNhanHang') THEN
    ALTER TABLE "InventoryDocumentLine" ADD "QcResult" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102040918_V0_13_0_KiemTraNhanHang') THEN
    ALTER TABLE "InventoryDocumentLine" ADD "RejectedQty" numeric(18,4) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102040918_V0_13_0_KiemTraNhanHang') THEN
    ALTER TABLE "CompanyParameter" ADD "RequireReceiptQc" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102040918_V0_13_0_KiemTraNhanHang') THEN
    ALTER TABLE "CompanyParameter" ADD "SeparatePoCreatorFromReceipt" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102040918_V0_13_0_KiemTraNhanHang') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261102040918_V0_13_0_KiemTraNhanHang', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102044357_V0_13_0_TienHoaDonPhieuNhap') THEN
    ALTER TABLE "InventoryDocument" ADD "InvoiceAmount" numeric(19,2);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102044357_V0_13_0_TienHoaDonPhieuNhap') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261102044357_V0_13_0_TienHoaDonPhieuNhap', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102071450_V0_13_0_DiemDatHangLai') THEN
    ALTER TABLE "Item" ADD "IsCritical" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102071450_V0_13_0_DiemDatHangLai') THEN
    ALTER TABLE "Item" ADD "ReorderPoint" numeric(18,4);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102071450_V0_13_0_DiemDatHangLai') THEN
    ALTER TABLE "Item" ADD "SafetyStock" numeric(18,4);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102071450_V0_13_0_DiemDatHangLai') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261102071450_V0_13_0_DiemDatHangLai', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102075505_V0_13_0_MucTieuKpi') THEN
    ALTER TABLE "CompanyParameter" ADD "KpiContractTarget" numeric(9,4) NOT NULL DEFAULT 95.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102075505_V0_13_0_MucTieuKpi') THEN
    ALTER TABLE "CompanyParameter" ADD "KpiEmergencyTarget" numeric(9,4) NOT NULL DEFAULT 5.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102075505_V0_13_0_MucTieuKpi') THEN
    ALTER TABLE "CompanyParameter" ADD "KpiOnTimeTarget" numeric(9,4) NOT NULL DEFAULT 95.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102075505_V0_13_0_MucTieuKpi') THEN
    ALTER TABLE "CompanyParameter" ADD "KpiQualityTarget" numeric(9,4) NOT NULL DEFAULT 98.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102075505_V0_13_0_MucTieuKpi') THEN
    ALTER TABLE "CompanyParameter" ADD "KpiSavingTarget" numeric(9,4) NOT NULL DEFAULT 3.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102075505_V0_13_0_MucTieuKpi') THEN
    ALTER TABLE "CompanyParameter" ADD "KpiSlaTarget" numeric(9,4) NOT NULL DEFAULT 95.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102075505_V0_13_0_MucTieuKpi') THEN
    ALTER TABLE "CompanyParameter" ADD "KpiSlowMovingDays" integer NOT NULL DEFAULT 90;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102075505_V0_13_0_MucTieuKpi') THEN
    ALTER TABLE "CompanyParameter" ADD "KpiSlowMovingTarget" numeric(9,4) NOT NULL DEFAULT 10.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102075505_V0_13_0_MucTieuKpi') THEN
    ALTER TABLE "CompanyParameter" ADD "KpiSupplierReviewTarget" numeric(9,4) NOT NULL DEFAULT 100.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102075505_V0_13_0_MucTieuKpi') THEN
    ALTER TABLE "CompanyParameter" ADD "KpiViolationTarget" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102075505_V0_13_0_MucTieuKpi') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261102075505_V0_13_0_MucTieuKpi', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083631_V0_13_0_GhiNhanViPham') THEN
    CREATE TABLE "ControlFinding" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "No" character varying(50) NOT NULL,
        "Date" date NOT NULL,
        "Severity" integer NOT NULL,
        "AnomalyCode" character varying(40),
        "Title" character varying(200) NOT NULL,
        "Description" character varying(2000) NOT NULL,
        "RefScreen" character varying(20),
        "RefId" uuid,
        "RefNo" character varying(50),
        "SupplierId" uuid,
        "Status" integer NOT NULL,
        "Conclusion" character varying(2000),
        "ConcludedAt" timestamp with time zone,
        "ConcludedBy" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_ControlFinding" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ControlFinding_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ControlFinding_Supplier_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083631_V0_13_0_GhiNhanViPham') THEN
    CREATE INDEX "IX_ControlFinding_CompanyId_Date" ON "ControlFinding" ("CompanyId", "Date");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083631_V0_13_0_GhiNhanViPham') THEN
    CREATE UNIQUE INDEX "IX_ControlFinding_CompanyId_No" ON "ControlFinding" ("CompanyId", "No");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083631_V0_13_0_GhiNhanViPham') THEN
    CREATE INDEX "IX_ControlFinding_SupplierId" ON "ControlFinding" ("SupplierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102083631_V0_13_0_GhiNhanViPham') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261102083631_V0_13_0_GhiNhanViPham', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102095709_V0_13_0_DanhGiaNccDinhKy') THEN
    CREATE TABLE "SupplierReview" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "No" character varying(50) NOT NULL,
        "SupplierId" uuid NOT NULL,
        "Year" integer NOT NULL,
        "Quarter" integer NOT NULL,
        "Date" date NOT NULL,
        "PriceScore" integer,
        "QualityScore" integer,
        "DeliveryScore" integer,
        "WarrantyScore" integer,
        "PaymentScore" integer,
        "ComplianceScore" integer,
        "Score" numeric(5,2),
        "Result" integer,
        "Comment" character varying(2000),
        "ReceiptAmount" numeric(19,2) NOT NULL,
        "OnTimePct" numeric(5,2),
        "ReturnPct" numeric(9,2),
        "QualityPct" numeric(5,2),
        "SystemRating" character varying(50),
        "Status" integer NOT NULL,
        "CompletedAt" timestamp with time zone,
        "CompletedBy" uuid,
        "CancelReason" character varying(2000),
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_SupplierReview" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SupplierReview_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_SupplierReview_Supplier_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102095709_V0_13_0_DanhGiaNccDinhKy') THEN
    CREATE UNIQUE INDEX "IX_SupplierReview_CompanyId_No" ON "SupplierReview" ("CompanyId", "No");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102095709_V0_13_0_DanhGiaNccDinhKy') THEN
    CREATE INDEX "IX_SupplierReview_SupplierId" ON "SupplierReview" ("SupplierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102095709_V0_13_0_DanhGiaNccDinhKy') THEN
    CREATE UNIQUE INDEX "UX_SupplierReview_Quarter" ON "SupplierReview" ("CompanyId", "SupplierId", "Year", "Quarter") WHERE "Status" <> 3;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102095709_V0_13_0_DanhGiaNccDinhKy') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261102095709_V0_13_0_DanhGiaNccDinhKy', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102101751_V0_13_0_ChietKhauTaiTroHopDong') THEN
    CREATE TABLE "PurchaseContractRebate" (
        "Id" uuid NOT NULL,
        "ContractId" uuid NOT NULL,
        "LineNo" integer NOT NULL,
        "Kind" integer NOT NULL,
        "Method" integer NOT NULL,
        "Rate" numeric(9,4),
        "Amount" numeric(19,2),
        "MinPurchaseAmount" numeric(19,2),
        "Period" integer NOT NULL,
        "Description" character varying(500) NOT NULL,
        CONSTRAINT "PK_PurchaseContractRebate" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PurchaseContractRebate_PurchaseContract_ContractId" FOREIGN KEY ("ContractId") REFERENCES "PurchaseContract" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102101751_V0_13_0_ChietKhauTaiTroHopDong') THEN
    CREATE TABLE "ContractRebateReceipt" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "ContractId" uuid NOT NULL,
        "RebateId" uuid NOT NULL,
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
        CONSTRAINT "PK_ContractRebateReceipt" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ContractRebateReceipt_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ContractRebateReceipt_PurchaseContractRebate_RebateId" FOREIGN KEY ("RebateId") REFERENCES "PurchaseContractRebate" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ContractRebateReceipt_PurchaseContract_ContractId" FOREIGN KEY ("ContractId") REFERENCES "PurchaseContract" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102101751_V0_13_0_ChietKhauTaiTroHopDong') THEN
    CREATE INDEX "IX_ContractRebateReceipt_CompanyId" ON "ContractRebateReceipt" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102101751_V0_13_0_ChietKhauTaiTroHopDong') THEN
    CREATE INDEX "IX_ContractRebateReceipt_ContractId_RebateId_PeriodFrom" ON "ContractRebateReceipt" ("ContractId", "RebateId", "PeriodFrom");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102101751_V0_13_0_ChietKhauTaiTroHopDong') THEN
    CREATE INDEX "IX_ContractRebateReceipt_RebateId" ON "ContractRebateReceipt" ("RebateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102101751_V0_13_0_ChietKhauTaiTroHopDong') THEN
    CREATE INDEX "IX_PurchaseContractRebate_ContractId_LineNo" ON "PurchaseContractRebate" ("ContractId", "LineNo");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102101751_V0_13_0_ChietKhauTaiTroHopDong') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261102101751_V0_13_0_ChietKhauTaiTroHopDong', '10.0.14');
    END IF;
END $EF$;
COMMIT;

