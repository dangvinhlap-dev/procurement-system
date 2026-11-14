START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114003546_V0_28_0_DeNghiCapNhatHoSoNcc') THEN
    CREATE TABLE "SupplierProfileRequest" (
        "Id" uuid NOT NULL,
        "No" character varying(50) NOT NULL,
        "SupplierId" uuid NOT NULL,
        "Status" integer NOT NULL,
        "FieldChangesJson" jsonb NOT NULL,
        "ContactsJson" jsonb,
        "BankAccountsJson" jsonb,
        "Note" character varying(1000),
        "SubmittedAt" timestamp with time zone NOT NULL,
        "PortalUserId" uuid NOT NULL,
        "SubmittedByEmail" character varying(256) NOT NULL,
        "ReviewedBy" uuid,
        "ClosedAt" timestamp with time zone,
        "ReviewComment" character varying(1000),
        "BankChangeRequestNo" character varying(50),
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_SupplierProfileRequest" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SupplierProfileRequest_SupplierPortalUser_PortalUserId" FOREIGN KEY ("PortalUserId") REFERENCES "SupplierPortalUser" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_SupplierProfileRequest_Supplier_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114003546_V0_28_0_DeNghiCapNhatHoSoNcc') THEN
    CREATE UNIQUE INDEX "IX_SupplierProfileRequest_No" ON "SupplierProfileRequest" ("No");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114003546_V0_28_0_DeNghiCapNhatHoSoNcc') THEN
    CREATE INDEX "IX_SupplierProfileRequest_PortalUserId" ON "SupplierProfileRequest" ("PortalUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114003546_V0_28_0_DeNghiCapNhatHoSoNcc') THEN
    CREATE UNIQUE INDEX "IX_SupplierProfileRequest_SupplierId_Pending" ON "SupplierProfileRequest" ("SupplierId") WHERE "Status" = 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114003546_V0_28_0_DeNghiCapNhatHoSoNcc') THEN
    CREATE INDEX "IX_SupplierProfileRequest_SupplierId_Status" ON "SupplierProfileRequest" ("SupplierId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114003546_V0_28_0_DeNghiCapNhatHoSoNcc') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261114003546_V0_28_0_DeNghiCapNhatHoSoNcc', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114025817_V0_28_0_NhomHangToiDa4Cap') THEN
    ALTER TABLE "ItemCategory" ADD "Level" integer NOT NULL DEFAULT 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114025817_V0_28_0_NhomHangToiDa4Cap') THEN
    WITH RECURSIVE tree AS (
        SELECT "Id", 1 AS lvl FROM "ItemCategory" WHERE "ParentId" IS NULL
        UNION ALL
        SELECT c."Id", t.lvl + 1 FROM "ItemCategory" c JOIN tree t ON c."ParentId" = t."Id"
    )
    UPDATE "ItemCategory" c SET "Level" = t.lvl FROM tree t WHERE c."Id" = t."Id";

    DO $$
    DECLARE deep text;
    BEGIN
        SELECT string_agg("Code", ', ') INTO deep FROM "ItemCategory" WHERE "Level" > 4;
        IF deep IS NOT NULL THEN
            RAISE EXCEPTION 'Nhóm hàng tối đa 4 cấp: các nhóm % đang ở cấp 5 trở xuống – chuyển các nhóm này lên trên rồi chạy lại.', deep;
        END IF;
    END $$;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114025817_V0_28_0_NhomHangToiDa4Cap') THEN
    ALTER TABLE "ItemCategory" ADD CONSTRAINT "CK_ItemCategory_Level" CHECK ("Level" BETWEEN 1 AND 4);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261114025817_V0_28_0_NhomHangToiDa4Cap') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261114025817_V0_28_0_NhomHangToiDa4Cap', '10.0.14');
    END IF;
END $EF$;
COMMIT;

