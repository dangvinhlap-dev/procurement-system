START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030003708_V0_2_0_CongTyKho') THEN
    CREATE UNIQUE INDEX "IX_Company_TaxCode" ON "Company" ("TaxCode") WHERE "TaxCode" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030003708_V0_2_0_CongTyKho') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261030003708_V0_2_0_CongTyKho', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005536_V0_2_0_HangHoa') THEN
    ALTER TABLE "ItemCategory" ADD "InventoryAccount" character varying(10);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005536_V0_2_0_HangHoa') THEN
    CREATE TABLE "Uom" (
        "Id" uuid NOT NULL,
        "Code" character varying(10) NOT NULL,
        "Name" character varying(50) NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_Uom" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005536_V0_2_0_HangHoa') THEN
    CREATE TABLE "Item" (
        "Id" uuid NOT NULL,
        "Code" character varying(30) NOT NULL,
        "Name" character varying(300) NOT NULL,
        "EnglishName" character varying(300),
        "Type" integer NOT NULL,
        "CategoryId" uuid NOT NULL,
        "BaseUomId" uuid NOT NULL,
        "Specification" character varying(500),
        "Brand" character varying(100),
        "Origin" character varying(100),
        "Barcode" character varying(50),
        "VatCode" character varying(5),
        "TechnicalSpecs" character varying(4000),
        "TrackLot" boolean NOT NULL,
        "TrackExpiry" boolean NOT NULL,
        "MinStock" numeric(18,4),
        "MaxStock" numeric(18,4),
        "IsActive" boolean NOT NULL,
        "SearchText" character varying(800) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_Item" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Item_ItemCategory_CategoryId" FOREIGN KEY ("CategoryId") REFERENCES "ItemCategory" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Item_Uom_BaseUomId" FOREIGN KEY ("BaseUomId") REFERENCES "Uom" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005536_V0_2_0_HangHoa') THEN
    CREATE TABLE "ItemUomConversion" (
        "Id" uuid NOT NULL,
        "ItemId" uuid NOT NULL,
        "UomId" uuid NOT NULL,
        "Factor" numeric(18,6) NOT NULL,
        CONSTRAINT "PK_ItemUomConversion" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ItemUomConversion_Item_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Item" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_ItemUomConversion_Uom_UomId" FOREIGN KEY ("UomId") REFERENCES "Uom" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005536_V0_2_0_HangHoa') THEN
    CREATE INDEX "IX_Item_Barcode" ON "Item" ("Barcode");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005536_V0_2_0_HangHoa') THEN
    CREATE INDEX "IX_Item_BaseUomId" ON "Item" ("BaseUomId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005536_V0_2_0_HangHoa') THEN
    CREATE INDEX "IX_Item_CategoryId" ON "Item" ("CategoryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005536_V0_2_0_HangHoa') THEN
    CREATE UNIQUE INDEX "IX_Item_Code" ON "Item" ("Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005536_V0_2_0_HangHoa') THEN
    CREATE UNIQUE INDEX "IX_ItemUomConversion_ItemId_UomId" ON "ItemUomConversion" ("ItemId", "UomId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005536_V0_2_0_HangHoa') THEN
    CREATE INDEX "IX_ItemUomConversion_UomId" ON "ItemUomConversion" ("UomId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005536_V0_2_0_HangHoa') THEN
    CREATE UNIQUE INDEX "IX_Uom_Code" ON "Uom" ("Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030005536_V0_2_0_HangHoa') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261030005536_V0_2_0_HangHoa', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030011302_V0_2_0_NhaCungCap') THEN
    CREATE TABLE "SupplierGroup" (
        "Id" uuid NOT NULL,
        "Code" character varying(20) NOT NULL,
        "Name" character varying(200) NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_SupplierGroup" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030011302_V0_2_0_NhaCungCap') THEN
    CREATE TABLE "Supplier" (
        "Id" uuid NOT NULL,
        "Code" character varying(30) NOT NULL,
        "Name" character varying(300) NOT NULL,
        "ShortName" character varying(100),
        "GroupId" uuid NOT NULL,
        "IsForeign" boolean NOT NULL,
        "TaxCode" character varying(20),
        "Address" character varying(500),
        "Phone" character varying(50),
        "Email" character varying(256),
        "Website" character varying(200),
        "PaymentTerms" character varying(200),
        "SupplyScope" character varying(500),
        "Rating" integer,
        "Status" integer NOT NULL,
        "StatusNote" character varying(500),
        "InternalCompanyId" uuid,
        "SearchText" character varying(800) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_Supplier" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Supplier_Company_InternalCompanyId" FOREIGN KEY ("InternalCompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Supplier_SupplierGroup_GroupId" FOREIGN KEY ("GroupId") REFERENCES "SupplierGroup" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030011302_V0_2_0_NhaCungCap') THEN
    CREATE TABLE "SupplierBankAccount" (
        "Id" uuid NOT NULL,
        "SupplierId" uuid NOT NULL,
        "BankName" character varying(200) NOT NULL,
        "Branch" character varying(200),
        "AccountNumber" character varying(30) NOT NULL,
        "AccountName" character varying(200),
        "IsDefault" boolean NOT NULL,
        CONSTRAINT "PK_SupplierBankAccount" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SupplierBankAccount_Supplier_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Supplier" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030011302_V0_2_0_NhaCungCap') THEN
    CREATE TABLE "SupplierContact" (
        "Id" uuid NOT NULL,
        "SupplierId" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "Position" character varying(100),
        "Phone" character varying(50),
        "Email" character varying(256),
        "IsPrimary" boolean NOT NULL,
        CONSTRAINT "PK_SupplierContact" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SupplierContact_Supplier_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Supplier" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030011302_V0_2_0_NhaCungCap') THEN
    CREATE UNIQUE INDEX "IX_Supplier_Code" ON "Supplier" ("Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030011302_V0_2_0_NhaCungCap') THEN
    CREATE INDEX "IX_Supplier_GroupId" ON "Supplier" ("GroupId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030011302_V0_2_0_NhaCungCap') THEN
    CREATE UNIQUE INDEX "IX_Supplier_InternalCompanyId" ON "Supplier" ("InternalCompanyId") WHERE "InternalCompanyId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030011302_V0_2_0_NhaCungCap') THEN
    CREATE UNIQUE INDEX "IX_Supplier_TaxCode" ON "Supplier" ("TaxCode") WHERE "TaxCode" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030011302_V0_2_0_NhaCungCap') THEN
    CREATE INDEX "IX_SupplierBankAccount_SupplierId" ON "SupplierBankAccount" ("SupplierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030011302_V0_2_0_NhaCungCap') THEN
    CREATE INDEX "IX_SupplierContact_SupplierId" ON "SupplierContact" ("SupplierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030011302_V0_2_0_NhaCungCap') THEN
    CREATE UNIQUE INDEX "IX_SupplierGroup_Code" ON "SupplierGroup" ("Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030011302_V0_2_0_NhaCungCap') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261030011302_V0_2_0_NhaCungCap', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030013343_V0_2_0_NccCongTySanPham') THEN
    CREATE TABLE "SupplierCompany" (
        "Id" uuid NOT NULL,
        "SupplierId" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "LocalCode" character varying(30),
        "StartDate" date,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_SupplierCompany" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SupplierCompany_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_SupplierCompany_Supplier_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030013343_V0_2_0_NccCongTySanPham') THEN
    CREATE TABLE "SupplierItem" (
        "Id" uuid NOT NULL,
        "SupplierId" uuid NOT NULL,
        "ItemId" uuid NOT NULL,
        "SupplierItemCode" character varying(50),
        "PurchaseUomId" uuid NOT NULL,
        "Moq" numeric(18,4),
        "LeadTimeDays" integer,
        "IsPreferred" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_SupplierItem" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SupplierItem_Item_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Item" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_SupplierItem_Supplier_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_SupplierItem_Uom_PurchaseUomId" FOREIGN KEY ("PurchaseUomId") REFERENCES "Uom" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030013343_V0_2_0_NccCongTySanPham') THEN
    CREATE UNIQUE INDEX "IX_SupplierCompany_CompanyId_LocalCode" ON "SupplierCompany" ("CompanyId", "LocalCode") WHERE "LocalCode" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030013343_V0_2_0_NccCongTySanPham') THEN
    CREATE UNIQUE INDEX "IX_SupplierCompany_SupplierId_CompanyId" ON "SupplierCompany" ("SupplierId", "CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030013343_V0_2_0_NccCongTySanPham') THEN
    CREATE INDEX "IX_SupplierItem_ItemId" ON "SupplierItem" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030013343_V0_2_0_NccCongTySanPham') THEN
    CREATE UNIQUE INDEX "IX_SupplierItem_ItemId_Preferred" ON "SupplierItem" ("ItemId") WHERE "IsPreferred";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030013343_V0_2_0_NccCongTySanPham') THEN
    CREATE INDEX "IX_SupplierItem_PurchaseUomId" ON "SupplierItem" ("PurchaseUomId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030013343_V0_2_0_NccCongTySanPham') THEN
    CREATE UNIQUE INDEX "IX_SupplierItem_SupplierId_ItemId" ON "SupplierItem" ("SupplierId", "ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030013343_V0_2_0_NccCongTySanPham') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261030013343_V0_2_0_NccCongTySanPham', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030021724_V0_3_0_KyKho') THEN
    CREATE TABLE "InventoryPeriod" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "Year" integer NOT NULL,
        "Month" integer NOT NULL,
        "StartDate" date NOT NULL,
        "EndDate" date NOT NULL,
        "Status" integer NOT NULL,
        "ClosedAt" timestamp with time zone,
        "ClosedBy" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_InventoryPeriod" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_InventoryPeriod_Month" CHECK ("Month" BETWEEN 0 AND 12),
        CONSTRAINT "FK_InventoryPeriod_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030021724_V0_3_0_KyKho') THEN
    CREATE UNIQUE INDEX "IX_InventoryPeriod_CompanyId_Year_Month" ON "InventoryPeriod" ("CompanyId", "Year", "Month");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030021724_V0_3_0_KyKho') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261030021724_V0_3_0_KyKho', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030023839_V0_3_0_ChungTuKho') THEN
    CREATE TABLE "InventoryDocument" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "DocType" character varying(10) NOT NULL,
        "No" character varying(50) NOT NULL,
        "Date" date NOT NULL,
        "Status" integer NOT NULL,
        "FromWarehouseId" uuid,
        "ToWarehouseId" uuid,
        "SupplierId" uuid,
        "InvoiceNo" character varying(50),
        "InvoiceSeries" character varying(20),
        "InvoiceDate" date,
        "DeliveryNoteNo" character varying(50),
        "Deliverer" character varying(200),
        "Note" character varying(1000),
        "PeriodId" uuid,
        "PostedAt" timestamp with time zone,
        "PostedBy" uuid,
        "TotalAmount" numeric(19,2) NOT NULL,
        "TotalVat" numeric(19,2) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_InventoryDocument" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_InventoryDocument_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_InventoryDocument_InventoryPeriod_PeriodId" FOREIGN KEY ("PeriodId") REFERENCES "InventoryPeriod" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_InventoryDocument_Supplier_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_InventoryDocument_Warehouse_FromWarehouseId_CompanyId" FOREIGN KEY ("FromWarehouseId", "CompanyId") REFERENCES "Warehouse" ("Id", "CompanyId") ON DELETE RESTRICT,
        CONSTRAINT "FK_InventoryDocument_Warehouse_ToWarehouseId_CompanyId" FOREIGN KEY ("ToWarehouseId", "CompanyId") REFERENCES "Warehouse" ("Id", "CompanyId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030023839_V0_3_0_ChungTuKho') THEN
    CREATE TABLE "InventoryDocumentLine" (
        "Id" uuid NOT NULL,
        "DocumentId" uuid NOT NULL,
        "LineNo" integer NOT NULL,
        "ItemId" uuid NOT NULL,
        "UomId" uuid NOT NULL,
        "Qty" numeric(18,4) NOT NULL,
        "UomFactor" numeric(18,6) NOT NULL,
        "BaseQty" numeric(18,4) NOT NULL,
        "UnitPrice" numeric(19,6) NOT NULL,
        "Amount" numeric(19,2) NOT NULL,
        "VatCode" character varying(5),
        "VatAmount" numeric(19,2) NOT NULL,
        "LotNo" character varying(50),
        "ExpiryDate" date,
        "Note" character varying(500),
        CONSTRAINT "PK_InventoryDocumentLine" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_InventoryDocumentLine_InventoryDocument_DocumentId" FOREIGN KEY ("DocumentId") REFERENCES "InventoryDocument" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_InventoryDocumentLine_Item_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Item" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_InventoryDocumentLine_Uom_UomId" FOREIGN KEY ("UomId") REFERENCES "Uom" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030023839_V0_3_0_ChungTuKho') THEN
    CREATE INDEX "IX_InventoryDocument_CompanyId_DocType_Date" ON "InventoryDocument" ("CompanyId", "DocType", "Date");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030023839_V0_3_0_ChungTuKho') THEN
    CREATE UNIQUE INDEX "IX_InventoryDocument_CompanyId_DocType_No" ON "InventoryDocument" ("CompanyId", "DocType", "No");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030023839_V0_3_0_ChungTuKho') THEN
    CREATE INDEX "IX_InventoryDocument_FromWarehouseId_CompanyId" ON "InventoryDocument" ("FromWarehouseId", "CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030023839_V0_3_0_ChungTuKho') THEN
    CREATE INDEX "IX_InventoryDocument_PeriodId" ON "InventoryDocument" ("PeriodId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030023839_V0_3_0_ChungTuKho') THEN
    CREATE INDEX "IX_InventoryDocument_SupplierId" ON "InventoryDocument" ("SupplierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030023839_V0_3_0_ChungTuKho') THEN
    CREATE INDEX "IX_InventoryDocument_ToWarehouseId_CompanyId" ON "InventoryDocument" ("ToWarehouseId", "CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030023839_V0_3_0_ChungTuKho') THEN
    CREATE INDEX "IX_InventoryDocumentLine_DocumentId_LineNo" ON "InventoryDocumentLine" ("DocumentId", "LineNo");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030023839_V0_3_0_ChungTuKho') THEN
    CREATE INDEX "IX_InventoryDocumentLine_ItemId" ON "InventoryDocumentLine" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030023839_V0_3_0_ChungTuKho') THEN
    CREATE INDEX "IX_InventoryDocumentLine_UomId" ON "InventoryDocumentLine" ("UomId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030023839_V0_3_0_ChungTuKho') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261030023839_V0_3_0_ChungTuKho', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030055444_V0_3_0_SoKho') THEN
    CREATE TABLE "StockLedger" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "WarehouseId" uuid NOT NULL,
        "ItemId" uuid NOT NULL,
        "LotNo" character varying(50),
        "PeriodId" uuid NOT NULL,
        "PostingDate" date NOT NULL,
        "DocumentId" uuid NOT NULL,
        "DocumentLineId" uuid NOT NULL,
        "DocType" character varying(10) NOT NULL,
        "Direction" integer NOT NULL,
        "BaseQty" numeric(18,4) NOT NULL,
        "UnitCost" numeric(19,6) NOT NULL,
        "Amount" numeric(19,2) NOT NULL,
        "IsCostFinal" boolean NOT NULL,
        CONSTRAINT "PK_StockLedger" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_StockLedger_BaseQty" CHECK ("BaseQty" > 0),
        CONSTRAINT "CK_StockLedger_Direction" CHECK ("Direction" IN (-1, 1)),
        CONSTRAINT "FK_StockLedger_InventoryDocumentLine_DocumentLineId" FOREIGN KEY ("DocumentLineId") REFERENCES "InventoryDocumentLine" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_StockLedger_InventoryDocument_DocumentId" FOREIGN KEY ("DocumentId") REFERENCES "InventoryDocument" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_StockLedger_InventoryPeriod_PeriodId" FOREIGN KEY ("PeriodId") REFERENCES "InventoryPeriod" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_StockLedger_Item_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Item" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_StockLedger_Warehouse_WarehouseId_CompanyId" FOREIGN KEY ("WarehouseId", "CompanyId") REFERENCES "Warehouse" ("Id", "CompanyId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030055444_V0_3_0_SoKho') THEN
    CREATE TABLE "StockOnHand" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "WarehouseId" uuid NOT NULL,
        "ItemId" uuid NOT NULL,
        "LotNo" character varying(50),
        "Qty" numeric(18,4) NOT NULL,
        "ReservedQty" numeric(18,4) NOT NULL,
        CONSTRAINT "PK_StockOnHand" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_StockOnHand_Item_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Item" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_StockOnHand_Warehouse_WarehouseId_CompanyId" FOREIGN KEY ("WarehouseId", "CompanyId") REFERENCES "Warehouse" ("Id", "CompanyId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030055444_V0_3_0_SoKho') THEN
    CREATE INDEX "IX_StockLedger_CompanyId_PeriodId_ItemId" ON "StockLedger" ("CompanyId", "PeriodId", "ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030055444_V0_3_0_SoKho') THEN
    CREATE INDEX "IX_StockLedger_CompanyId_WarehouseId_ItemId_PostingDate" ON "StockLedger" ("CompanyId", "WarehouseId", "ItemId", "PostingDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030055444_V0_3_0_SoKho') THEN
    CREATE INDEX "IX_StockLedger_DocumentId" ON "StockLedger" ("DocumentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030055444_V0_3_0_SoKho') THEN
    CREATE INDEX "IX_StockLedger_DocumentLineId" ON "StockLedger" ("DocumentLineId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030055444_V0_3_0_SoKho') THEN
    CREATE INDEX "IX_StockLedger_ItemId" ON "StockLedger" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030055444_V0_3_0_SoKho') THEN
    CREATE INDEX "IX_StockLedger_PeriodId" ON "StockLedger" ("PeriodId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030055444_V0_3_0_SoKho') THEN
    CREATE INDEX "IX_StockLedger_WarehouseId_CompanyId" ON "StockLedger" ("WarehouseId", "CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030055444_V0_3_0_SoKho') THEN
    CREATE UNIQUE INDEX "IX_StockOnHand_CompanyId_WarehouseId_ItemId_LotNo" ON "StockOnHand" ("CompanyId", "WarehouseId", "ItemId", "LotNo") NULLS NOT DISTINCT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030055444_V0_3_0_SoKho') THEN
    CREATE INDEX "IX_StockOnHand_ItemId" ON "StockOnHand" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030055444_V0_3_0_SoKho') THEN
    CREATE INDEX "IX_StockOnHand_WarehouseId_CompanyId" ON "StockOnHand" ("WarehouseId", "CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030055444_V0_3_0_SoKho') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261030055444_V0_3_0_SoKho', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030060926_V0_3_0_TraNccChuyenKho') THEN
    ALTER TABLE "InventoryDocumentLine" ADD "SourceLineId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030060926_V0_3_0_TraNccChuyenKho') THEN
    ALTER TABLE "InventoryDocument" ADD "LinkedDocumentId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030060926_V0_3_0_TraNccChuyenKho') THEN
    ALTER TABLE "InventoryDocument" ADD "Receiver" character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030060926_V0_3_0_TraNccChuyenKho') THEN
    CREATE INDEX "IX_InventoryDocumentLine_SourceLineId" ON "InventoryDocumentLine" ("SourceLineId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030060926_V0_3_0_TraNccChuyenKho') THEN
    CREATE INDEX "IX_InventoryDocument_LinkedDocumentId" ON "InventoryDocument" ("LinkedDocumentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030060926_V0_3_0_TraNccChuyenKho') THEN
    ALTER TABLE "InventoryDocument" ADD CONSTRAINT "FK_InventoryDocument_InventoryDocument_LinkedDocumentId" FOREIGN KEY ("LinkedDocumentId") REFERENCES "InventoryDocument" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030060926_V0_3_0_TraNccChuyenKho') THEN
    ALTER TABLE "InventoryDocumentLine" ADD CONSTRAINT "FK_InventoryDocumentLine_InventoryDocumentLine_SourceLineId" FOREIGN KEY ("SourceLineId") REFERENCES "InventoryDocumentLine" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030060926_V0_3_0_TraNccChuyenKho') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261030060926_V0_3_0_TraNccChuyenKho', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030065121_V0_4_0_DongKyGiaVon') THEN
    ALTER TABLE "InventoryPeriod" ADD "ReopenReason" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030065121_V0_4_0_DongKyGiaVon') THEN
    CREATE TABLE "ItemPeriodCost" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "PeriodId" uuid NOT NULL,
        "ItemId" uuid NOT NULL,
        "UnitCost" numeric(19,6) NOT NULL,
        "CalculatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_ItemPeriodCost" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ItemPeriodCost_InventoryPeriod_PeriodId" FOREIGN KEY ("PeriodId") REFERENCES "InventoryPeriod" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ItemPeriodCost_Item_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Item" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030065121_V0_4_0_DongKyGiaVon') THEN
    CREATE TABLE "StockPeriodBalance" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "PeriodId" uuid NOT NULL,
        "WarehouseId" uuid NOT NULL,
        "ItemId" uuid NOT NULL,
        "LotNo" character varying(50),
        "OpenQty" numeric(18,4) NOT NULL,
        "OpenAmount" numeric(19,2) NOT NULL,
        "InQty" numeric(18,4) NOT NULL,
        "InAmount" numeric(19,2) NOT NULL,
        "OutQty" numeric(18,4) NOT NULL,
        "OutAmount" numeric(19,2) NOT NULL,
        "CloseQty" numeric(18,4) NOT NULL,
        "CloseAmount" numeric(19,2) NOT NULL,
        CONSTRAINT "PK_StockPeriodBalance" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_StockPeriodBalance_InventoryPeriod_PeriodId" FOREIGN KEY ("PeriodId") REFERENCES "InventoryPeriod" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_StockPeriodBalance_Item_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Item" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_StockPeriodBalance_Warehouse_WarehouseId_CompanyId" FOREIGN KEY ("WarehouseId", "CompanyId") REFERENCES "Warehouse" ("Id", "CompanyId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030065121_V0_4_0_DongKyGiaVon') THEN
    CREATE INDEX "IX_ItemPeriodCost_ItemId" ON "ItemPeriodCost" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030065121_V0_4_0_DongKyGiaVon') THEN
    CREATE UNIQUE INDEX "IX_ItemPeriodCost_PeriodId_ItemId" ON "ItemPeriodCost" ("PeriodId", "ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030065121_V0_4_0_DongKyGiaVon') THEN
    CREATE INDEX "IX_StockPeriodBalance_CompanyId_PeriodId" ON "StockPeriodBalance" ("CompanyId", "PeriodId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030065121_V0_4_0_DongKyGiaVon') THEN
    CREATE INDEX "IX_StockPeriodBalance_ItemId" ON "StockPeriodBalance" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030065121_V0_4_0_DongKyGiaVon') THEN
    CREATE UNIQUE INDEX "IX_StockPeriodBalance_PeriodId_WarehouseId_ItemId_LotNo" ON "StockPeriodBalance" ("PeriodId", "WarehouseId", "ItemId", "LotNo") NULLS NOT DISTINCT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030065121_V0_4_0_DongKyGiaVon') THEN
    CREATE INDEX "IX_StockPeriodBalance_WarehouseId_CompanyId" ON "StockPeriodBalance" ("WarehouseId", "CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030065121_V0_4_0_DongKyGiaVon') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261030065121_V0_4_0_DongKyGiaVon', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030071327_V0_5_0_MuaBanNoiBo') THEN
    ALTER TABLE "InventoryDocument" ADD "CounterCompanyId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030071327_V0_5_0_MuaBanNoiBo') THEN
    CREATE TABLE "InternalPrice" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "BuyerCompanyId" uuid,
        "ItemId" uuid NOT NULL,
        "UnitPrice" numeric(19,6) NOT NULL,
        "VatCode" character varying(5),
        "ValidFrom" date NOT NULL,
        "ValidTo" date,
        "Note" character varying(500),
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_InternalPrice" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_InternalPrice_Buyer" CHECK ("BuyerCompanyId" IS NULL OR "BuyerCompanyId" <> "CompanyId"),
        CONSTRAINT "FK_InternalPrice_Company_BuyerCompanyId" FOREIGN KEY ("BuyerCompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_InternalPrice_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_InternalPrice_Item_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Item" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030071327_V0_5_0_MuaBanNoiBo') THEN
    CREATE INDEX "IX_InventoryDocument_CounterCompanyId" ON "InventoryDocument" ("CounterCompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030071327_V0_5_0_MuaBanNoiBo') THEN
    CREATE INDEX "IX_InternalPrice_BuyerCompanyId" ON "InternalPrice" ("BuyerCompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030071327_V0_5_0_MuaBanNoiBo') THEN
    CREATE INDEX "IX_InternalPrice_CompanyId_ItemId_BuyerCompanyId_ValidFrom" ON "InternalPrice" ("CompanyId", "ItemId", "BuyerCompanyId", "ValidFrom");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030071327_V0_5_0_MuaBanNoiBo') THEN
    CREATE INDEX "IX_InternalPrice_ItemId" ON "InternalPrice" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030071327_V0_5_0_MuaBanNoiBo') THEN
    ALTER TABLE "InventoryDocument" ADD CONSTRAINT "FK_InventoryDocument_Company_CounterCompanyId" FOREIGN KEY ("CounterCompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261030071327_V0_5_0_MuaBanNoiBo') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261030071327_V0_5_0_MuaBanNoiBo', '10.0.14');
    END IF;
END $EF$;
COMMIT;

