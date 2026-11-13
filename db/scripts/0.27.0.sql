START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112081247_V0_25_0_BaoGiaPortalNiemPhong') THEN
    ALTER TABLE "RfqSupplier" ADD "IsFromPortal" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112081247_V0_25_0_BaoGiaPortalNiemPhong') THEN
    ALTER TABLE "RfqSupplier" ADD "PortalSubmissionId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112081247_V0_25_0_BaoGiaPortalNiemPhong') THEN
    ALTER TABLE "Rfq" ADD "DeadlineTime" time without time zone NOT NULL DEFAULT TIME '23:59:00';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112081247_V0_25_0_BaoGiaPortalNiemPhong') THEN
    ALTER TABLE "Rfq" ADD "PortalQuotesOpenedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112081247_V0_25_0_BaoGiaPortalNiemPhong') THEN
    ALTER TABLE "Rfq" ADD "PortalQuotesOpenedBy" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112081247_V0_25_0_BaoGiaPortalNiemPhong') THEN
    CREATE TABLE "RfqPortalSubmission" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "RfqId" uuid NOT NULL,
        "SupplierId" uuid NOT NULL,
        "Sequence" integer NOT NULL,
        "SubmittedAt" timestamp with time zone NOT NULL,
        "SubmittedByPortalUserId" uuid NOT NULL,
        "SubmittedByEmail" character varying(256) NOT NULL,
        "QuoteDate" date NOT NULL,
        "ValidUntil" date,
        "PaymentTerms" character varying(200),
        "Note" character varying(1000),
        "ContentHash" character(64) NOT NULL,
        CONSTRAINT "PK_RfqPortalSubmission" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_RfqPortalSubmission_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_RfqPortalSubmission_Rfq_RfqId" FOREIGN KEY ("RfqId") REFERENCES "Rfq" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_RfqPortalSubmission_SupplierPortalUser_SubmittedByPortalUse~" FOREIGN KEY ("SubmittedByPortalUserId") REFERENCES "SupplierPortalUser" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_RfqPortalSubmission_Supplier_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112081247_V0_25_0_BaoGiaPortalNiemPhong') THEN
    CREATE TABLE "RfqPortalSubmissionLine" (
        "Id" uuid NOT NULL,
        "SubmissionId" uuid NOT NULL,
        "RfqLineId" uuid NOT NULL,
        "UnitPrice" numeric(19,6) NOT NULL,
        "DiscountPct" numeric(9,4) NOT NULL,
        "VatCode" character varying(5),
        "LeadTimeDays" integer,
        "Note" character varying(500),
        CONSTRAINT "PK_RfqPortalSubmissionLine" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_RfqPortalSubmissionLine_RfqLine_RfqLineId" FOREIGN KEY ("RfqLineId") REFERENCES "RfqLine" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_RfqPortalSubmissionLine_RfqPortalSubmission_SubmissionId" FOREIGN KEY ("SubmissionId") REFERENCES "RfqPortalSubmission" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112081247_V0_25_0_BaoGiaPortalNiemPhong') THEN
    CREATE INDEX "IX_RfqPortalSubmission_CompanyId" ON "RfqPortalSubmission" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112081247_V0_25_0_BaoGiaPortalNiemPhong') THEN
    CREATE UNIQUE INDEX "IX_RfqPortalSubmission_RfqId_SupplierId_Sequence" ON "RfqPortalSubmission" ("RfqId", "SupplierId", "Sequence");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112081247_V0_25_0_BaoGiaPortalNiemPhong') THEN
    CREATE INDEX "IX_RfqPortalSubmission_SubmittedByPortalUserId" ON "RfqPortalSubmission" ("SubmittedByPortalUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112081247_V0_25_0_BaoGiaPortalNiemPhong') THEN
    CREATE INDEX "IX_RfqPortalSubmission_SupplierId_RfqId" ON "RfqPortalSubmission" ("SupplierId", "RfqId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112081247_V0_25_0_BaoGiaPortalNiemPhong') THEN
    CREATE INDEX "IX_RfqPortalSubmissionLine_RfqLineId" ON "RfqPortalSubmissionLine" ("RfqLineId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112081247_V0_25_0_BaoGiaPortalNiemPhong') THEN
    CREATE UNIQUE INDEX "IX_RfqPortalSubmissionLine_SubmissionId_RfqLineId" ON "RfqPortalSubmissionLine" ("SubmissionId", "RfqLineId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112081247_V0_25_0_BaoGiaPortalNiemPhong') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261112081247_V0_25_0_BaoGiaPortalNiemPhong', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112083733_V0_25_0_DinhKemBaoGiaPortal') THEN
    ALTER TABLE "Attachment" ADD "UploadedByPortalUserId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112083733_V0_25_0_DinhKemBaoGiaPortal') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261112083733_V0_25_0_DinhKemBaoGiaPortal', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112090418_V0_25_0_HoiDapBaoGiaPortal') THEN
    CREATE TABLE "RfqClarification" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "RfqId" uuid NOT NULL,
        "SupplierId" uuid,
        "Question" character varying(2000),
        "AskedAt" timestamp with time zone,
        "AskedByPortalUserId" uuid,
        "AskedByEmail" character varying(256),
        "Answer" character varying(2000),
        "AnsweredAt" timestamp with time zone,
        "AnsweredBy" uuid,
        "AnsweredByName" character varying(200),
        CONSTRAINT "PK_RfqClarification" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_RfqClarification_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_RfqClarification_Rfq_RfqId" FOREIGN KEY ("RfqId") REFERENCES "Rfq" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_RfqClarification_SupplierPortalUser_AskedByPortalUserId" FOREIGN KEY ("AskedByPortalUserId") REFERENCES "SupplierPortalUser" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_RfqClarification_Supplier_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_RfqClarification_User_AnsweredBy" FOREIGN KEY ("AnsweredBy") REFERENCES "User" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112090418_V0_25_0_HoiDapBaoGiaPortal') THEN
    CREATE INDEX "IX_RfqClarification_AnsweredBy" ON "RfqClarification" ("AnsweredBy");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112090418_V0_25_0_HoiDapBaoGiaPortal') THEN
    CREATE INDEX "IX_RfqClarification_AskedByPortalUserId" ON "RfqClarification" ("AskedByPortalUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112090418_V0_25_0_HoiDapBaoGiaPortal') THEN
    CREATE INDEX "IX_RfqClarification_CompanyId" ON "RfqClarification" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112090418_V0_25_0_HoiDapBaoGiaPortal') THEN
    CREATE INDEX "IX_RfqClarification_RfqId" ON "RfqClarification" ("RfqId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112090418_V0_25_0_HoiDapBaoGiaPortal') THEN
    CREATE INDEX "IX_RfqClarification_SupplierId" ON "RfqClarification" ("SupplierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112090418_V0_25_0_HoiDapBaoGiaPortal') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261112090418_V0_25_0_HoiDapBaoGiaPortal', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112094131_V0_25_0_EmailPortalNcc') THEN
    CREATE TABLE "SupplierPortalEmail" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "SupplierId" uuid NOT NULL,
        "PortalUserId" uuid NOT NULL,
        "ToAddress" character varying(256) NOT NULL,
        "ToName" character varying(200) NOT NULL,
        "Kind" integer NOT NULL,
        "DocType" character varying(20) NOT NULL,
        "DocId" uuid NOT NULL,
        "Subject" character varying(300) NOT NULL,
        "HtmlBody" text NOT NULL,
        "TextBody" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "State" integer NOT NULL,
        "Attempts" integer NOT NULL,
        "SentAt" timestamp with time zone,
        "Note" character varying(500),
        CONSTRAINT "PK_SupplierPortalEmail" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SupplierPortalEmail_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_SupplierPortalEmail_SupplierPortalUser_PortalUserId" FOREIGN KEY ("PortalUserId") REFERENCES "SupplierPortalUser" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_SupplierPortalEmail_Supplier_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112094131_V0_25_0_EmailPortalNcc') THEN
    CREATE INDEX "IX_SupplierPortalEmail_CompanyId" ON "SupplierPortalEmail" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112094131_V0_25_0_EmailPortalNcc') THEN
    CREATE INDEX "IX_SupplierPortalEmail_DocType_DocId" ON "SupplierPortalEmail" ("DocType", "DocId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112094131_V0_25_0_EmailPortalNcc') THEN
    CREATE INDEX "IX_SupplierPortalEmail_PortalUserId" ON "SupplierPortalEmail" ("PortalUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112094131_V0_25_0_EmailPortalNcc') THEN
    CREATE INDEX "IX_SupplierPortalEmail_State_CreatedAt" ON "SupplierPortalEmail" ("State", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112094131_V0_25_0_EmailPortalNcc') THEN
    CREATE INDEX "IX_SupplierPortalEmail_SupplierId" ON "SupplierPortalEmail" ("SupplierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112094131_V0_25_0_EmailPortalNcc') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261112094131_V0_25_0_EmailPortalNcc', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112101334_V0_26_0_NccXacNhanDonHang') THEN
    CREATE TABLE "PurchaseOrderSupplierResponse" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "PurchaseOrderId" uuid NOT NULL,
        "SupplierId" uuid NOT NULL,
        "Decision" integer NOT NULL,
        "PromisedDate" date,
        "Note" character varying(1000),
        "RespondedAt" timestamp with time zone NOT NULL,
        "PortalUserId" uuid NOT NULL,
        "RespondedByEmail" character varying(256) NOT NULL,
        "ReopenedAt" timestamp with time zone,
        "ReopenedBy" uuid,
        "ReopenedByName" character varying(200),
        "ReopenReason" character varying(1000),
        CONSTRAINT "PK_PurchaseOrderSupplierResponse" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PurchaseOrderSupplierResponse_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseOrderSupplierResponse_PurchaseOrder_PurchaseOrderId" FOREIGN KEY ("PurchaseOrderId") REFERENCES "PurchaseOrder" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseOrderSupplierResponse_SupplierPortalUser_PortalUser~" FOREIGN KEY ("PortalUserId") REFERENCES "SupplierPortalUser" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseOrderSupplierResponse_Supplier_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseOrderSupplierResponse_User_ReopenedBy" FOREIGN KEY ("ReopenedBy") REFERENCES "User" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112101334_V0_26_0_NccXacNhanDonHang') THEN
    CREATE INDEX "IX_PurchaseOrderSupplierResponse_CompanyId" ON "PurchaseOrderSupplierResponse" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112101334_V0_26_0_NccXacNhanDonHang') THEN
    CREATE INDEX "IX_PurchaseOrderSupplierResponse_PortalUserId" ON "PurchaseOrderSupplierResponse" ("PortalUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112101334_V0_26_0_NccXacNhanDonHang') THEN
    CREATE UNIQUE INDEX "IX_PurchaseOrderSupplierResponse_PurchaseOrderId" ON "PurchaseOrderSupplierResponse" ("PurchaseOrderId") WHERE "ReopenedAt" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112101334_V0_26_0_NccXacNhanDonHang') THEN
    CREATE INDEX "IX_PurchaseOrderSupplierResponse_ReopenedBy" ON "PurchaseOrderSupplierResponse" ("ReopenedBy");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112101334_V0_26_0_NccXacNhanDonHang') THEN
    CREATE INDEX "IX_PurchaseOrderSupplierResponse_SupplierId" ON "PurchaseOrderSupplierResponse" ("SupplierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261112101334_V0_26_0_NccXacNhanDonHang') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261112101334_V0_26_0_NccXacNhanDonHang', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113004006_V0_26_0_GuiKetQuaChonThau') THEN
    ALTER TABLE "Rfq" ADD "ResultSentAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113004006_V0_26_0_GuiKetQuaChonThau') THEN
    ALTER TABLE "Rfq" ADD "ResultSentBy" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113004006_V0_26_0_GuiKetQuaChonThau') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261113004006_V0_26_0_GuiKetQuaChonThau', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113011045_V0_26_0_NccBaoGiaoHang') THEN
    ALTER TABLE "GoodsReceiptConfirmation" ADD "DeliveryNoticeId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113011045_V0_26_0_NccBaoGiaoHang') THEN
    CREATE TABLE "PurchaseOrderDeliveryNotice" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "No" character varying(50) NOT NULL,
        "PurchaseOrderId" uuid NOT NULL,
        "SupplierId" uuid NOT NULL,
        "ExpectedDate" date NOT NULL,
        "DeliveryNoteNo" character varying(100) NOT NULL,
        "Deliverer" character varying(200),
        "Note" character varying(1000),
        "Status" integer NOT NULL,
        "SubmittedAt" timestamp with time zone NOT NULL,
        "PortalUserId" uuid NOT NULL,
        "SubmittedByEmail" character varying(256) NOT NULL,
        "CancelledAt" timestamp with time zone,
        "CancelledByEmail" character varying(256),
        "CancelReason" character varying(1000),
        CONSTRAINT "PK_PurchaseOrderDeliveryNotice" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PurchaseOrderDeliveryNotice_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseOrderDeliveryNotice_PurchaseOrder_PurchaseOrderId" FOREIGN KEY ("PurchaseOrderId") REFERENCES "PurchaseOrder" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseOrderDeliveryNotice_SupplierPortalUser_PortalUserId" FOREIGN KEY ("PortalUserId") REFERENCES "SupplierPortalUser" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseOrderDeliveryNotice_Supplier_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113011045_V0_26_0_NccBaoGiaoHang') THEN
    CREATE TABLE "PurchaseOrderDeliveryNoticeLine" (
        "Id" uuid NOT NULL,
        "NoticeId" uuid NOT NULL,
        "LineNo" integer NOT NULL,
        "PurchaseOrderLineId" uuid NOT NULL,
        "Qty" numeric(18,4) NOT NULL,
        "Note" character varying(500),
        CONSTRAINT "PK_PurchaseOrderDeliveryNoticeLine" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PurchaseOrderDeliveryNoticeLine_PurchaseOrderDeliveryNotice~" FOREIGN KEY ("NoticeId") REFERENCES "PurchaseOrderDeliveryNotice" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_PurchaseOrderDeliveryNoticeLine_PurchaseOrderLine_PurchaseO~" FOREIGN KEY ("PurchaseOrderLineId") REFERENCES "PurchaseOrderLine" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113011045_V0_26_0_NccBaoGiaoHang') THEN
    CREATE UNIQUE INDEX "IX_GoodsReceiptConfirmation_DeliveryNoticeId" ON "GoodsReceiptConfirmation" ("DeliveryNoticeId") WHERE "DeliveryNoticeId" IS NOT NULL AND "Status" <> 3;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113011045_V0_26_0_NccBaoGiaoHang') THEN
    CREATE UNIQUE INDEX "IX_PurchaseOrderDeliveryNotice_CompanyId_No" ON "PurchaseOrderDeliveryNotice" ("CompanyId", "No");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113011045_V0_26_0_NccBaoGiaoHang') THEN
    CREATE INDEX "IX_PurchaseOrderDeliveryNotice_PortalUserId" ON "PurchaseOrderDeliveryNotice" ("PortalUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113011045_V0_26_0_NccBaoGiaoHang') THEN
    CREATE INDEX "IX_PurchaseOrderDeliveryNotice_PurchaseOrderId" ON "PurchaseOrderDeliveryNotice" ("PurchaseOrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113011045_V0_26_0_NccBaoGiaoHang') THEN
    CREATE INDEX "IX_PurchaseOrderDeliveryNotice_SupplierId" ON "PurchaseOrderDeliveryNotice" ("SupplierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113011045_V0_26_0_NccBaoGiaoHang') THEN
    CREATE INDEX "IX_PurchaseOrderDeliveryNoticeLine_NoticeId_LineNo" ON "PurchaseOrderDeliveryNoticeLine" ("NoticeId", "LineNo");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113011045_V0_26_0_NccBaoGiaoHang') THEN
    CREATE INDEX "IX_PurchaseOrderDeliveryNoticeLine_PurchaseOrderLineId" ON "PurchaseOrderDeliveryNoticeLine" ("PurchaseOrderLineId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113011045_V0_26_0_NccBaoGiaoHang') THEN
    ALTER TABLE "GoodsReceiptConfirmation" ADD CONSTRAINT "FK_GoodsReceiptConfirmation_PurchaseOrderDeliveryNotice_Delive~" FOREIGN KEY ("DeliveryNoticeId") REFERENCES "PurchaseOrderDeliveryNotice" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113011045_V0_26_0_NccBaoGiaoHang') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261113011045_V0_26_0_NccBaoGiaoHang', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113033635_V0_27_0_DeNghiThanhToan') THEN
    CREATE TABLE "PaymentRequest" (
        "Id" uuid NOT NULL,
        "PurchaseOrderId" uuid NOT NULL,
        "SupplierId" uuid NOT NULL,
        "Form" integer NOT NULL,
        "DepartmentId" uuid,
        "ConstructionProjectId" uuid,
        "Method" integer NOT NULL,
        "BankAccountId" uuid,
        "BankName" character varying(200),
        "BankBranch" character varying(200),
        "BankAccountNumber" character varying(50),
        "BankAccountName" character varying(200),
        "TotalAmount" numeric(19,2) NOT NULL,
        "DeductionAmount" numeric(19,2) NOT NULL,
        "DeductionReason" character varying(1000),
        "PayableAmount" numeric(19,2) NOT NULL,
        "PaidDate" date,
        "PaidBy" uuid,
        "PaidRecordedAt" timestamp with time zone,
        "PaymentNote" character varying(1000),
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        "CompanyId" uuid NOT NULL,
        "No" character varying(50) NOT NULL,
        "Date" date NOT NULL,
        "Status" integer NOT NULL,
        "ApprovalRound" integer NOT NULL,
        "RejectReason" character varying(500),
        "ApprovedAt" timestamp with time zone,
        "Note" character varying(1000),
        "ClosedReason" character varying(500),
        "ClosedBy" uuid,
        "ClosedAt" timestamp with time zone,
        "BudgetAmount" numeric(19,2),
        "BudgetUsedAmount" numeric(19,2),
        "IsOverBudget" boolean NOT NULL,
        "CancelReason" character varying(500),
        CONSTRAINT "PK_PaymentRequest" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PaymentRequest_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PaymentRequest_ConstructionProject_ConstructionProjectId_Co~" FOREIGN KEY ("ConstructionProjectId", "CompanyId") REFERENCES "ConstructionProject" ("Id", "CompanyId") ON DELETE RESTRICT,
        CONSTRAINT "FK_PaymentRequest_Department_DepartmentId_CompanyId" FOREIGN KEY ("DepartmentId", "CompanyId") REFERENCES "Department" ("Id", "CompanyId") ON DELETE RESTRICT,
        CONSTRAINT "FK_PaymentRequest_PurchaseOrder_PurchaseOrderId" FOREIGN KEY ("PurchaseOrderId") REFERENCES "PurchaseOrder" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PaymentRequest_Supplier_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113033635_V0_27_0_DeNghiThanhToan') THEN
    CREATE TABLE "PaymentRequestLine" (
        "Id" uuid NOT NULL,
        "RequestId" uuid NOT NULL,
        "LineNo" integer NOT NULL,
        "ReceiptConfirmationId" uuid NOT NULL,
        "InvoiceNo" character varying(50) NOT NULL,
        "InvoiceSeries" character varying(50),
        "InvoiceDate" date,
        "Amount" numeric(19,2) NOT NULL,
        "Description" character varying(500) NOT NULL,
        CONSTRAINT "PK_PaymentRequestLine" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PaymentRequestLine_GoodsReceiptConfirmation_ReceiptConfirma~" FOREIGN KEY ("ReceiptConfirmationId") REFERENCES "GoodsReceiptConfirmation" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PaymentRequestLine_PaymentRequest_RequestId" FOREIGN KEY ("RequestId") REFERENCES "PaymentRequest" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113033635_V0_27_0_DeNghiThanhToan') THEN
    CREATE INDEX "IX_PaymentRequest_CompanyId_Date" ON "PaymentRequest" ("CompanyId", "Date");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113033635_V0_27_0_DeNghiThanhToan') THEN
    CREATE UNIQUE INDEX "IX_PaymentRequest_CompanyId_No" ON "PaymentRequest" ("CompanyId", "No");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113033635_V0_27_0_DeNghiThanhToan') THEN
    CREATE INDEX "IX_PaymentRequest_ConstructionProjectId_CompanyId" ON "PaymentRequest" ("ConstructionProjectId", "CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113033635_V0_27_0_DeNghiThanhToan') THEN
    CREATE INDEX "IX_PaymentRequest_DepartmentId_CompanyId" ON "PaymentRequest" ("DepartmentId", "CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113033635_V0_27_0_DeNghiThanhToan') THEN
    CREATE INDEX "IX_PaymentRequest_PurchaseOrderId" ON "PaymentRequest" ("PurchaseOrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113033635_V0_27_0_DeNghiThanhToan') THEN
    CREATE INDEX "IX_PaymentRequest_SupplierId" ON "PaymentRequest" ("SupplierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113033635_V0_27_0_DeNghiThanhToan') THEN
    CREATE INDEX "IX_PaymentRequestLine_ReceiptConfirmationId" ON "PaymentRequestLine" ("ReceiptConfirmationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113033635_V0_27_0_DeNghiThanhToan') THEN
    CREATE INDEX "IX_PaymentRequestLine_RequestId_LineNo" ON "PaymentRequestLine" ("RequestId", "LineNo");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113033635_V0_27_0_DeNghiThanhToan') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261113033635_V0_27_0_DeNghiThanhToan', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113061036_V0_27_0_BangKeTapVu') THEN
    CREATE TABLE "SundryExpenseSheet" (
        "Id" uuid NOT NULL,
        "DepartmentId" uuid NOT NULL,
        "TotalAmount" numeric(19,2) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        "CompanyId" uuid NOT NULL,
        "No" character varying(50) NOT NULL,
        "Date" date NOT NULL,
        "Status" integer NOT NULL,
        "ApprovalRound" integer NOT NULL,
        "RejectReason" character varying(500),
        "ApprovedAt" timestamp with time zone,
        "Note" character varying(1000),
        "ClosedReason" character varying(500),
        "ClosedBy" uuid,
        "ClosedAt" timestamp with time zone,
        "BudgetAmount" numeric(19,2),
        "BudgetUsedAmount" numeric(19,2),
        "IsOverBudget" boolean NOT NULL,
        "CancelReason" character varying(500),
        CONSTRAINT "PK_SundryExpenseSheet" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SundryExpenseSheet_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_SundryExpenseSheet_Department_DepartmentId_CompanyId" FOREIGN KEY ("DepartmentId", "CompanyId") REFERENCES "Department" ("Id", "CompanyId") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113061036_V0_27_0_BangKeTapVu') THEN
    CREATE TABLE "SundryExpenseLine" (
        "Id" uuid NOT NULL,
        "SheetId" uuid NOT NULL,
        "LineNo" integer NOT NULL,
        "ExpenseDate" date NOT NULL,
        "Description" character varying(500) NOT NULL,
        "Vendor" character varying(200),
        "ReceiptNo" character varying(50),
        "Amount" numeric(19,2) NOT NULL,
        CONSTRAINT "PK_SundryExpenseLine" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SundryExpenseLine_SundryExpenseSheet_SheetId" FOREIGN KEY ("SheetId") REFERENCES "SundryExpenseSheet" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113061036_V0_27_0_BangKeTapVu') THEN
    CREATE INDEX "IX_SundryExpenseLine_SheetId_LineNo" ON "SundryExpenseLine" ("SheetId", "LineNo");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113061036_V0_27_0_BangKeTapVu') THEN
    CREATE INDEX "IX_SundryExpenseSheet_CompanyId_Date" ON "SundryExpenseSheet" ("CompanyId", "Date");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113061036_V0_27_0_BangKeTapVu') THEN
    CREATE UNIQUE INDEX "IX_SundryExpenseSheet_CompanyId_No" ON "SundryExpenseSheet" ("CompanyId", "No");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113061036_V0_27_0_BangKeTapVu') THEN
    CREATE INDEX "IX_SundryExpenseSheet_DepartmentId_CompanyId" ON "SundryExpenseSheet" ("DepartmentId", "CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113061036_V0_27_0_BangKeTapVu') THEN
    CREATE INDEX "IX_SundryExpenseSheet_DepartmentId_Date" ON "SundryExpenseSheet" ("DepartmentId", "Date");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113061036_V0_27_0_BangKeTapVu') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261113061036_V0_27_0_BangKeTapVu', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113063142_V0_27_0_DeNghiThanhToanTapVu') THEN
    ALTER TABLE "PaymentRequestLine" ALTER COLUMN "ReceiptConfirmationId" DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113063142_V0_27_0_DeNghiThanhToanTapVu') THEN
    ALTER TABLE "PaymentRequestLine" ALTER COLUMN "InvoiceNo" DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113063142_V0_27_0_DeNghiThanhToanTapVu') THEN
    ALTER TABLE "PaymentRequestLine" ADD "SundryExpenseSheetId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113063142_V0_27_0_DeNghiThanhToanTapVu') THEN
    ALTER TABLE "PaymentRequest" ALTER COLUMN "SupplierId" DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113063142_V0_27_0_DeNghiThanhToanTapVu') THEN
    ALTER TABLE "PaymentRequest" ALTER COLUMN "PurchaseOrderId" DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113063142_V0_27_0_DeNghiThanhToanTapVu') THEN
    ALTER TABLE "PaymentRequest" ADD "PayeeName" character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113063142_V0_27_0_DeNghiThanhToanTapVu') THEN
    CREATE INDEX "IX_PaymentRequestLine_SundryExpenseSheetId" ON "PaymentRequestLine" ("SundryExpenseSheetId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113063142_V0_27_0_DeNghiThanhToanTapVu') THEN
    ALTER TABLE "PaymentRequestLine" ADD CONSTRAINT "FK_PaymentRequestLine_SundryExpenseSheet_SundryExpenseSheetId" FOREIGN KEY ("SundryExpenseSheetId") REFERENCES "SundryExpenseSheet" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261113063142_V0_27_0_DeNghiThanhToanTapVu') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261113063142_V0_27_0_DeNghiThanhToanTapVu', '10.0.14');
    END IF;
END $EF$;
COMMIT;

