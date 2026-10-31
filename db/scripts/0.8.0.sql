START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031032437_V0_8_0_XepLoaiNcc') THEN
    CREATE TABLE "SupplierRatingRule" (
        "Id" uuid NOT NULL,
        "Code" character varying(10) NOT NULL,
        "Name" character varying(50) NOT NULL,
        "SortOrder" integer NOT NULL,
        "MinOnTimePct" numeric(5,2) NOT NULL,
        "MaxReturnPct" numeric(5,2) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_SupplierRatingRule" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031032437_V0_8_0_XepLoaiNcc') THEN
    CREATE UNIQUE INDEX "IX_SupplierRatingRule_Code" ON "SupplierRatingRule" ("Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031032437_V0_8_0_XepLoaiNcc') THEN
    CREATE UNIQUE INDEX "IX_SupplierRatingRule_SortOrder" ON "SupplierRatingRule" ("SortOrder");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261031032437_V0_8_0_XepLoaiNcc') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261031032437_V0_8_0_XepLoaiNcc', '10.0.14');
    END IF;
END $EF$;
COMMIT;

