START TRANSACTION;
ALTER TABLE "Warehouse" ADD "AllowNegativeStock" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE "Warehouse" ADD "ParentWarehouseId" uuid;

ALTER TABLE "Warehouse" ADD "Type" integer NOT NULL DEFAULT 2;

ALTER TABLE "Item" ADD "IsCritical" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE "Item" ADD "MaxStock" numeric(18,4);

ALTER TABLE "Item" ADD "MinStock" numeric(18,4);

ALTER TABLE "Item" ADD "ReorderPoint" numeric(18,4);

ALTER TABLE "Item" ADD "SafetyStock" numeric(18,4);

ALTER TABLE "Item" ADD "TrackExpiry" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE "Item" ADD "TrackLot" boolean NOT NULL DEFAULT FALSE;

CREATE UNIQUE INDEX "IX_Warehouse_CompanyId_Main" ON "Warehouse" ("CompanyId") WHERE "Type" = 1;

CREATE INDEX "IX_Warehouse_ParentWarehouseId_CompanyId" ON "Warehouse" ("ParentWarehouseId", "CompanyId");

ALTER TABLE "Warehouse" ADD CONSTRAINT "FK_Warehouse_Warehouse_ParentWarehouseId_CompanyId" FOREIGN KEY ("ParentWarehouseId", "CompanyId") REFERENCES "Warehouse" ("Id", "CompanyId") ON DELETE RESTRICT;

DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20261116070648_V0_39_0_BoCotKho';

COMMIT;

