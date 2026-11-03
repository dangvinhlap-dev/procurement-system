START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102154648_V0_15_0_PhieuDieuChinhKho') THEN
    ALTER TABLE "InventoryDocumentLine" ADD "Direction" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102154648_V0_15_0_PhieuDieuChinhKho') THEN
    ALTER TABLE "InventoryDocument" ADD "CountReportNo" character varying(50);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102154648_V0_15_0_PhieuDieuChinhKho') THEN
    ALTER TABLE "InventoryDocument" ADD "Reason" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102154648_V0_15_0_PhieuDieuChinhKho') THEN
    ALTER TABLE "InventoryDocumentLine" ADD CONSTRAINT "CK_InventoryDocumentLine_Direction" CHECK ("Direction" IS NULL OR "Direction" IN (-1, 1));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102154648_V0_15_0_PhieuDieuChinhKho') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261102154648_V0_15_0_PhieuDieuChinhKho', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103002518_V0_15_0_SoLieuKiemKe') THEN
    ALTER TABLE "InventoryDocumentLine" ADD "BookQty" numeric(18,4);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103002518_V0_15_0_SoLieuKiemKe') THEN
    ALTER TABLE "InventoryDocumentLine" ADD "CountedQty" numeric(18,4);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103002518_V0_15_0_SoLieuKiemKe') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261103002518_V0_15_0_SoLieuKiemKe', '10.0.14');
    END IF;
END $EF$;
COMMIT;

