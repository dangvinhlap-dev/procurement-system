START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116003854_V0_37_0_LamMoiTest') THEN
    ALTER TABLE "AgentEnvironment" ADD "ServiceKeyCreatedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116003854_V0_37_0_LamMoiTest') THEN
    ALTER TABLE "AgentEnvironment" ADD "ServiceKeyCreatedByName" character varying(256);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116003854_V0_37_0_LamMoiTest') THEN
    ALTER TABLE "AgentEnvironment" ADD "ServiceKeyHash" character(64);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116003854_V0_37_0_LamMoiTest') THEN
    ALTER TABLE "AgentEnvironment" ADD "ServiceKeyHint" character varying(8);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116003854_V0_37_0_LamMoiTest') THEN
    CREATE UNIQUE INDEX "IX_AgentEnvironment_ServiceKeyHash" ON "AgentEnvironment" ("ServiceKeyHash") WHERE "ServiceKeyHash" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261116003854_V0_37_0_LamMoiTest') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261116003854_V0_37_0_LamMoiTest', '10.0.14');
    END IF;
END $EF$;
COMMIT;

