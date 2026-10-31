-- Sinh dữ liệu đo hiệu năng trong CSDL procurement_perf (KHÔNG chạy trên CSDL dev/test/thật).
-- BHA: 3.000 mặt hàng, 200 NCC, 20.000 phiếu nhập × 5 dòng, 10.000 phiếu chuyển KT → KP1 × 5 dòng, kỳ 10/2026.
\set ON_ERROR_STOP on
BEGIN;
CREATE TEMP TABLE ctx AS
SELECT c."Id" AS company, kt."Id" AS kt, kp."Id" AS kp, p."Id" AS period,
       (SELECT "Id" FROM "ItemCategory" WHERE "Code" = 'VT') AS category,
       (SELECT "Id" FROM "Uom" WHERE "Code" = 'CAI') AS uom,
       (SELECT "Id" FROM "SupplierGroup" WHERE "Code" = 'TN') AS sgroup
FROM "Company" c
JOIN "Warehouse" kt ON kt."CompanyId" = c."Id" AND kt."Code" = 'KT'
JOIN "Warehouse" kp ON kp."CompanyId" = c."Id" AND kp."Code" = 'KP1'
JOIN "InventoryPeriod" p ON p."CompanyId" = c."Id" AND p."Year" = 2026 AND p."Month" = 10
WHERE c."Code" = 'BHA';

UPDATE "ItemCategory" SET "InventoryAccount" = '1521' WHERE "Code" = 'VT';

INSERT INTO "Item" ("Id","Code","Name","Type","CategoryId","BaseUomId","VatCode","TrackLot","TrackExpiry","MinStock","IsActive","SearchText","CreatedAt")
SELECT gen_random_uuid(), 'PERF-' || lpad(g::text, 5, '0'), 'Vật tư đo hiệu năng ' || g, 1, ctx.category, ctx.uom, '10', false, false,
       CASE WHEN g % 10 = 0 THEN 500 END, true, 'perf-' || g || ' vat tu do hieu nang ' || g, now()
FROM ctx, generate_series(1, 3000) g;

INSERT INTO "Supplier" ("Id","Code","Name","GroupId","IsForeign","Status","SearchText","CreatedAt")
SELECT gen_random_uuid(), 'NPERF' || lpad(g::text, 4, '0'), 'NCC đo hiệu năng ' || g, ctx.sgroup, false, 2, 'nperf' || g, now()
FROM ctx, generate_series(1, 200) g;
INSERT INTO "SupplierCompany" ("Id","SupplierId","CompanyId","LocalCode","IsActive","CreatedAt")
SELECT gen_random_uuid(), s."Id", ctx.company, '331-' || s."Code", true, now() FROM ctx, "Supplier" s WHERE s."Code" LIKE 'NPERF%';

CREATE TEMP TABLE items AS SELECT row_number() OVER (ORDER BY "Code") AS n, "Id" FROM "Item" WHERE "Code" LIKE 'PERF-%';
CREATE TEMP TABLE sups AS SELECT row_number() OVER (ORDER BY "Code") AS n, "Id" FROM "Supplier" WHERE "Code" LIKE 'NPERF%';

-- Phiếu nhập mua đã ghi sổ.
CREATE TEMP TABLE grn AS
SELECT gen_random_uuid() AS id, g AS n, DATE '2026-10-01' + (g % 31) AS d, (SELECT "Id" FROM sups WHERE sups.n = 1 + g % 200) AS sup FROM generate_series(1, 20000) g;
INSERT INTO "InventoryDocument" ("Id","CompanyId","DocType","No","Date","Status","ToWarehouseId","SupplierId","InvoiceNo","PeriodId","PostedAt","TotalAmount","TotalVat","CreatedAt")
SELECT grn.id, ctx.company, 'GRN', 'GRN-BHA-PERF-' || lpad(grn.n::text, 6, '0'), grn.d, 2, ctx.kt, grn.sup, lpad(grn.n::text, 7, '0'), ctx.period, now(), 5 * 1000000, 5 * 100000, now()
FROM grn, ctx;
CREATE TEMP TABLE grn_line AS
SELECT gen_random_uuid() AS id, grn.id AS doc, grn.d, l AS line_no, (SELECT "Id" FROM items WHERE items.n = 1 + (grn.n * 7 + l * 13) % 3000) AS item
FROM grn, generate_series(1, 5) l;
INSERT INTO "InventoryDocumentLine" ("Id","DocumentId","LineNo","ItemId","UomId","Qty","UomFactor","BaseQty","UnitPrice","Amount","VatCode","VatAmount")
SELECT gl.id, gl.doc, gl.line_no, gl.item, ctx.uom, 100, 1, 100, 10000, 1000000, '10', 100000 FROM grn_line gl, ctx;
INSERT INTO "StockLedger" ("Id","CompanyId","WarehouseId","ItemId","PeriodId","PostingDate","DocumentId","DocumentLineId","DocType","Direction","BaseQty","UnitCost","Amount","IsCostFinal")
SELECT gen_random_uuid(), ctx.company, ctx.kt, gl.item, ctx.period, gl.d, gl.doc, gl.id, 'GRN', 1, 100, 10000, 1000000, true FROM grn_line gl, ctx;

-- Phiếu chuyển kho KT → KP1 đã ghi sổ (hai dòng sổ kho mỗi dòng phiếu).
CREATE TEMP TABLE trf AS SELECT gen_random_uuid() AS id, g AS n, DATE '2026-10-01' + (g % 31) AS d FROM generate_series(1, 10000) g;
INSERT INTO "InventoryDocument" ("Id","CompanyId","DocType","No","Date","Status","FromWarehouseId","ToWarehouseId","PeriodId","PostedAt","TotalAmount","TotalVat","CreatedAt")
SELECT trf.id, ctx.company, 'TRF', 'TRF-BHA-PERF-' || lpad(trf.n::text, 6, '0'), trf.d, 2, ctx.kt, ctx.kp, ctx.period, now(), 0, 0, now() FROM trf, ctx;
CREATE TEMP TABLE trf_line AS
SELECT gen_random_uuid() AS id, trf.id AS doc, trf.d, l AS line_no, (SELECT "Id" FROM items WHERE items.n = 1 + (trf.n * 11 + l * 17) % 3000) AS item
FROM trf, generate_series(1, 5) l;
INSERT INTO "InventoryDocumentLine" ("Id","DocumentId","LineNo","ItemId","UomId","Qty","UomFactor","BaseQty","UnitPrice","Amount","VatAmount")
SELECT tl.id, tl.doc, tl.line_no, tl.item, ctx.uom, 10, 1, 10, 0, 0, 0 FROM trf_line tl, ctx;
INSERT INTO "StockLedger" ("Id","CompanyId","WarehouseId","ItemId","PeriodId","PostingDate","DocumentId","DocumentLineId","DocType","Direction","BaseQty","UnitCost","Amount","IsCostFinal")
SELECT gen_random_uuid(), ctx.company, w.wh, tl.item, ctx.period, tl.d, tl.doc, tl.id, 'TRF', w.dir, 10, 10000, 100000, false
FROM trf_line tl, ctx, LATERAL (VALUES (ctx.kt, -1), (ctx.kp, 1)) AS w(wh, dir);

-- Tồn hiện tại = cộng sổ kho của các mặt hàng đo.
INSERT INTO "StockOnHand" ("Id","CompanyId","WarehouseId","ItemId","LotNo","Qty","ReservedQty")
SELECT gen_random_uuid(), l."CompanyId", l."WarehouseId", l."ItemId", NULL, sum(l."Direction" * l."BaseQty"), 0
FROM "StockLedger" l JOIN items i ON i."Id" = l."ItemId"
GROUP BY l."CompanyId", l."WarehouseId", l."ItemId";
COMMIT;
ANALYZE;
SELECT 'ledger', count(*) FROM "StockLedger" UNION ALL SELECT 'lines', count(*) FROM "InventoryDocumentLine" UNION ALL SELECT 'onhand', count(*) FROM "StockOnHand";
