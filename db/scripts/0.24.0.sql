START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112030122_V0_24_0_RfqBieuMau3') THEN
    ALTER TABLE "Rfq" ADD "Category" integer NOT NULL DEFAULT 2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112030122_V0_24_0_RfqBieuMau3') THEN
    ALTER TABLE "Rfq" ADD "ConstructionProjectId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112030122_V0_24_0_RfqBieuMau3') THEN
    ALTER TABLE "Rfq" ADD "Form" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112030122_V0_24_0_RfqBieuMau3') THEN
    ALTER TABLE "Rfq" ADD "SelectionReason" character varying(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112030122_V0_24_0_RfqBieuMau3') THEN
    ALTER TABLE "CompanyParameter" ADD "RfqComparisonThreshold" numeric(19,2) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112030122_V0_24_0_RfqBieuMau3') THEN
    CREATE INDEX "IX_Rfq_ConstructionProjectId_CompanyId" ON "Rfq" ("ConstructionProjectId", "CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112030122_V0_24_0_RfqBieuMau3') THEN
    ALTER TABLE "Rfq" ADD CONSTRAINT "FK_Rfq_ConstructionProject_ConstructionProjectId_CompanyId" FOREIGN KEY ("ConstructionProjectId", "CompanyId") REFERENCES "ConstructionProject" ("Id", "CompanyId") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112030122_V0_24_0_RfqBieuMau3') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261112030122_V0_24_0_RfqBieuMau3', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112033352_V0_24_0_PoBieuMau4') THEN
    ALTER TABLE "PurchaseOrder" ADD "Category" integer NOT NULL DEFAULT 2;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112033352_V0_24_0_PoBieuMau4') THEN
    ALTER TABLE "PurchaseOrder" ADD "ConstructionProjectId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112033352_V0_24_0_PoBieuMau4') THEN
    CREATE INDEX "IX_PurchaseOrder_ConstructionProjectId_CompanyId" ON "PurchaseOrder" ("ConstructionProjectId", "CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112033352_V0_24_0_PoBieuMau4') THEN
    ALTER TABLE "PurchaseOrder" ADD CONSTRAINT "FK_PurchaseOrder_ConstructionProject_ConstructionProjectId_Com~" FOREIGN KEY ("ConstructionProjectId", "CompanyId") REFERENCES "ConstructionProject" ("Id", "CompanyId") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112033352_V0_24_0_PoBieuMau4') THEN
    UPDATE "PurchaseOrder" o
    SET "Category" = 1, "ConstructionProjectId" = x."ProjectId"
    FROM (
        SELECT ol."OrderId", (MIN(r."ConstructionProjectId"::text))::uuid AS "ProjectId"
        FROM "PurchaseOrderLine" ol
        JOIN "PurchaseRequestLine" l ON l."Id" = ol."SourcePrLineId"
        JOIN "PurchaseRequest" r ON r."Id" = l."RequestId"
        WHERE r."Category" = 1 AND r."ConstructionProjectId" IS NOT NULL
        GROUP BY ol."OrderId"
    ) x
    WHERE o."Id" = x."OrderId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112033352_V0_24_0_PoBieuMau4') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261112033352_V0_24_0_PoBieuMau4', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112040631_V0_24_0_DonBanNoiBo') THEN
    ALTER TABLE "PurchaseOrder" ADD "InternalSellerCompanyId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112040631_V0_24_0_DonBanNoiBo') THEN
    CREATE TABLE "InternalSaleConfirmation" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "PurchaseOrderId" uuid NOT NULL,
        "OrderNo" character varying(50) NOT NULL,
        "BuyerCompanyId" uuid NOT NULL,
        "Decision" integer NOT NULL,
        "Note" character varying(500),
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_InternalSaleConfirmation" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_InternalSaleConfirmation_Company_BuyerCompanyId" FOREIGN KEY ("BuyerCompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_InternalSaleConfirmation_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_InternalSaleConfirmation_PurchaseOrder_PurchaseOrderId" FOREIGN KEY ("PurchaseOrderId") REFERENCES "PurchaseOrder" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112040631_V0_24_0_DonBanNoiBo') THEN
    CREATE INDEX "IX_PurchaseOrder_InternalSellerCompanyId" ON "PurchaseOrder" ("InternalSellerCompanyId") WHERE "InternalSellerCompanyId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112040631_V0_24_0_DonBanNoiBo') THEN
    CREATE INDEX "IX_InternalSaleConfirmation_BuyerCompanyId" ON "InternalSaleConfirmation" ("BuyerCompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112040631_V0_24_0_DonBanNoiBo') THEN
    CREATE INDEX "IX_InternalSaleConfirmation_CompanyId" ON "InternalSaleConfirmation" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112040631_V0_24_0_DonBanNoiBo') THEN
    CREATE UNIQUE INDEX "IX_InternalSaleConfirmation_PurchaseOrderId" ON "InternalSaleConfirmation" ("PurchaseOrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112040631_V0_24_0_DonBanNoiBo') THEN
    ALTER TABLE "PurchaseOrder" ADD CONSTRAINT "FK_PurchaseOrder_Company_InternalSellerCompanyId" FOREIGN KEY ("InternalSellerCompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112040631_V0_24_0_DonBanNoiBo') THEN
    UPDATE "PurchaseOrder" AS o
    SET "InternalSellerCompanyId" = s."InternalCompanyId"
    FROM "Supplier" AS s
    WHERE s."Id" = o."SupplierId" AND s."InternalCompanyId" IS NOT NULL AND s."InternalCompanyId" <> o."CompanyId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112040631_V0_24_0_DonBanNoiBo') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261112040631_V0_24_0_DonBanNoiBo', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112060953_V0_25_0_TaiKhoanPortalNcc') THEN
    CREATE TABLE "SupplierPortalUser" (
        "Id" uuid NOT NULL,
        "SupplierId" uuid NOT NULL,
        "Email" character varying(256) NOT NULL,
        "FullName" character varying(200) NOT NULL,
        "Phone" character varying(50),
        "Position" character varying(100),
        "IsSupplierAdmin" boolean NOT NULL,
        "Status" integer NOT NULL,
        "PasswordHash" character varying(500),
        "ActivatedAt" timestamp with time zone,
        "LastLoginAt" timestamp with time zone,
        "FailedLoginCount" integer NOT NULL,
        "LockoutEnd" timestamp with time zone,
        "SecurityStamp" character varying(64) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_SupplierPortalUser" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SupplierPortalUser_Supplier_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112060953_V0_25_0_TaiKhoanPortalNcc') THEN
    CREATE TABLE "SupplierPortalCode" (
        "Id" uuid NOT NULL,
        "PortalUserId" uuid NOT NULL,
        "Purpose" integer NOT NULL,
        "CodeHash" character varying(64) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "UsedAt" timestamp with time zone,
        "FailedAttempts" integer NOT NULL,
        CONSTRAINT "PK_SupplierPortalCode" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SupplierPortalCode_SupplierPortalUser_PortalUserId" FOREIGN KEY ("PortalUserId") REFERENCES "SupplierPortalUser" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112060953_V0_25_0_TaiKhoanPortalNcc') THEN
    CREATE INDEX "IX_SupplierPortalCode_CodeHash" ON "SupplierPortalCode" ("CodeHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112060953_V0_25_0_TaiKhoanPortalNcc') THEN
    CREATE INDEX "IX_SupplierPortalCode_PortalUserId_Purpose" ON "SupplierPortalCode" ("PortalUserId", "Purpose");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112060953_V0_25_0_TaiKhoanPortalNcc') THEN
    CREATE UNIQUE INDEX "IX_SupplierPortalUser_Email" ON "SupplierPortalUser" ("Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112060953_V0_25_0_TaiKhoanPortalNcc') THEN
    CREATE INDEX "IX_SupplierPortalUser_SupplierId" ON "SupplierPortalUser" ("SupplierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112060953_V0_25_0_TaiKhoanPortalNcc') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261112060953_V0_25_0_TaiKhoanPortalNcc', '10.0.14');
    END IF;
END $EF$;
COMMIT;

