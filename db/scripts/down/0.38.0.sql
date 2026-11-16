START TRANSACTION;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116045917_V0_38_0_BoCotThamSoKho') THEN
    ALTER TABLE "CompanyParameter" ADD "InternalMarkupPercent" numeric(9,4) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116045917_V0_38_0_BoCotThamSoKho') THEN
    ALTER TABLE "CompanyParameter" ADD "KpiSlowMovingDays" integer NOT NULL DEFAULT 90;
    END IF;
END $EF$;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116045917_V0_38_0_BoCotThamSoKho') THEN
    ALTER TABLE "CompanyParameter" ADD "KpiSlowMovingTarget" numeric(9,4) NOT NULL DEFAULT 10.0;
    END IF;
END $EF$;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116045917_V0_38_0_BoCotThamSoKho') THEN
    ALTER TABLE "CompanyParameter" ADD "RequireReceiptPo" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;
DO $EF$
BEGIN
    IF EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116045917_V0_38_0_BoCotThamSoKho') THEN
    DELETE FROM "__EFMigrationsHistory"
    WHERE "MigrationId" = '20261116045917_V0_38_0_BoCotThamSoKho';
    END IF;
END $EF$;
COMMIT;

