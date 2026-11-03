START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103134628_V0_17_0_CanhBaoGiaCaoPo') THEN
    ALTER TABLE "CompanyParameter" ADD "PoHighPriceWarnPercent" numeric(9,4) NOT NULL DEFAULT 10.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103134628_V0_17_0_CanhBaoGiaCaoPo') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261103134628_V0_17_0_CanhBaoGiaCaoPo', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103140327_V0_17_0_HopDongTuKetQuaThau') THEN
    ALTER TABLE "RfqSupplier" ADD "ContractId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103140327_V0_17_0_HopDongTuKetQuaThau') THEN
    CREATE INDEX "IX_RfqSupplier_ContractId" ON "RfqSupplier" ("ContractId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103140327_V0_17_0_HopDongTuKetQuaThau') THEN
    ALTER TABLE "RfqSupplier" ADD CONSTRAINT "FK_RfqSupplier_PurchaseContract_ContractId" FOREIGN KEY ("ContractId") REFERENCES "PurchaseContract" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103140327_V0_17_0_HopDongTuKetQuaThau') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261103140327_V0_17_0_HopDongTuKetQuaThau', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103141843_V0_17_0_ThoiDiemGuiDuyet') THEN
    ALTER TABLE "ApprovalStep" ADD "SubmittedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103141843_V0_17_0_ThoiDiemGuiDuyet') THEN
    ALTER TABLE "ApprovalStep" ADD "SubmittedBy" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103141843_V0_17_0_ThoiDiemGuiDuyet') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261103141843_V0_17_0_ThoiDiemGuiDuyet', '10.0.14');
    END IF;
END $EF$;
COMMIT;

