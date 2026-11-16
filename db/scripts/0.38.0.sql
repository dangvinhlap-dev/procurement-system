START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116045917_V0_38_0_BoCotThamSoKho') THEN
    ALTER TABLE "CompanyParameter" DROP COLUMN "InternalMarkupPercent";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116045917_V0_38_0_BoCotThamSoKho') THEN
    ALTER TABLE "CompanyParameter" DROP COLUMN "KpiSlowMovingDays";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116045917_V0_38_0_BoCotThamSoKho') THEN
    ALTER TABLE "CompanyParameter" DROP COLUMN "KpiSlowMovingTarget";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116045917_V0_38_0_BoCotThamSoKho') THEN
    ALTER TABLE "CompanyParameter" DROP COLUMN "RequireReceiptPo";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116045917_V0_38_0_BoCotThamSoKho') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261116045917_V0_38_0_BoCotThamSoKho', '10.0.14');
    END IF;
END $EF$;
COMMIT;

