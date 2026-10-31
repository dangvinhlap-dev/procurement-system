START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    ALTER TABLE "InventoryDocumentLine" ADD "PurchaseOrderLineId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    ALTER TABLE "InventoryDocument" ADD "PurchaseOrderId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    CREATE TABLE "ApprovalRule" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "DocType" character varying(10) NOT NULL,
        "Level" integer NOT NULL,
        "MinAmount" numeric(19,2) NOT NULL,
        "RoleId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_ApprovalRule" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ApprovalRule_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ApprovalRule_Role_RoleId" FOREIGN KEY ("RoleId") REFERENCES "Role" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    CREATE TABLE "ApprovalStep" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "DocType" character varying(10) NOT NULL,
        "DocumentId" uuid NOT NULL,
        "Round" integer NOT NULL,
        "Level" integer NOT NULL,
        "RoleId" uuid,
        "Status" integer NOT NULL,
        "ActedBy" uuid,
        "ActedAt" timestamp with time zone,
        "Comment" character varying(500),
        CONSTRAINT "PK_ApprovalStep" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ApprovalStep_Role_RoleId" FOREIGN KEY ("RoleId") REFERENCES "Role" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    CREATE TABLE "PurchaseOrder" (
        "Id" uuid NOT NULL,
        "SupplierId" uuid NOT NULL,
        "WarehouseId" uuid NOT NULL,
        "ExpectedDate" date,
        "PaymentTerms" character varying(200),
        "DeliveryTerms" character varying(200),
        "DeliveryAddress" character varying(500),
        "CurrencyCode" character varying(3) NOT NULL,
        "ReceiveTolerancePct" numeric(9,4) NOT NULL,
        "GrossAmount" numeric(19,2) NOT NULL,
        "TotalDiscount" numeric(19,2) NOT NULL,
        "TotalAmount" numeric(19,2) NOT NULL,
        "TotalVat" numeric(19,2) NOT NULL,
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
        CONSTRAINT "PK_PurchaseOrder" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PurchaseOrder_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseOrder_Supplier_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseOrder_Warehouse_WarehouseId_CompanyId" FOREIGN KEY ("WarehouseId", "CompanyId") REFERENCES "Warehouse" ("Id", "CompanyId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    CREATE TABLE "PurchaseRequest" (
        "Id" uuid NOT NULL,
        "Department" character varying(200),
        "RequesterId" uuid,
        "Purpose" character varying(500),
        "NeededDate" date,
        "WarehouseId" uuid,
        "Priority" integer NOT NULL,
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
        CONSTRAINT "PK_PurchaseRequest" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PurchaseRequest_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseRequest_Warehouse_WarehouseId_CompanyId" FOREIGN KEY ("WarehouseId", "CompanyId") REFERENCES "Warehouse" ("Id", "CompanyId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    CREATE TABLE "PurchaseRequestLine" (
        "Id" uuid NOT NULL,
        "RequestId" uuid NOT NULL,
        "LineNo" integer NOT NULL,
        "ItemId" uuid NOT NULL,
        "UomId" uuid NOT NULL,
        "Qty" numeric(18,4) NOT NULL,
        "UomFactor" numeric(18,6) NOT NULL,
        "BaseQty" numeric(18,4) NOT NULL,
        "EstPrice" numeric(19,6) NOT NULL,
        "Amount" numeric(19,2) NOT NULL,
        "SuggestedSupplierId" uuid,
        "NeededDate" date,
        "Note" character varying(500),
        CONSTRAINT "PK_PurchaseRequestLine" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PurchaseRequestLine_Item_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Item" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseRequestLine_PurchaseRequest_RequestId" FOREIGN KEY ("RequestId") REFERENCES "PurchaseRequest" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_PurchaseRequestLine_Supplier_SuggestedSupplierId" FOREIGN KEY ("SuggestedSupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseRequestLine_Uom_UomId" FOREIGN KEY ("UomId") REFERENCES "Uom" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    CREATE TABLE "PurchaseOrderLine" (
        "Id" uuid NOT NULL,
        "OrderId" uuid NOT NULL,
        "LineNo" integer NOT NULL,
        "ItemId" uuid NOT NULL,
        "UomId" uuid NOT NULL,
        "Qty" numeric(18,4) NOT NULL,
        "UomFactor" numeric(18,6) NOT NULL,
        "BaseQty" numeric(18,4) NOT NULL,
        "UnitPrice" numeric(19,6) NOT NULL,
        "DiscountPct" numeric(9,4) NOT NULL,
        "GrossAmount" numeric(19,2) NOT NULL,
        "DiscountAmount" numeric(19,2) NOT NULL,
        "Amount" numeric(19,2) NOT NULL,
        "VatCode" character varying(5),
        "VatAmount" numeric(19,2) NOT NULL,
        "DeliveryDate" date,
        "Note" character varying(500),
        "SourcePrLineId" uuid,
        CONSTRAINT "PK_PurchaseOrderLine" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PurchaseOrderLine_Item_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Item" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseOrderLine_PurchaseOrder_OrderId" FOREIGN KEY ("OrderId") REFERENCES "PurchaseOrder" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_PurchaseOrderLine_PurchaseRequestLine_SourcePrLineId" FOREIGN KEY ("SourcePrLineId") REFERENCES "PurchaseRequestLine" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseOrderLine_Uom_UomId" FOREIGN KEY ("UomId") REFERENCES "Uom" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    CREATE INDEX "IX_InventoryDocumentLine_PurchaseOrderLineId" ON "InventoryDocumentLine" ("PurchaseOrderLineId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    CREATE INDEX "IX_InventoryDocument_PurchaseOrderId" ON "InventoryDocument" ("PurchaseOrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    CREATE UNIQUE INDEX "IX_ApprovalRule_CompanyId_DocType_Level" ON "ApprovalRule" ("CompanyId", "DocType", "Level");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    CREATE INDEX "IX_ApprovalRule_RoleId" ON "ApprovalRule" ("RoleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    CREATE INDEX "IX_ApprovalStep_CompanyId_Status" ON "ApprovalStep" ("CompanyId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    CREATE INDEX "IX_ApprovalStep_DocType_DocumentId_Round_Level" ON "ApprovalStep" ("DocType", "DocumentId", "Round", "Level");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    CREATE INDEX "IX_ApprovalStep_RoleId" ON "ApprovalStep" ("RoleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    CREATE INDEX "IX_PurchaseOrder_CompanyId_Date" ON "PurchaseOrder" ("CompanyId", "Date");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    CREATE UNIQUE INDEX "IX_PurchaseOrder_CompanyId_No" ON "PurchaseOrder" ("CompanyId", "No");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    CREATE INDEX "IX_PurchaseOrder_SupplierId" ON "PurchaseOrder" ("SupplierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    CREATE INDEX "IX_PurchaseOrder_WarehouseId_CompanyId" ON "PurchaseOrder" ("WarehouseId", "CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    CREATE INDEX "IX_PurchaseOrderLine_ItemId" ON "PurchaseOrderLine" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    CREATE INDEX "IX_PurchaseOrderLine_OrderId_LineNo" ON "PurchaseOrderLine" ("OrderId", "LineNo");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    CREATE INDEX "IX_PurchaseOrderLine_SourcePrLineId" ON "PurchaseOrderLine" ("SourcePrLineId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    CREATE INDEX "IX_PurchaseOrderLine_UomId" ON "PurchaseOrderLine" ("UomId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    CREATE INDEX "IX_PurchaseRequest_CompanyId_Date" ON "PurchaseRequest" ("CompanyId", "Date");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    CREATE UNIQUE INDEX "IX_PurchaseRequest_CompanyId_No" ON "PurchaseRequest" ("CompanyId", "No");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    CREATE INDEX "IX_PurchaseRequest_WarehouseId_CompanyId" ON "PurchaseRequest" ("WarehouseId", "CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    CREATE INDEX "IX_PurchaseRequestLine_ItemId" ON "PurchaseRequestLine" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    CREATE INDEX "IX_PurchaseRequestLine_RequestId_LineNo" ON "PurchaseRequestLine" ("RequestId", "LineNo");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    CREATE INDEX "IX_PurchaseRequestLine_SuggestedSupplierId" ON "PurchaseRequestLine" ("SuggestedSupplierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    CREATE INDEX "IX_PurchaseRequestLine_UomId" ON "PurchaseRequestLine" ("UomId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    ALTER TABLE "InventoryDocument" ADD CONSTRAINT "FK_InventoryDocument_PurchaseOrder_PurchaseOrderId" FOREIGN KEY ("PurchaseOrderId") REFERENCES "PurchaseOrder" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    ALTER TABLE "InventoryDocumentLine" ADD CONSTRAINT "FK_InventoryDocumentLine_PurchaseOrderLine_PurchaseOrderLineId" FOREIGN KEY ("PurchaseOrderLineId") REFERENCES "PurchaseOrderLine" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030105556_V0_6_0_MuaHang') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261030105556_V0_6_0_MuaHang', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031002555_V0_7_0_BangGiaNcc') THEN
    CREATE TABLE "SupplierPriceList" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "No" character varying(50) NOT NULL,
        "SupplierId" uuid NOT NULL,
        "Title" character varying(200) NOT NULL,
        "IsGroupWide" boolean NOT NULL,
        "ValidFrom" date NOT NULL,
        "ValidTo" date,
        "CurrencyCode" character varying(3) NOT NULL,
        "DeliveryTerms" character varying(200),
        "Note" character varying(1000),
        "Status" integer NOT NULL,
        "ActivatedAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_SupplierPriceList" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SupplierPriceList_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_SupplierPriceList_Supplier_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031002555_V0_7_0_BangGiaNcc') THEN
    CREATE TABLE "SupplierPriceListLine" (
        "Id" uuid NOT NULL,
        "PriceListId" uuid NOT NULL,
        "LineNo" integer NOT NULL,
        "ItemId" uuid NOT NULL,
        "UomId" uuid NOT NULL,
        "UomFactor" numeric(18,6) NOT NULL,
        "MinQty" numeric(18,4) NOT NULL,
        "UnitPrice" numeric(19,6) NOT NULL,
        "DiscountPct" numeric(9,4) NOT NULL,
        "VatCode" character varying(5),
        "Note" character varying(500),
        CONSTRAINT "PK_SupplierPriceListLine" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SupplierPriceListLine_Item_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Item" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_SupplierPriceListLine_SupplierPriceList_PriceListId" FOREIGN KEY ("PriceListId") REFERENCES "SupplierPriceList" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_SupplierPriceListLine_Uom_UomId" FOREIGN KEY ("UomId") REFERENCES "Uom" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031002555_V0_7_0_BangGiaNcc') THEN
    CREATE UNIQUE INDEX "IX_SupplierPriceList_CompanyId_No" ON "SupplierPriceList" ("CompanyId", "No");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031002555_V0_7_0_BangGiaNcc') THEN
    CREATE INDEX "IX_SupplierPriceList_SupplierId_Status_ValidFrom" ON "SupplierPriceList" ("SupplierId", "Status", "ValidFrom");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031002555_V0_7_0_BangGiaNcc') THEN
    CREATE INDEX "IX_SupplierPriceListLine_ItemId" ON "SupplierPriceListLine" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031002555_V0_7_0_BangGiaNcc') THEN
    CREATE INDEX "IX_SupplierPriceListLine_PriceListId_LineNo" ON "SupplierPriceListLine" ("PriceListId", "LineNo");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031002555_V0_7_0_BangGiaNcc') THEN
    CREATE INDEX "IX_SupplierPriceListLine_UomId" ON "SupplierPriceListLine" ("UomId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031002555_V0_7_0_BangGiaNcc') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261031002555_V0_7_0_BangGiaNcc', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031004652_V0_7_0_TimNguonDauThau') THEN
    CREATE TABLE "SourcingRequest" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "No" character varying(50) NOT NULL,
        "Date" date NOT NULL,
        "ItemId" uuid,
        "Description" character varying(500),
        "Specification" character varying(2000),
        "Qty" numeric(18,4),
        "Deadline" date,
        "Responsible" character varying(200),
        "Note" character varying(1000),
        "Status" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_SourcingRequest" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SourcingRequest_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_SourcingRequest_Item_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Item" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031004652_V0_7_0_TimNguonDauThau') THEN
    CREATE TABLE "Rfq" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "No" character varying(50) NOT NULL,
        "Date" date NOT NULL,
        "Title" character varying(200) NOT NULL,
        "Deadline" date NOT NULL,
        "SourcingId" uuid,
        "PriceWeight" integer NOT NULL,
        "QualityWeight" integer NOT NULL,
        "DeliveryWeight" integer NOT NULL,
        "PaymentWeight" integer NOT NULL,
        "WarrantyWeight" integer NOT NULL,
        "Note" character varying(1000),
        "Status" integer NOT NULL,
        "SentAt" timestamp with time zone,
        "AwardedAt" timestamp with time zone,
        "AwardDate" date,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_Rfq" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_Rfq_Weights" CHECK ("PriceWeight" + "QualityWeight" + "DeliveryWeight" + "PaymentWeight" + "WarrantyWeight" = 100),
        CONSTRAINT "FK_Rfq_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Rfq_SourcingRequest_SourcingId" FOREIGN KEY ("SourcingId") REFERENCES "SourcingRequest" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031004652_V0_7_0_TimNguonDauThau') THEN
    CREATE TABLE "SourcingCandidate" (
        "Id" uuid NOT NULL,
        "SourcingId" uuid NOT NULL,
        "SupplierId" uuid NOT NULL,
        "Status" integer NOT NULL,
        "Note" character varying(500),
        CONSTRAINT "PK_SourcingCandidate" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SourcingCandidate_SourcingRequest_SourcingId" FOREIGN KEY ("SourcingId") REFERENCES "SourcingRequest" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_SourcingCandidate_Supplier_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031004652_V0_7_0_TimNguonDauThau') THEN
    CREATE TABLE "RfqLine" (
        "Id" uuid NOT NULL,
        "RfqId" uuid NOT NULL,
        "LineNo" integer NOT NULL,
        "ItemId" uuid NOT NULL,
        "UomId" uuid NOT NULL,
        "UomFactor" numeric(18,6) NOT NULL,
        "Qty" numeric(18,4) NOT NULL,
        "SourcePrLineId" uuid,
        "Note" character varying(500),
        "AwardedSupplierId" uuid,
        "AwardReason" character varying(500),
        CONSTRAINT "PK_RfqLine" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_RfqLine_Item_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Item" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_RfqLine_PurchaseRequestLine_SourcePrLineId" FOREIGN KEY ("SourcePrLineId") REFERENCES "PurchaseRequestLine" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_RfqLine_Rfq_RfqId" FOREIGN KEY ("RfqId") REFERENCES "Rfq" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_RfqLine_Supplier_AwardedSupplierId" FOREIGN KEY ("AwardedSupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_RfqLine_Uom_UomId" FOREIGN KEY ("UomId") REFERENCES "Uom" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031004652_V0_7_0_TimNguonDauThau') THEN
    CREATE TABLE "RfqSupplier" (
        "Id" uuid NOT NULL,
        "RfqId" uuid NOT NULL,
        "SupplierId" uuid NOT NULL,
        "HasQuoted" boolean NOT NULL,
        "QuoteDate" date,
        "ValidUntil" date,
        "PaymentTerms" character varying(200),
        "QualityScore" numeric(5,2) NOT NULL,
        "DeliveryScore" numeric(5,2) NOT NULL,
        "PaymentScore" numeric(5,2) NOT NULL,
        "WarrantyScore" numeric(5,2) NOT NULL,
        "Note" character varying(1000),
        "PurchaseOrderId" uuid,
        CONSTRAINT "PK_RfqSupplier" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_RfqSupplier_PurchaseOrder_PurchaseOrderId" FOREIGN KEY ("PurchaseOrderId") REFERENCES "PurchaseOrder" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_RfqSupplier_Rfq_RfqId" FOREIGN KEY ("RfqId") REFERENCES "Rfq" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_RfqSupplier_Supplier_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031004652_V0_7_0_TimNguonDauThau') THEN
    CREATE TABLE "QuotationLine" (
        "Id" uuid NOT NULL,
        "RfqSupplierId" uuid NOT NULL,
        "RfqLineId" uuid NOT NULL,
        "UnitPrice" numeric(19,6) NOT NULL,
        "DiscountPct" numeric(9,4) NOT NULL,
        "VatCode" character varying(5),
        "LeadTimeDays" integer,
        "Note" character varying(500),
        CONSTRAINT "PK_QuotationLine" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_QuotationLine_RfqLine_RfqLineId" FOREIGN KEY ("RfqLineId") REFERENCES "RfqLine" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_QuotationLine_RfqSupplier_RfqSupplierId" FOREIGN KEY ("RfqSupplierId") REFERENCES "RfqSupplier" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031004652_V0_7_0_TimNguonDauThau') THEN
    CREATE INDEX "IX_QuotationLine_RfqLineId" ON "QuotationLine" ("RfqLineId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031004652_V0_7_0_TimNguonDauThau') THEN
    CREATE UNIQUE INDEX "IX_QuotationLine_RfqSupplierId_RfqLineId" ON "QuotationLine" ("RfqSupplierId", "RfqLineId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031004652_V0_7_0_TimNguonDauThau') THEN
    CREATE UNIQUE INDEX "IX_Rfq_CompanyId_No" ON "Rfq" ("CompanyId", "No");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031004652_V0_7_0_TimNguonDauThau') THEN
    CREATE INDEX "IX_Rfq_CompanyId_Status" ON "Rfq" ("CompanyId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031004652_V0_7_0_TimNguonDauThau') THEN
    CREATE INDEX "IX_Rfq_SourcingId" ON "Rfq" ("SourcingId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031004652_V0_7_0_TimNguonDauThau') THEN
    CREATE INDEX "IX_RfqLine_AwardedSupplierId" ON "RfqLine" ("AwardedSupplierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031004652_V0_7_0_TimNguonDauThau') THEN
    CREATE INDEX "IX_RfqLine_ItemId" ON "RfqLine" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031004652_V0_7_0_TimNguonDauThau') THEN
    CREATE INDEX "IX_RfqLine_RfqId_LineNo" ON "RfqLine" ("RfqId", "LineNo");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031004652_V0_7_0_TimNguonDauThau') THEN
    CREATE INDEX "IX_RfqLine_SourcePrLineId" ON "RfqLine" ("SourcePrLineId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031004652_V0_7_0_TimNguonDauThau') THEN
    CREATE INDEX "IX_RfqLine_UomId" ON "RfqLine" ("UomId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031004652_V0_7_0_TimNguonDauThau') THEN
    CREATE INDEX "IX_RfqSupplier_PurchaseOrderId" ON "RfqSupplier" ("PurchaseOrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031004652_V0_7_0_TimNguonDauThau') THEN
    CREATE UNIQUE INDEX "IX_RfqSupplier_RfqId_SupplierId" ON "RfqSupplier" ("RfqId", "SupplierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031004652_V0_7_0_TimNguonDauThau') THEN
    CREATE INDEX "IX_RfqSupplier_SupplierId" ON "RfqSupplier" ("SupplierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031004652_V0_7_0_TimNguonDauThau') THEN
    CREATE UNIQUE INDEX "IX_SourcingCandidate_SourcingId_SupplierId" ON "SourcingCandidate" ("SourcingId", "SupplierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031004652_V0_7_0_TimNguonDauThau') THEN
    CREATE INDEX "IX_SourcingCandidate_SupplierId" ON "SourcingCandidate" ("SupplierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031004652_V0_7_0_TimNguonDauThau') THEN
    CREATE UNIQUE INDEX "IX_SourcingRequest_CompanyId_No" ON "SourcingRequest" ("CompanyId", "No");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031004652_V0_7_0_TimNguonDauThau') THEN
    CREATE INDEX "IX_SourcingRequest_ItemId" ON "SourcingRequest" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031004652_V0_7_0_TimNguonDauThau') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261031004652_V0_7_0_TimNguonDauThau', '10.0.14');
    END IF;
END $EF$;
COMMIT;

