START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101124754_V0_12_0_DuyetKetQuaChonThau') THEN
    ALTER TABLE "Rfq" ADD "ApprovalRound" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101124754_V0_12_0_DuyetKetQuaChonThau') THEN
    ALTER TABLE "Rfq" ADD "AwardedBy" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101124754_V0_12_0_DuyetKetQuaChonThau') THEN
    ALTER TABLE "Rfq" ADD "FewSuppliersReason" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101124754_V0_12_0_DuyetKetQuaChonThau') THEN
    ALTER TABLE "Rfq" ADD "RejectReason" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101124754_V0_12_0_DuyetKetQuaChonThau') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261101124754_V0_12_0_DuyetKetQuaChonThau', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101131154_V0_12_0_HoSoTrinhThau') THEN
    ALTER TABLE "RfqSupplier" ADD "PriceListId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101131154_V0_12_0_HoSoTrinhThau') THEN
    CREATE TABLE "RfqDossierItem" (
        "Id" uuid NOT NULL,
        "RfqId" uuid NOT NULL,
        "Code" character varying(20) NOT NULL,
        "IsDone" boolean NOT NULL,
        "Note" character varying(500),
        CONSTRAINT "PK_RfqDossierItem" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_RfqDossierItem_Rfq_RfqId" FOREIGN KEY ("RfqId") REFERENCES "Rfq" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101131154_V0_12_0_HoSoTrinhThau') THEN
    CREATE INDEX "IX_RfqSupplier_PriceListId" ON "RfqSupplier" ("PriceListId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101131154_V0_12_0_HoSoTrinhThau') THEN
    CREATE UNIQUE INDEX "IX_RfqDossierItem_RfqId_Code" ON "RfqDossierItem" ("RfqId", "Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101131154_V0_12_0_HoSoTrinhThau') THEN
    ALTER TABLE "RfqSupplier" ADD CONSTRAINT "FK_RfqSupplier_SupplierPriceList_PriceListId" FOREIGN KEY ("PriceListId") REFERENCES "SupplierPriceList" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101131154_V0_12_0_HoSoTrinhThau') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261101131154_V0_12_0_HoSoTrinhThau', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101135024_V0_12_0_HopDongNguyenTac') THEN
    CREATE TABLE "PurchaseContract" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "No" character varying(50) NOT NULL,
        "SupplierId" uuid NOT NULL,
        "PriceListId" uuid NOT NULL,
        "ContractNo" character varying(100),
        "Title" character varying(200) NOT NULL,
        "SignDate" date,
        "ValidFrom" date NOT NULL,
        "ValidTo" date NOT NULL,
        "PriceAdjustmentTerms" character varying(2000),
        "PaymentTerms" character varying(2000),
        "CreditLimit" numeric(19,2),
        "DeliveryTerms" character varying(2000),
        "DiscountTerms" character varying(2000),
        "SponsorshipTerms" character varying(2000),
        "PenaltyTerms" character varying(2000),
        "OtherTerms" character varying(4000),
        "Note" character varying(1000),
        "Status" integer NOT NULL,
        "ActivatedAt" timestamp with time zone,
        "TerminatedOn" date,
        "TerminationReason" character varying(500),
        "ApprovalRound" integer NOT NULL,
        "RejectReason" character varying(500),
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_PurchaseContract" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_PurchaseContract_Validity" CHECK ("ValidTo" >= "ValidFrom"),
        CONSTRAINT "FK_PurchaseContract_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseContract_SupplierPriceList_PriceListId" FOREIGN KEY ("PriceListId") REFERENCES "SupplierPriceList" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseContract_Supplier_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101135024_V0_12_0_HopDongNguyenTac') THEN
    CREATE TABLE "PurchaseContractDepartment" (
        "Id" uuid NOT NULL,
        "ContractId" uuid NOT NULL,
        "DepartmentId" uuid NOT NULL,
        CONSTRAINT "PK_PurchaseContractDepartment" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PurchaseContractDepartment_Department_DepartmentId" FOREIGN KEY ("DepartmentId") REFERENCES "Department" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseContractDepartment_PurchaseContract_ContractId" FOREIGN KEY ("ContractId") REFERENCES "PurchaseContract" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101135024_V0_12_0_HopDongNguyenTac') THEN
    CREATE TABLE "PurchaseContractLine" (
        "Id" uuid NOT NULL,
        "ContractId" uuid NOT NULL,
        "LineNo" integer NOT NULL,
        "ItemId" uuid NOT NULL,
        "UomId" uuid NOT NULL,
        "ExpectedQty" numeric(18,4) NOT NULL,
        "MinCommitQty" numeric(18,4),
        "UnitPrice" numeric(19,6) NOT NULL,
        "DiscountPct" numeric(9,4) NOT NULL,
        "ExpectedAmount" numeric(19,2) NOT NULL,
        "Note" character varying(500),
        CONSTRAINT "PK_PurchaseContractLine" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PurchaseContractLine_Item_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Item" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseContractLine_PurchaseContract_ContractId" FOREIGN KEY ("ContractId") REFERENCES "PurchaseContract" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_PurchaseContractLine_Uom_UomId" FOREIGN KEY ("UomId") REFERENCES "Uom" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101135024_V0_12_0_HopDongNguyenTac') THEN
    CREATE UNIQUE INDEX "IX_PurchaseContract_CompanyId_No" ON "PurchaseContract" ("CompanyId", "No");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101135024_V0_12_0_HopDongNguyenTac') THEN
    CREATE INDEX "IX_PurchaseContract_CompanyId_SupplierId_Status" ON "PurchaseContract" ("CompanyId", "SupplierId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101135024_V0_12_0_HopDongNguyenTac') THEN
    CREATE INDEX "IX_PurchaseContract_PriceListId" ON "PurchaseContract" ("PriceListId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101135024_V0_12_0_HopDongNguyenTac') THEN
    CREATE INDEX "IX_PurchaseContract_SupplierId" ON "PurchaseContract" ("SupplierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101135024_V0_12_0_HopDongNguyenTac') THEN
    CREATE UNIQUE INDEX "IX_PurchaseContractDepartment_ContractId_DepartmentId" ON "PurchaseContractDepartment" ("ContractId", "DepartmentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101135024_V0_12_0_HopDongNguyenTac') THEN
    CREATE INDEX "IX_PurchaseContractDepartment_DepartmentId" ON "PurchaseContractDepartment" ("DepartmentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101135024_V0_12_0_HopDongNguyenTac') THEN
    CREATE UNIQUE INDEX "IX_PurchaseContractLine_ContractId_ItemId_UomId" ON "PurchaseContractLine" ("ContractId", "ItemId", "UomId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101135024_V0_12_0_HopDongNguyenTac') THEN
    CREATE INDEX "IX_PurchaseContractLine_ItemId" ON "PurchaseContractLine" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101135024_V0_12_0_HopDongNguyenTac') THEN
    CREATE INDEX "IX_PurchaseContractLine_UomId" ON "PurchaseContractLine" ("UomId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101135024_V0_12_0_HopDongNguyenTac') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261101135024_V0_12_0_HopDongNguyenTac', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101142419_V0_12_0_PhuLucHopDong') THEN
    CREATE TABLE "PurchaseContractAmendment" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "ContractId" uuid NOT NULL,
        "Seq" integer NOT NULL,
        "No" character varying(60) NOT NULL,
        "PaperNo" character varying(100),
        "Reason" character varying(500) NOT NULL,
        "SignDate" date,
        "OldValidTo" date NOT NULL,
        "NewValidTo" date NOT NULL,
        "OldPriceListId" uuid NOT NULL,
        "NewPriceListId" uuid NOT NULL,
        "PriceAdjustmentTerms" character varying(2000),
        "PaymentTerms" character varying(2000),
        "CreditLimit" numeric(19,2),
        "DeliveryTerms" character varying(2000),
        "DiscountTerms" character varying(2000),
        "SponsorshipTerms" character varying(2000),
        "PenaltyTerms" character varying(2000),
        "OtherTerms" character varying(4000),
        "Note" character varying(1000),
        "ExpectedAmount" numeric(19,2) NOT NULL,
        "ChangeSummary" character varying(1000) NOT NULL,
        "Status" integer NOT NULL,
        "ApprovalRound" integer NOT NULL,
        "RejectReason" character varying(500),
        "ApprovedAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_PurchaseContractAmendment" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PurchaseContractAmendment_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseContractAmendment_PurchaseContract_ContractId" FOREIGN KEY ("ContractId") REFERENCES "PurchaseContract" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseContractAmendment_SupplierPriceList_NewPriceListId" FOREIGN KEY ("NewPriceListId") REFERENCES "SupplierPriceList" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_PurchaseContractAmendment_SupplierPriceList_OldPriceListId" FOREIGN KEY ("OldPriceListId") REFERENCES "SupplierPriceList" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101142419_V0_12_0_PhuLucHopDong') THEN
    CREATE INDEX "IX_PurchaseContractAmendment_CompanyId" ON "PurchaseContractAmendment" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101142419_V0_12_0_PhuLucHopDong') THEN
    CREATE UNIQUE INDEX "IX_PurchaseContractAmendment_ContractId_Seq" ON "PurchaseContractAmendment" ("ContractId", "Seq");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101142419_V0_12_0_PhuLucHopDong') THEN
    CREATE INDEX "IX_PurchaseContractAmendment_NewPriceListId" ON "PurchaseContractAmendment" ("NewPriceListId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101142419_V0_12_0_PhuLucHopDong') THEN
    CREATE INDEX "IX_PurchaseContractAmendment_OldPriceListId" ON "PurchaseContractAmendment" ("OldPriceListId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101142419_V0_12_0_PhuLucHopDong') THEN
    CREATE UNIQUE INDEX "UX_PurchaseContractAmendment_Open" ON "PurchaseContractAmendment" ("ContractId") WHERE "Status" IN (1, 2);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101142419_V0_12_0_PhuLucHopDong') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261101142419_V0_12_0_PhuLucHopDong', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101151104_V0_12_0_KeKhaiXungDotLoiIch') THEN
    CREATE TABLE "ConflictDeclaration" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "SupplierId" uuid NOT NULL,
        "Relation" integer NOT NULL,
        "Description" character varying(500) NOT NULL,
        "ValidFrom" date NOT NULL,
        "ValidTo" date,
        "EndReason" character varying(500),
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_ConflictDeclaration" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ConflictDeclaration_Supplier_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ConflictDeclaration_User_UserId" FOREIGN KEY ("UserId") REFERENCES "User" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101151104_V0_12_0_KeKhaiXungDotLoiIch') THEN
    CREATE INDEX "IX_ConflictDeclaration_SupplierId" ON "ConflictDeclaration" ("SupplierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101151104_V0_12_0_KeKhaiXungDotLoiIch') THEN
    CREATE INDEX "IX_ConflictDeclaration_UserId_SupplierId" ON "ConflictDeclaration" ("UserId", "SupplierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261101151104_V0_12_0_KeKhaiXungDotLoiIch') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261101151104_V0_12_0_KeKhaiXungDotLoiIch', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102003023_V0_12_0_NccDocQuyen') THEN
    CREATE TABLE "SoleSourceApproval" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "No" character varying(50) NOT NULL,
        "SupplierId" uuid NOT NULL,
        "ItemId" uuid NOT NULL,
        "Reason" character varying(1000) NOT NULL,
        "ValidFrom" date NOT NULL,
        "ReviewDate" date NOT NULL,
        "Status" integer NOT NULL,
        "ApprovalRound" integer NOT NULL,
        "RejectReason" character varying(500),
        "ApprovedAt" timestamp with time zone,
        "EndedOn" date,
        "EndReason" character varying(1000),
        "RenewedFromId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CreatedBy" uuid,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" uuid,
        CONSTRAINT "PK_SoleSourceApproval" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_SoleSourceApproval_Review" CHECK ("ReviewDate" > "ValidFrom"),
        CONSTRAINT "FK_SoleSourceApproval_Company_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Company" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_SoleSourceApproval_Item_ItemId" FOREIGN KEY ("ItemId") REFERENCES "Item" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_SoleSourceApproval_SoleSourceApproval_RenewedFromId" FOREIGN KEY ("RenewedFromId") REFERENCES "SoleSourceApproval" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_SoleSourceApproval_Supplier_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Supplier" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102003023_V0_12_0_NccDocQuyen') THEN
    CREATE UNIQUE INDEX "IX_SoleSourceApproval_CompanyId_No" ON "SoleSourceApproval" ("CompanyId", "No");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102003023_V0_12_0_NccDocQuyen') THEN
    CREATE INDEX "IX_SoleSourceApproval_CompanyId_SupplierId_ItemId" ON "SoleSourceApproval" ("CompanyId", "SupplierId", "ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102003023_V0_12_0_NccDocQuyen') THEN
    CREATE INDEX "IX_SoleSourceApproval_ItemId" ON "SoleSourceApproval" ("ItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102003023_V0_12_0_NccDocQuyen') THEN
    CREATE INDEX "IX_SoleSourceApproval_RenewedFromId" ON "SoleSourceApproval" ("RenewedFromId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102003023_V0_12_0_NccDocQuyen') THEN
    CREATE INDEX "IX_SoleSourceApproval_SupplierId" ON "SoleSourceApproval" ("SupplierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102003023_V0_12_0_NccDocQuyen') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261102003023_V0_12_0_NccDocQuyen', '10.0.14');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102011909_V0_12_0_NguonGiaPo') THEN
    ALTER TABLE "PurchaseOrderLine" ADD "ApprovedDiscountPct" numeric(9,4);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102011909_V0_12_0_NguonGiaPo') THEN
    ALTER TABLE "PurchaseOrderLine" ADD "ApprovedUnitPrice" numeric(19,6);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102011909_V0_12_0_NguonGiaPo') THEN
    ALTER TABLE "PurchaseOrderLine" ADD "ContractId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102011909_V0_12_0_NguonGiaPo') THEN
    ALTER TABLE "PurchaseOrderLine" ADD "PriceDeviationReason" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102011909_V0_12_0_NguonGiaPo') THEN
    ALTER TABLE "PurchaseOrderLine" ADD "PriceListId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102011909_V0_12_0_NguonGiaPo') THEN
    ALTER TABLE "PurchaseOrderLine" ADD "PriceSource" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102011909_V0_12_0_NguonGiaPo') THEN
    ALTER TABLE "PurchaseOrderLine" ADD "SourceRfqLineId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102011909_V0_12_0_NguonGiaPo') THEN
    ALTER TABLE "PurchaseOrder" ADD "HasPriceException" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102011909_V0_12_0_NguonGiaPo') THEN
    ALTER TABLE "PurchaseOrder" ADD "IsOutOfContract" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102011909_V0_12_0_NguonGiaPo') THEN
    CREATE INDEX "IX_PurchaseOrderLine_ContractId" ON "PurchaseOrderLine" ("ContractId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102011909_V0_12_0_NguonGiaPo') THEN
    CREATE INDEX "IX_PurchaseOrderLine_PriceListId" ON "PurchaseOrderLine" ("PriceListId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102011909_V0_12_0_NguonGiaPo') THEN
    CREATE INDEX "IX_PurchaseOrderLine_SourceRfqLineId" ON "PurchaseOrderLine" ("SourceRfqLineId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102011909_V0_12_0_NguonGiaPo') THEN
    ALTER TABLE "PurchaseOrderLine" ADD CONSTRAINT "FK_PurchaseOrderLine_PurchaseContract_ContractId" FOREIGN KEY ("ContractId") REFERENCES "PurchaseContract" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102011909_V0_12_0_NguonGiaPo') THEN
    ALTER TABLE "PurchaseOrderLine" ADD CONSTRAINT "FK_PurchaseOrderLine_RfqLine_SourceRfqLineId" FOREIGN KEY ("SourceRfqLineId") REFERENCES "RfqLine" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102011909_V0_12_0_NguonGiaPo') THEN
    ALTER TABLE "PurchaseOrderLine" ADD CONSTRAINT "FK_PurchaseOrderLine_SupplierPriceList_PriceListId" FOREIGN KEY ("PriceListId") REFERENCES "SupplierPriceList" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261102011909_V0_12_0_NguonGiaPo') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261102011909_V0_12_0_NguonGiaPo', '10.0.14');
    END IF;
END $EF$;
COMMIT;

