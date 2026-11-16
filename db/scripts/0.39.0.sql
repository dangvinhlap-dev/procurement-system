START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116070648_V0_39_0_BoCotKho') THEN
    ALTER TABLE "Warehouse" DROP CONSTRAINT "FK_Warehouse_Warehouse_ParentWarehouseId_CompanyId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116070648_V0_39_0_BoCotKho') THEN
    DROP INDEX "IX_Warehouse_CompanyId_Main";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116070648_V0_39_0_BoCotKho') THEN
    DROP INDEX "IX_Warehouse_ParentWarehouseId_CompanyId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116070648_V0_39_0_BoCotKho') THEN
    ALTER TABLE "Warehouse" DROP COLUMN "AllowNegativeStock";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116070648_V0_39_0_BoCotKho') THEN
    ALTER TABLE "Warehouse" DROP COLUMN "ParentWarehouseId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116070648_V0_39_0_BoCotKho') THEN
    ALTER TABLE "Warehouse" DROP COLUMN "Type";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116070648_V0_39_0_BoCotKho') THEN
    ALTER TABLE "Item" DROP COLUMN "IsCritical";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116070648_V0_39_0_BoCotKho') THEN
    ALTER TABLE "Item" DROP COLUMN "MaxStock";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116070648_V0_39_0_BoCotKho') THEN
    ALTER TABLE "Item" DROP COLUMN "MinStock";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116070648_V0_39_0_BoCotKho') THEN
    ALTER TABLE "Item" DROP COLUMN "ReorderPoint";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116070648_V0_39_0_BoCotKho') THEN
    ALTER TABLE "Item" DROP COLUMN "SafetyStock";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116070648_V0_39_0_BoCotKho') THEN
    ALTER TABLE "Item" DROP COLUMN "TrackExpiry";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116070648_V0_39_0_BoCotKho') THEN
    ALTER TABLE "Item" DROP COLUMN "TrackLot";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116070648_V0_39_0_BoCotKho') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261116070648_V0_39_0_BoCotKho', '10.0.14');
    END IF;
END $EF$;
COMMIT;

