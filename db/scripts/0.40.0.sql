START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116101856_V0_40_0_DonGiaDuKienPR') THEN
    ALTER TABLE "PurchaseRequestLine" ADD "EstPriceManual" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116101856_V0_40_0_DonGiaDuKienPR') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261116101856_V0_40_0_DonGiaDuKienPR', '10.0.14');
    END IF;
END $EF$;
COMMIT;

