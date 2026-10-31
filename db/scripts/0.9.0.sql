START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031090237_V0_9_0_DanhSoDanhMuc') THEN
    CREATE TABLE "CatalogNumbering" (
        "Id" uuid NOT NULL,
        "Catalog" character varying(30) NOT NULL,
        "CategoryId" uuid,
        "Prefix" character varying(20) NOT NULL,
        "Digits" integer NOT NULL,
        "FromNumber" bigint NOT NULL,
        "ToNumber" bigint NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_CatalogNumbering" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CatalogNumbering_ItemCategory_CategoryId" FOREIGN KEY ("CategoryId") REFERENCES "ItemCategory" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031090237_V0_9_0_DanhSoDanhMuc') THEN
    CREATE UNIQUE INDEX "IX_CatalogNumbering_Catalog_CategoryId" ON "CatalogNumbering" ("Catalog", "CategoryId") NULLS NOT DISTINCT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031090237_V0_9_0_DanhSoDanhMuc') THEN
    CREATE INDEX "IX_CatalogNumbering_CategoryId" ON "CatalogNumbering" ("CategoryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031090237_V0_9_0_DanhSoDanhMuc') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261031090237_V0_9_0_DanhSoDanhMuc', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031135641_V0_9_0_DaiMaMacDinh') THEN
    INSERT INTO "CatalogNumbering" ("Id", "Catalog", "CategoryId", "Prefix", "Digits", "FromNumber", "ToNumber", "CreatedAt")
    SELECT v.id, v.catalog, NULL, v.prefix, v.digits, 1, v.to_number, now()
    FROM (VALUES
        ('0199a000-0000-7000-8000-000000000001'::uuid, 'supplier', 'NCC', 6, 999999::bigint),
        ('0199a000-0000-7000-8000-000000000002'::uuid, 'supplier-group', 'GN', 4, 9999::bigint),
        ('0199a000-0000-7000-8000-000000000003'::uuid, 'item-category', 'NH', 6, 999999::bigint),
        ('0199a000-0000-7000-8000-000000000004'::uuid, 'item', '{NHOM}-', 5, 99999::bigint)
    ) AS v(id, catalog, prefix, digits, to_number)
    WHERE NOT EXISTS (SELECT 1 FROM "CatalogNumbering" c WHERE c."Catalog" = v.catalog AND c."CategoryId" IS NULL)
      AND NOT EXISTS (SELECT 1 FROM "CatalogNumbering" c WHERE c."Id" = v.id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031135641_V0_9_0_DaiMaMacDinh') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261031135641_V0_9_0_DaiMaMacDinh', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031142912_V0_9_0_ThongBao') THEN
    CREATE TABLE "Notification" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "Type" character varying(30) NOT NULL,
        "Title" character varying(200) NOT NULL,
        "Message" character varying(1000) NOT NULL,
        "ScreenCode" character varying(20) NOT NULL,
        "TargetId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "ReadAt" timestamp with time zone,
        CONSTRAINT "PK_Notification" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Notification_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Notification_User_UserId" FOREIGN KEY ("UserId") REFERENCES "User" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031142912_V0_9_0_ThongBao') THEN
    CREATE INDEX "IX_Notification_CompanyId" ON "Notification" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031142912_V0_9_0_ThongBao') THEN
    CREATE INDEX "IX_Notification_UserId_CompanyId_CreatedAt" ON "Notification" ("UserId", "CompanyId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031142912_V0_9_0_ThongBao') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261031142912_V0_9_0_ThongBao', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031145516_V0_9_0_ThamSoCongTy') THEN
    CREATE TABLE "CompanyParameter" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "RfqMinSuppliers" integer NOT NULL DEFAULT 3,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_CompanyParameter" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CompanyParameter_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031145516_V0_9_0_ThamSoCongTy') THEN
    CREATE UNIQUE INDEX "IX_CompanyParameter_CompanyId" ON "CompanyParameter" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031145516_V0_9_0_ThamSoCongTy') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261031145516_V0_9_0_ThamSoCongTy', '10.0.14');
    END IF;
END $EF$;
COMMIT;

