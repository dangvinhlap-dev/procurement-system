START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111043639_V0_23_0_DinhKemBuocDuyet') THEN
    ALTER TABLE "Attachment" ADD "AddedAfterApproval" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111043639_V0_23_0_DinhKemBuocDuyet') THEN
    ALTER TABLE "Attachment" ADD "ApprovalStepId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111043639_V0_23_0_DinhKemBuocDuyet') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261111043639_V0_23_0_DinhKemBuocDuyet', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111060509_V0_23_0_ChucVuNguoiDung') THEN
    ALTER TABLE "User" ADD "Position" character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111060509_V0_23_0_ChucVuNguoiDung') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261111060509_V0_23_0_ChucVuNguoiDung', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111065637_V0_23_0_LyDoHuyPhieu') THEN
    ALTER TABLE "SourcingRequest" ADD "CancelReason" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111065637_V0_23_0_LyDoHuyPhieu') THEN
    ALTER TABLE "Rfq" ADD "CancelReason" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111065637_V0_23_0_LyDoHuyPhieu') THEN
    ALTER TABLE "PurchaseContractAmendment" ADD "CancelReason" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111065637_V0_23_0_LyDoHuyPhieu') THEN
    ALTER TABLE "PurchaseContract" ADD "CancelReason" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111065637_V0_23_0_LyDoHuyPhieu') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261111065637_V0_23_0_LyDoHuyPhieu', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111075328_V0_23_0_HuyBangGia') THEN
    ALTER TABLE "SupplierPriceList" ADD "CancelReason" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111075328_V0_23_0_HuyBangGia') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261111075328_V0_23_0_HuyBangGia', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111082758_V0_23_0_CongTrinhDuToan') THEN
    CREATE TABLE "ConstructionProject" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "Code" character varying(30) NOT NULL,
        "Name" character varying(300) NOT NULL,
        "DepartmentId" uuid NOT NULL,
        "Location" character varying(300),
        "StartDate" date,
        "ExpectedEndDate" date,
        "EstimateAmount" numeric(19,2) NOT NULL,
        "Status" integer NOT NULL,
        "Note" character varying(500),
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_ConstructionProject" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ConstructionProject_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ConstructionProject_Department_DepartmentId_CompanyId" FOREIGN KEY ("DepartmentId", "CompanyId") REFERENCES "Department" ("Id", "CompanyId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111082758_V0_23_0_CongTrinhDuToan') THEN
    CREATE TABLE "ConstructionProjectEstimateRevision" (
        "Id" uuid NOT NULL,
        "ProjectId" uuid NOT NULL,
        "RevisionNo" integer NOT NULL,
        "EffectiveDate" date NOT NULL,
        "PreviousAmount" numeric(19,2) NOT NULL,
        "NewAmount" numeric(19,2) NOT NULL,
        "Basis" character varying(500),
        "Reason" character varying(500),
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_ConstructionProjectEstimateRevision" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ConstructionProjectEstimateRevision_ConstructionProject_Pro~" FOREIGN KEY ("ProjectId") REFERENCES "ConstructionProject" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111082758_V0_23_0_CongTrinhDuToan') THEN
    CREATE UNIQUE INDEX "IX_ConstructionProject_CompanyId_Code" ON "ConstructionProject" ("CompanyId", "Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111082758_V0_23_0_CongTrinhDuToan') THEN
    CREATE INDEX "IX_ConstructionProject_DepartmentId_CompanyId" ON "ConstructionProject" ("DepartmentId", "CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111082758_V0_23_0_CongTrinhDuToan') THEN
    CREATE UNIQUE INDEX "IX_ConstructionProjectEstimateRevision_ProjectId_RevisionNo" ON "ConstructionProjectEstimateRevision" ("ProjectId", "RevisionNo");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111082758_V0_23_0_CongTrinhDuToan') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261111082758_V0_23_0_CongTrinhDuToan', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111085332_V0_23_0_LoaiMuaDeNghi') THEN
    ALTER TABLE "PurchaseRequest" ADD "Category" integer NOT NULL DEFAULT 2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111085332_V0_23_0_LoaiMuaDeNghi') THEN
    ALTER TABLE "PurchaseRequest" ADD "ConstructionProjectId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111085332_V0_23_0_LoaiMuaDeNghi') THEN
    ALTER TABLE "ConstructionProject" ADD CONSTRAINT "AK_ConstructionProject_Id_CompanyId" UNIQUE ("Id", "CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111085332_V0_23_0_LoaiMuaDeNghi') THEN
    CREATE INDEX "IX_PurchaseRequest_ConstructionProjectId_CompanyId" ON "PurchaseRequest" ("ConstructionProjectId", "CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111085332_V0_23_0_LoaiMuaDeNghi') THEN
    ALTER TABLE "PurchaseRequest" ADD CONSTRAINT "FK_PurchaseRequest_ConstructionProject_ConstructionProjectId_C~" FOREIGN KEY ("ConstructionProjectId", "CompanyId") REFERENCES "ConstructionProject" ("Id", "CompanyId") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111085332_V0_23_0_LoaiMuaDeNghi') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261111085332_V0_23_0_LoaiMuaDeNghi', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111091658_V0_23_0_ThuKhoNhapTon') THEN
    ALTER TABLE "PurchaseRequestLine" ADD "RequiredQty" numeric(18,4) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111091658_V0_23_0_ThuKhoNhapTon') THEN
    UPDATE "PurchaseRequestLine" SET "RequiredQty" = "Qty";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111091658_V0_23_0_ThuKhoNhapTon') THEN
    ALTER TABLE "PurchaseRequestLine" ADD "StockQty" numeric(18,4);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111091658_V0_23_0_ThuKhoNhapTon') THEN
    ALTER TABLE "PurchaseRequest" ADD "StockConfirmedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111091658_V0_23_0_ThuKhoNhapTon') THEN
    ALTER TABLE "PurchaseRequest" ADD "StockConfirmedBy" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111091658_V0_23_0_ThuKhoNhapTon') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261111091658_V0_23_0_ThuKhoNhapTon', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111094318_V0_23_0_BuocThuKho') THEN
    ALTER TABLE "ApprovalStep" ADD "WarehouseId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111094318_V0_23_0_BuocThuKho') THEN
    CREATE INDEX "IX_ApprovalStep_WarehouseId" ON "ApprovalStep" ("WarehouseId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111094318_V0_23_0_BuocThuKho') THEN
    ALTER TABLE "ApprovalStep" ADD CONSTRAINT "FK_ApprovalStep_Warehouse_WarehouseId" FOREIGN KEY ("WarehouseId") REFERENCES "Warehouse" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111094318_V0_23_0_BuocThuKho') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261111094318_V0_23_0_BuocThuKho', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111101921_V0_23_0_DonViMua') THEN
    CREATE TABLE "PurchasingUnit" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "Code" character varying(20) NOT NULL,
        "Name" character varying(200) NOT NULL,
        "Note" character varying(500),
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_PurchasingUnit" PRIMARY KEY ("Id"),
        CONSTRAINT "AK_PurchasingUnit_Id_CompanyId" UNIQUE ("Id", "CompanyId"),
        CONSTRAINT "FK_PurchasingUnit_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111101921_V0_23_0_DonViMua') THEN
    CREATE UNIQUE INDEX "IX_PurchasingUnit_CompanyId_Code" ON "PurchasingUnit" ("CompanyId", "Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111101921_V0_23_0_DonViMua') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261111101921_V0_23_0_DonViMua', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111104256_V0_23_0_BangTongHopDeXuat') THEN
    CREATE TABLE "PurchaseSummary" (
        "Id" uuid NOT NULL,
        "Form" integer NOT NULL,
        "DepartmentId" uuid NOT NULL,
        "ConstructionProjectId" uuid,
        "TotalAmount" numeric(19,2) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        "CompanyId" uuid NOT NULL,
        "No" character varying(50) NOT NULL,
        "Date" date NOT NULL,
        "Status" integer NOT NULL,
        "ApprovalRound" integer NOT NULL,
        "RejectReason" character varying(500),
        "ApprovedAt" timestamp with time zone,
        "Note" character varying(1000),
        "ClosedReason" character varying(500),
        "ClosedBy" uuid,
        "ClosedAt" timestamp with time zone,
        "BudgetAmount" numeric(19,2),
        "BudgetUsedAmount" numeric(19,2),
        "IsOverBudget" boolean NOT NULL,
        "CancelReason" character varying(500),
        CONSTRAINT "PK_PurchaseSummary" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PurchaseSummary_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseSummary_ConstructionProject_ConstructionProjectId_C~" FOREIGN KEY ("ConstructionProjectId", "CompanyId") REFERENCES "ConstructionProject" ("Id", "CompanyId") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseSummary_Department_DepartmentId_CompanyId" FOREIGN KEY ("DepartmentId", "CompanyId") REFERENCES "Department" ("Id", "CompanyId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111104256_V0_23_0_BangTongHopDeXuat') THEN
    CREATE TABLE "PurchaseSummaryLine" (
        "Id" uuid NOT NULL,
        "SummaryId" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "LineNo" integer NOT NULL,
        "RequestId" uuid NOT NULL,
        "RequestLineId" uuid NOT NULL,
        "ItemId" uuid NOT NULL,
        "UomId" uuid NOT NULL,
        "Qty" numeric(18,4) NOT NULL,
        "UomFactor" numeric(18,6) NOT NULL,
        "BaseQty" numeric(18,4) NOT NULL,
        "EstPrice" numeric(19,6) NOT NULL,
        "Amount" numeric(19,2) NOT NULL,
        "NeededDate" date,
        "PurchasingUnitId" uuid NOT NULL,
        "Note" character varying(500),
        CONSTRAINT "PK_PurchaseSummaryLine" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PurchaseSummaryLine_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseSummaryLine_Item_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Item" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseSummaryLine_PurchaseRequestLine_RequestLineId" FOREIGN KEY ("RequestLineId") REFERENCES "PurchaseRequestLine" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseSummaryLine_PurchaseRequest_RequestId" FOREIGN KEY ("RequestId") REFERENCES "PurchaseRequest" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseSummaryLine_PurchaseSummary_SummaryId" FOREIGN KEY ("SummaryId") REFERENCES "PurchaseSummary" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_PurchaseSummaryLine_PurchasingUnit_PurchasingUnitId_Company~" FOREIGN KEY ("PurchasingUnitId", "CompanyId") REFERENCES "PurchasingUnit" ("Id", "CompanyId") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseSummaryLine_Uom_UomId" FOREIGN KEY ("UomId") REFERENCES "Uom" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111104256_V0_23_0_BangTongHopDeXuat') THEN
    CREATE INDEX "IX_PurchaseSummary_CompanyId_Date" ON "PurchaseSummary" ("CompanyId", "Date");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111104256_V0_23_0_BangTongHopDeXuat') THEN
    CREATE UNIQUE INDEX "IX_PurchaseSummary_CompanyId_No" ON "PurchaseSummary" ("CompanyId", "No");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111104256_V0_23_0_BangTongHopDeXuat') THEN
    CREATE INDEX "IX_PurchaseSummary_ConstructionProjectId_CompanyId" ON "PurchaseSummary" ("ConstructionProjectId", "CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111104256_V0_23_0_BangTongHopDeXuat') THEN
    CREATE INDEX "IX_PurchaseSummary_DepartmentId_CompanyId" ON "PurchaseSummary" ("DepartmentId", "CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111104256_V0_23_0_BangTongHopDeXuat') THEN
    CREATE INDEX "IX_PurchaseSummaryLine_CompanyId" ON "PurchaseSummaryLine" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111104256_V0_23_0_BangTongHopDeXuat') THEN
    CREATE INDEX "IX_PurchaseSummaryLine_ItemId" ON "PurchaseSummaryLine" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111104256_V0_23_0_BangTongHopDeXuat') THEN
    CREATE INDEX "IX_PurchaseSummaryLine_PurchasingUnitId_CompanyId" ON "PurchaseSummaryLine" ("PurchasingUnitId", "CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111104256_V0_23_0_BangTongHopDeXuat') THEN
    CREATE INDEX "IX_PurchaseSummaryLine_RequestId" ON "PurchaseSummaryLine" ("RequestId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111104256_V0_23_0_BangTongHopDeXuat') THEN
    CREATE INDEX "IX_PurchaseSummaryLine_RequestLineId" ON "PurchaseSummaryLine" ("RequestLineId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111104256_V0_23_0_BangTongHopDeXuat') THEN
    CREATE INDEX "IX_PurchaseSummaryLine_SummaryId_LineNo" ON "PurchaseSummaryLine" ("SummaryId", "LineNo");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111104256_V0_23_0_BangTongHopDeXuat') THEN
    CREATE INDEX "IX_PurchaseSummaryLine_UomId" ON "PurchaseSummaryLine" ("UomId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261111104256_V0_23_0_BangTongHopDeXuat') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261111104256_V0_23_0_BangTongHopDeXuat', '10.0.14');
    END IF;
END $EF$;
COMMIT;

