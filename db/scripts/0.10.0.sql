START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101034938_V0_10_0_MarkupNoiBo') THEN
    ALTER TABLE "CompanyParameter" ADD "InternalMarkupPercent" numeric(9,4) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101034938_V0_10_0_MarkupNoiBo') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261101034938_V0_10_0_MarkupNoiBo', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101043937_V0_10_0_DuLieuMuaLichSu') THEN
    CREATE TABLE "PurchaseHistoryBatch" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "FileName" character varying(255) NOT NULL,
        "RecordCount" integer NOT NULL,
        "FromDate" date NOT NULL,
        "ToDate" date NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_PurchaseHistoryBatch" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PurchaseHistoryBatch_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101043937_V0_10_0_DuLieuMuaLichSu') THEN
    CREATE TABLE "PurchaseHistoryRecord" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "BatchId" uuid NOT NULL,
        "RowNumber" integer NOT NULL,
        "Department" character varying(200),
        "ItemCode" character varying(50) NOT NULL,
        "ItemName" character varying(300) NOT NULL,
        "Specification" character varying(500),
        "Brand" character varying(100),
        "Origin" character varying(100),
        "Standard" character varying(200),
        "Uom" character varying(50) NOT NULL,
        "RequestDate" date,
        "PurchaseDate" date NOT NULL,
        "Quantity" numeric(18,4) NOT NULL,
        "UnitPrice" numeric(19,6) NOT NULL,
        "VatPercent" numeric(5,2),
        "Currency" character varying(3) NOT NULL,
        "Amount" numeric(19,2) NOT NULL,
        "RelatedCost" numeric(19,2),
        "RelatedCostNote" character varying(500),
        "SupplierName" character varying(300) NOT NULL,
        "SupplierTaxCode" character varying(20),
        "SupplierContact" character varying(300),
        "PaymentTerms" character varying(300),
        "Discount" character varying(200),
        "Sponsorship" character varying(300),
        "OnTime" boolean,
        "QualityPassed" boolean,
        "Complaint" character varying(500),
        "ItemId" uuid,
        "SupplierId" uuid,
        "SearchText" character varying(1000) NOT NULL,
        CONSTRAINT "PK_PurchaseHistoryRecord" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PurchaseHistoryRecord_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseHistoryRecord_Item_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Item" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseHistoryRecord_PurchaseHistoryBatch_BatchId" FOREIGN KEY ("BatchId") REFERENCES "PurchaseHistoryBatch" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_PurchaseHistoryRecord_Supplier_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101043937_V0_10_0_DuLieuMuaLichSu') THEN
    CREATE INDEX "IX_PurchaseHistoryBatch_CompanyId_CreatedAt" ON "PurchaseHistoryBatch" ("CompanyId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101043937_V0_10_0_DuLieuMuaLichSu') THEN
    CREATE INDEX "IX_PurchaseHistoryRecord_BatchId" ON "PurchaseHistoryRecord" ("BatchId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101043937_V0_10_0_DuLieuMuaLichSu') THEN
    CREATE INDEX "IX_PurchaseHistoryRecord_CompanyId_PurchaseDate" ON "PurchaseHistoryRecord" ("CompanyId", "PurchaseDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101043937_V0_10_0_DuLieuMuaLichSu') THEN
    CREATE INDEX "IX_PurchaseHistoryRecord_ItemId" ON "PurchaseHistoryRecord" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101043937_V0_10_0_DuLieuMuaLichSu') THEN
    CREATE INDEX "IX_PurchaseHistoryRecord_SupplierId" ON "PurchaseHistoryRecord" ("SupplierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101043937_V0_10_0_DuLieuMuaLichSu') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261101043937_V0_10_0_DuLieuMuaLichSu', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101060936_V0_10_0_AnhXaDuLieuMua') THEN
    CREATE TABLE "PurchaseHistoryItemMapping" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "SourceCode" character varying(50) NOT NULL,
        "ItemId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_PurchaseHistoryItemMapping" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PurchaseHistoryItemMapping_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseHistoryItemMapping_Item_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Item" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101060936_V0_10_0_AnhXaDuLieuMua') THEN
    CREATE TABLE "PurchaseHistorySupplierMapping" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "SourceKey" character varying(310) NOT NULL,
        "SupplierId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_PurchaseHistorySupplierMapping" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PurchaseHistorySupplierMapping_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseHistorySupplierMapping_Supplier_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101060936_V0_10_0_AnhXaDuLieuMua') THEN
    CREATE INDEX "IX_PurchaseHistoryRecord_CompanyId_ItemCode" ON "PurchaseHistoryRecord" ("CompanyId", "ItemCode");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101060936_V0_10_0_AnhXaDuLieuMua') THEN
    CREATE INDEX "IX_PurchaseHistoryRecord_CompanyId_SupplierTaxCode" ON "PurchaseHistoryRecord" ("CompanyId", "SupplierTaxCode");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101060936_V0_10_0_AnhXaDuLieuMua') THEN
    CREATE UNIQUE INDEX "IX_PurchaseHistoryItemMapping_CompanyId_SourceCode" ON "PurchaseHistoryItemMapping" ("CompanyId", "SourceCode");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101060936_V0_10_0_AnhXaDuLieuMua') THEN
    CREATE INDEX "IX_PurchaseHistoryItemMapping_ItemId" ON "PurchaseHistoryItemMapping" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101060936_V0_10_0_AnhXaDuLieuMua') THEN
    CREATE UNIQUE INDEX "IX_PurchaseHistorySupplierMapping_CompanyId_SourceKey" ON "PurchaseHistorySupplierMapping" ("CompanyId", "SourceKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101060936_V0_10_0_AnhXaDuLieuMua') THEN
    CREATE INDEX "IX_PurchaseHistorySupplierMapping_SupplierId" ON "PurchaseHistorySupplierMapping" ("SupplierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101060936_V0_10_0_AnhXaDuLieuMua') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261101060936_V0_10_0_AnhXaDuLieuMua', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101070150_V0_10_0_DinhKemFile') THEN
    CREATE TABLE "Attachment" (
        "Id" uuid NOT NULL,
        "OwnerType" character varying(30) NOT NULL,
        "OwnerId" uuid NOT NULL,
        "CompanyId" uuid,
        "FileName" character varying(255) NOT NULL,
        "Extension" character varying(10) NOT NULL,
        "ContentType" character varying(100) NOT NULL,
        "SizeBytes" bigint NOT NULL,
        "Sha256" character varying(64) NOT NULL,
        "StoragePath" character varying(100) NOT NULL,
        "Note" character varying(500),
        "IsDeleted" boolean NOT NULL,
        "DeletedAt" timestamp with time zone,
        "DeletedBy" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_Attachment" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Attachment_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101070150_V0_10_0_DinhKemFile') THEN
    CREATE INDEX "IX_Attachment_CompanyId" ON "Attachment" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101070150_V0_10_0_DinhKemFile') THEN
    CREATE INDEX "IX_Attachment_OwnerType_OwnerId" ON "Attachment" ("OwnerType", "OwnerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101070150_V0_10_0_DinhKemFile') THEN
    CREATE UNIQUE INDEX "IX_Attachment_StoragePath" ON "Attachment" ("StoragePath");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101070150_V0_10_0_DinhKemFile') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261101070150_V0_10_0_DinhKemFile', '10.0.14');
    END IF;
END $EF$;
COMMIT;

