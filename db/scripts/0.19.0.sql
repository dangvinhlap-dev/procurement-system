START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106030556_V0_19_0_PhieuDuyetNccGiaThang') THEN
    CREATE TABLE "MonthlySupplierApproval" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "No" character varying(50) NOT NULL,
        "Period" date NOT NULL,
        "ItemCategoryId" uuid NOT NULL,
        "Note" character varying(1000),
        "Status" integer NOT NULL,
        "ApprovalRound" integer NOT NULL,
        "RejectReason" character varying(500),
        "ApprovedAt" timestamp with time zone,
        "ReplacesId" uuid,
        "CancelledOn" date,
        "CancelReason" character varying(1000),
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_MonthlySupplierApproval" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_MonthlySupplierApproval_Period" CHECK (EXTRACT(DAY FROM "Period") = 1),
        CONSTRAINT "FK_MonthlySupplierApproval_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_MonthlySupplierApproval_ItemCategory_ItemCategoryId" FOREIGN KEY ("ItemCategoryId") REFERENCES "ItemCategory" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_MonthlySupplierApproval_MonthlySupplierApproval_ReplacesId" FOREIGN KEY ("ReplacesId") REFERENCES "MonthlySupplierApproval" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106030556_V0_19_0_PhieuDuyetNccGiaThang') THEN
    CREATE TABLE "MonthlySupplierApprovalCompany" (
        "Id" uuid NOT NULL,
        "ApprovalId" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        CONSTRAINT "PK_MonthlySupplierApprovalCompany" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_MonthlySupplierApprovalCompany_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_MonthlySupplierApprovalCompany_MonthlySupplierApproval_Appr~" FOREIGN KEY ("ApprovalId") REFERENCES "MonthlySupplierApproval" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106030556_V0_19_0_PhieuDuyetNccGiaThang') THEN
    CREATE TABLE "MonthlySupplierApprovalLine" (
        "Id" uuid NOT NULL,
        "ApprovalId" uuid NOT NULL,
        "LineNo" integer NOT NULL,
        "ItemId" uuid NOT NULL,
        "UomId" uuid NOT NULL,
        "UomFactor" numeric(18,6) NOT NULL,
        "MainSupplierId" uuid NOT NULL,
        "UnitPrice" numeric(19,6) NOT NULL,
        "BackupSupplierId" uuid,
        "BackupUnitPrice" numeric(19,6),
        "QuantityLimit" numeric(18,4),
        "Note" character varying(500),
        CONSTRAINT "PK_MonthlySupplierApprovalLine" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_MonthlySupplierApprovalLine_Backup" CHECK ("BackupSupplierId" IS NULL OR "BackupSupplierId" <> "MainSupplierId"),
        CONSTRAINT "FK_MonthlySupplierApprovalLine_Item_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Item" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_MonthlySupplierApprovalLine_MonthlySupplierApproval_Approva~" FOREIGN KEY ("ApprovalId") REFERENCES "MonthlySupplierApproval" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_MonthlySupplierApprovalLine_Supplier_BackupSupplierId" FOREIGN KEY ("BackupSupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_MonthlySupplierApprovalLine_Supplier_MainSupplierId" FOREIGN KEY ("MainSupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_MonthlySupplierApprovalLine_Uom_UomId" FOREIGN KEY ("UomId") REFERENCES "Uom" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106030556_V0_19_0_PhieuDuyetNccGiaThang') THEN
    CREATE UNIQUE INDEX "IX_MonthlySupplierApproval_CompanyId_No" ON "MonthlySupplierApproval" ("CompanyId", "No");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106030556_V0_19_0_PhieuDuyetNccGiaThang') THEN
    CREATE INDEX "IX_MonthlySupplierApproval_ItemCategoryId" ON "MonthlySupplierApproval" ("ItemCategoryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106030556_V0_19_0_PhieuDuyetNccGiaThang') THEN
    CREATE INDEX "IX_MonthlySupplierApproval_Period_Status" ON "MonthlySupplierApproval" ("Period", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106030556_V0_19_0_PhieuDuyetNccGiaThang') THEN
    CREATE INDEX "IX_MonthlySupplierApproval_ReplacesId" ON "MonthlySupplierApproval" ("ReplacesId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106030556_V0_19_0_PhieuDuyetNccGiaThang') THEN
    CREATE UNIQUE INDEX "IX_MonthlySupplierApprovalCompany_ApprovalId_CompanyId" ON "MonthlySupplierApprovalCompany" ("ApprovalId", "CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106030556_V0_19_0_PhieuDuyetNccGiaThang') THEN
    CREATE INDEX "IX_MonthlySupplierApprovalCompany_CompanyId" ON "MonthlySupplierApprovalCompany" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106030556_V0_19_0_PhieuDuyetNccGiaThang') THEN
    CREATE UNIQUE INDEX "IX_MonthlySupplierApprovalLine_ApprovalId_ItemId" ON "MonthlySupplierApprovalLine" ("ApprovalId", "ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106030556_V0_19_0_PhieuDuyetNccGiaThang') THEN
    CREATE INDEX "IX_MonthlySupplierApprovalLine_BackupSupplierId" ON "MonthlySupplierApprovalLine" ("BackupSupplierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106030556_V0_19_0_PhieuDuyetNccGiaThang') THEN
    CREATE INDEX "IX_MonthlySupplierApprovalLine_ItemId" ON "MonthlySupplierApprovalLine" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106030556_V0_19_0_PhieuDuyetNccGiaThang') THEN
    CREATE INDEX "IX_MonthlySupplierApprovalLine_MainSupplierId" ON "MonthlySupplierApprovalLine" ("MainSupplierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106030556_V0_19_0_PhieuDuyetNccGiaThang') THEN
    CREATE INDEX "IX_MonthlySupplierApprovalLine_UomId" ON "MonthlySupplierApprovalLine" ("UomId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106030556_V0_19_0_PhieuDuyetNccGiaThang') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261106030556_V0_19_0_PhieuDuyetNccGiaThang', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106041505_V0_19_0_PoTheoPhieuThang') THEN
    ALTER TABLE "PurchaseOrderLine" ADD "MonthlyApprovalLineId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106041505_V0_19_0_PoTheoPhieuThang') THEN
    ALTER TABLE "PurchaseOrder" ADD "IsOffApprovedList" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106041505_V0_19_0_PoTheoPhieuThang') THEN
    CREATE INDEX "IX_PurchaseOrderLine_MonthlyApprovalLineId" ON "PurchaseOrderLine" ("MonthlyApprovalLineId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106041505_V0_19_0_PoTheoPhieuThang') THEN
    ALTER TABLE "PurchaseOrderLine" ADD CONSTRAINT "FK_PurchaseOrderLine_MonthlySupplierApprovalLine_MonthlyApprov~" FOREIGN KEY ("MonthlyApprovalLineId") REFERENCES "MonthlySupplierApprovalLine" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106041505_V0_19_0_PoTheoPhieuThang') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261106041505_V0_19_0_PoTheoPhieuThang', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106044409_V0_19_0_KhaoSatGiaThiTruong') THEN
    CREATE TABLE "MarketPriceSurvey" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "No" character varying(50) NOT NULL,
        "SurveyDate" date NOT NULL,
        "ItemCategoryId" uuid NOT NULL,
        "Note" character varying(1000),
        "Status" integer NOT NULL,
        "ConfirmedAt" timestamp with time zone,
        "ConfirmedBy" uuid,
        "CancelledOn" date,
        "CancelReason" character varying(1000),
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_MarketPriceSurvey" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_MarketPriceSurvey_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_MarketPriceSurvey_ItemCategory_ItemCategoryId" FOREIGN KEY ("ItemCategoryId") REFERENCES "ItemCategory" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106044409_V0_19_0_KhaoSatGiaThiTruong') THEN
    CREATE TABLE "MarketPriceSurveyLine" (
        "Id" uuid NOT NULL,
        "SurveyId" uuid NOT NULL,
        "LineNo" integer NOT NULL,
        "ItemId" uuid NOT NULL,
        "UomId" uuid NOT NULL,
        "UomFactor" numeric(18,6) NOT NULL,
        "SupplierId" uuid,
        "SourceName" character varying(200),
        "UnitPrice" numeric(19,6) NOT NULL,
        "Note" character varying(500),
        CONSTRAINT "PK_MarketPriceSurveyLine" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_MarketPriceSurveyLine_Source" CHECK ("SupplierId" IS NOT NULL OR "SourceName" IS NOT NULL),
        CONSTRAINT "FK_MarketPriceSurveyLine_Item_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Item" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_MarketPriceSurveyLine_MarketPriceSurvey_SurveyId" FOREIGN KEY ("SurveyId") REFERENCES "MarketPriceSurvey" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_MarketPriceSurveyLine_Supplier_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_MarketPriceSurveyLine_Uom_UomId" FOREIGN KEY ("UomId") REFERENCES "Uom" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106044409_V0_19_0_KhaoSatGiaThiTruong') THEN
    CREATE UNIQUE INDEX "IX_MarketPriceSurvey_CompanyId_No" ON "MarketPriceSurvey" ("CompanyId", "No");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106044409_V0_19_0_KhaoSatGiaThiTruong') THEN
    CREATE INDEX "IX_MarketPriceSurvey_ItemCategoryId" ON "MarketPriceSurvey" ("ItemCategoryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106044409_V0_19_0_KhaoSatGiaThiTruong') THEN
    CREATE INDEX "IX_MarketPriceSurvey_SurveyDate_Status" ON "MarketPriceSurvey" ("SurveyDate", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106044409_V0_19_0_KhaoSatGiaThiTruong') THEN
    CREATE INDEX "IX_MarketPriceSurveyLine_ItemId" ON "MarketPriceSurveyLine" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106044409_V0_19_0_KhaoSatGiaThiTruong') THEN
    CREATE INDEX "IX_MarketPriceSurveyLine_SupplierId" ON "MarketPriceSurveyLine" ("SupplierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106044409_V0_19_0_KhaoSatGiaThiTruong') THEN
    CREATE INDEX "IX_MarketPriceSurveyLine_SurveyId" ON "MarketPriceSurveyLine" ("SurveyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106044409_V0_19_0_KhaoSatGiaThiTruong') THEN
    CREATE INDEX "IX_MarketPriceSurveyLine_UomId" ON "MarketPriceSurveyLine" ("UomId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106044409_V0_19_0_KhaoSatGiaThiTruong') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261106044409_V0_19_0_KhaoSatGiaThiTruong', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106055955_V0_19_0_GiaChuanTranMucTieu') THEN
    ALTER TABLE "MonthlySupplierApprovalLine" ADD "CeilingPrice" numeric(19,6);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106055955_V0_19_0_GiaChuanTranMucTieu') THEN
    ALTER TABLE "MonthlySupplierApprovalLine" ADD "StandardPrice" numeric(19,6);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106055955_V0_19_0_GiaChuanTranMucTieu') THEN
    ALTER TABLE "MonthlySupplierApprovalLine" ADD "TargetPrice" numeric(19,6);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106055955_V0_19_0_GiaChuanTranMucTieu') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261106055955_V0_19_0_GiaChuanTranMucTieu', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106093020_V0_19_0_KhoaSuaTrung') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261106093020_V0_19_0_KhoaSuaTrung', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106101607_V0_19_0_NhapMuaTheoPo') THEN
    ALTER TABLE "CompanyParameter" ADD "RequireReceiptPo" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106101607_V0_19_0_NhapMuaTheoPo') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261106101607_V0_19_0_NhapMuaTheoPo', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106102843_V0_19_0_DongPhanConLai') THEN
    ALTER TABLE "PurchaseRequest" ADD "ClosedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106102843_V0_19_0_DongPhanConLai') THEN
    ALTER TABLE "PurchaseRequest" ADD "ClosedBy" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106102843_V0_19_0_DongPhanConLai') THEN
    ALTER TABLE "PurchaseRequest" ADD "ClosedReason" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106102843_V0_19_0_DongPhanConLai') THEN
    ALTER TABLE "PurchaseOrder" ADD "ClosedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106102843_V0_19_0_DongPhanConLai') THEN
    ALTER TABLE "PurchaseOrder" ADD "ClosedBy" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106102843_V0_19_0_DongPhanConLai') THEN
    ALTER TABLE "PurchaseOrder" ADD "ClosedReason" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106102843_V0_19_0_DongPhanConLai') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261106102843_V0_19_0_DongPhanConLai', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106104102_V0_19_0_RfqHanNopLyDoHuy') THEN
    ALTER TABLE "Rfq" ADD "DeadlineExtensionReason" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106104102_V0_19_0_RfqHanNopLyDoHuy') THEN
    ALTER TABLE "Rfq" ADD "FewQuotesReason" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106104102_V0_19_0_RfqHanNopLyDoHuy') THEN
    ALTER TABLE "PurchaseRequest" ADD "CancelReason" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106104102_V0_19_0_RfqHanNopLyDoHuy') THEN
    ALTER TABLE "PurchaseOrder" ADD "CancelReason" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106104102_V0_19_0_RfqHanNopLyDoHuy') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261106104102_V0_19_0_RfqHanNopLyDoHuy', '10.0.14');
    END IF;
END $EF$;
COMMIT;

