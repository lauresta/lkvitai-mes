-- Master data reference seed
-- Safe to run multiple times (idempotent)
-- Run as one atomic operation
BEGIN;

INSERT INTO public.unit_of_measures ("Code", "Name", "Type") VALUES
('KG', 'Kilogram', 'Weight'),
('G', 'Gram', 'Weight'),
('L', 'Liter', 'Volume'),
('ML', 'Milliliter', 'Volume'),
('PCS', 'Pieces', 'Piece'),
('M', 'Meter', 'Length'),
('BOX', 'Box', 'Piece'),
('PKG', 'Package', 'Piece')
ON CONFLICT ("Code") DO UPDATE
SET "Name" = EXCLUDED."Name",
    "Type" = EXCLUDED."Type";

INSERT INTO public.handling_unit_types ("Code", "Name") VALUES
('PALLET', 'Pallet'),
('BOX', 'Box'),
('BAG', 'Bag')
ON CONFLICT ("Code") DO UPDATE
SET "Name" = EXCLUDED."Name";

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'adjustment_reason_codes'
          AND lower(column_name) = 'active'
    ) THEN
        INSERT INTO public.adjustment_reason_codes
        ("Code", "Name", "Description", "ParentId", "Category", "Active", "UsageCount", "CreatedAt", "CreatedBy")
        VALUES
        ('DAMAGE', 'Damage', NULL, NULL, 'ADJUSTMENT', TRUE, 0, NOW(), 'seed'),
        ('THEFT', 'Theft', NULL, NULL, 'ADJUSTMENT', TRUE, 0, NOW(), 'seed'),
        ('EVAPORATION', 'Evaporation', NULL, NULL, 'ADJUSTMENT', TRUE, 0, NOW(), 'seed'),
        ('INVENTORY', 'Inventory Correction', NULL, NULL, 'ADJUSTMENT', TRUE, 0, NOW(), 'seed'),
        ('SYSTEM_ERROR', 'System Error', NULL, NULL, 'ADJUSTMENT', TRUE, 0, NOW(), 'seed'),
        ('EXPIRED', 'Expired', NULL, NULL, 'ADJUSTMENT', TRUE, 0, NOW(), 'seed'),
        ('QC_REJECTED', 'QC Rejected', NULL, NULL, 'ADJUSTMENT', TRUE, 0, NOW(), 'seed'),
        ('PRODUCTION_SCRAP', 'Production Scrap', NULL, NULL, 'ADJUSTMENT', TRUE, 0, NOW(), 'seed')
        ON CONFLICT ("Code") DO UPDATE
        SET "Name" = EXCLUDED."Name",
            "Description" = EXCLUDED."Description",
            "Category" = EXCLUDED."Category",
            "Active" = EXCLUDED."Active";
    ELSIF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'adjustment_reason_codes'
          AND lower(column_name) = 'isactive'
    ) THEN
        INSERT INTO public.adjustment_reason_codes ("Code", "Name", "IsActive") VALUES
        ('DAMAGE', 'Damage', TRUE),
        ('THEFT', 'Theft', TRUE),
        ('EVAPORATION', 'Evaporation', TRUE),
        ('INVENTORY', 'Inventory Correction', TRUE),
        ('SYSTEM_ERROR', 'System Error', TRUE),
        ('EXPIRED', 'Expired', TRUE),
        ('QC_REJECTED', 'QC Rejected', TRUE),
        ('PRODUCTION_SCRAP', 'Production Scrap', TRUE)
        ON CONFLICT ("Code") DO UPDATE
        SET "Name" = EXCLUDED."Name",
            "IsActive" = EXCLUDED."IsActive";
    ELSE
        RAISE EXCEPTION 'adjustment_reason_codes table has neither Active nor IsActive column';
    END IF;
END $$;

INSERT INTO public.item_categories ("Code", "Name", "ParentCategoryId") VALUES
('RAW', 'Raw Materials', NULL),
('FINISHED', 'Finished Goods', NULL)
ON CONFLICT ("Code") DO UPDATE
SET "Name" = EXCLUDED."Name";

INSERT INTO public.item_categories ("Code", "Name", "ParentCategoryId")
SELECT 'FASTENERS', 'Fasteners', parent."Id"
FROM public.item_categories parent
WHERE parent."Code" = 'RAW'
ON CONFLICT ("Code") DO UPDATE
SET "Name" = EXCLUDED."Name",
    "ParentCategoryId" = EXCLUDED."ParentCategoryId";

INSERT INTO public.item_categories ("Code", "Name", "ParentCategoryId")
SELECT 'CHEMICALS', 'Chemicals', parent."Id"
FROM public.item_categories parent
WHERE parent."Code" = 'RAW'
ON CONFLICT ("Code") DO UPDATE
SET "Name" = EXCLUDED."Name",
    "ParentCategoryId" = EXCLUDED."ParentCategoryId";

INSERT INTO public.locations
("Code", "Barcode", "Type", "ParentLocationId", "IsVirtual", "MaxWeight", "MaxVolume", "Status", "ZoneType", "CreatedAt")
VALUES
('RECEIVING', 'VIRTUAL-RCV', 'Zone', NULL, TRUE, NULL, NULL, 'Active', NULL, NOW()),
('QC_HOLD', 'VIRTUAL-QC', 'Zone', NULL, TRUE, NULL, NULL, 'Active', 'Quarantine', NOW()),
('QUARANTINE', 'VIRTUAL-QTN', 'Zone', NULL, TRUE, NULL, NULL, 'Active', 'Quarantine', NOW()),
('PRODUCTION', 'VIRTUAL-PROD', 'Zone', NULL, TRUE, NULL, NULL, 'Active', NULL, NOW()),
('SHIPPING', 'VIRTUAL-SHIP', 'Zone', NULL, TRUE, NULL, NULL, 'Active', NULL, NOW()),
('SCRAP', 'VIRTUAL-SCRAP', 'Zone', NULL, TRUE, NULL, NULL, 'Active', NULL, NOW()),
('RETURN_TO_SUPPLIER', 'VIRTUAL-RTS', 'Zone', NULL, TRUE, NULL, NULL, 'Active', NULL, NOW())
ON CONFLICT ("Code") DO UPDATE
SET "Barcode" = EXCLUDED."Barcode",
    "Type" = EXCLUDED."Type",
    "IsVirtual" = EXCLUDED."IsVirtual",
    "Status" = EXCLUDED."Status";

-- Physical bins for warehouse visualization (3D map)
INSERT INTO public.locations
("Code", "Barcode", "Type", "ParentLocationId", "IsVirtual", "MaxWeight", "MaxVolume", "Status", "ZoneType",
 "CoordinateX", "CoordinateY", "CoordinateZ", "WidthMeters", "LengthMeters", "HeightMeters",
 "Aisle", "Rack", "Level", "Bin", "CapacityWeight", "CapacityVolume", "CreatedAt")
VALUES
('R3-C6-L1-B1', 'BIN-R3-C6-L1-B1', 'Bin', NULL, FALSE, 1200, 2.200, 'Active', 'General', 3, 6, 1, 1.20, 1.00, 1.10, 'R3', 'C6', 'L1', 'B1', 1200, 2.200, NOW()),
('R3-C6-L1-B2', 'BIN-R3-C6-L1-B2', 'Bin', NULL, FALSE, 1200, 2.200, 'Active', 'General', 4, 6, 1, 1.20, 1.00, 1.10, 'R3', 'C6', 'L1', 'B2', 1200, 2.200, NOW()),
('R3-C6-L2-B1', 'BIN-R3-C6-L2-B1', 'Bin', NULL, FALSE, 1200, 2.200, 'Active', 'General', 3, 6, 2, 1.20, 1.00, 1.10, 'R3', 'C6', 'L2', 'B1', 1200, 2.200, NOW()),
('R3-C7-L1-B1', 'BIN-R3-C7-L1-B1', 'Bin', NULL, FALSE, 800, 1.600, 'Active', 'General', 3, 7, 1, 1.00, 0.90, 1.00, 'R3', 'C7', 'L1', 'B1', 800, 1.600, NOW()),
('R4-C1-L1-B1', 'BIN-R4-C1-L1-B1', 'Bin', NULL, FALSE, 600, 1.200, 'Active', 'General', 4, 1, 1, 1.00, 0.80, 0.90, 'R4', 'C1', 'L1', 'B1', 600, 1.200, NOW())
ON CONFLICT ("Code") DO UPDATE
SET "Barcode" = EXCLUDED."Barcode",
    "Type" = EXCLUDED."Type",
    "IsVirtual" = EXCLUDED."IsVirtual",
    "Status" = EXCLUDED."Status",
    "ZoneType" = EXCLUDED."ZoneType",
    "CoordinateX" = EXCLUDED."CoordinateX",
    "CoordinateY" = EXCLUDED."CoordinateY",
    "CoordinateZ" = EXCLUDED."CoordinateZ",
    "WidthMeters" = EXCLUDED."WidthMeters",
    "LengthMeters" = EXCLUDED."LengthMeters",
    "HeightMeters" = EXCLUDED."HeightMeters",
    "Aisle" = EXCLUDED."Aisle",
    "Rack" = EXCLUDED."Rack",
    "Level" = EXCLUDED."Level",
    "Bin" = EXCLUDED."Bin",
    "CapacityWeight" = EXCLUDED."CapacityWeight",
    "CapacityVolume" = EXCLUDED."CapacityVolume";

-- Demo items for handling unit content
INSERT INTO public.items
("InternalSKU", "Name", "Description", "CategoryId", "BaseUoM", "Weight", "Volume", "RequiresLotTracking", "RequiresQC", "Status", "PrimaryBarcode", "ProductConfigId", "CreatedAt")
SELECT src."InternalSKU",
       src."Name",
       src."Description",
       cat."Id" AS "CategoryId",
       src."BaseUoM",
       src."Weight",
       src."Volume",
       src."RequiresLotTracking",
       src."RequiresQC",
       src."Status",
       src."PrimaryBarcode",
       src."ProductConfigId",
       NOW()
FROM (
    VALUES
    ('RM-BOLT-M8', 'Bolt M8', 'M8 hex bolt', 'FASTENERS', 'PCS', 0.020, 0.0010, FALSE, FALSE, 'Active', 'SKU-RM-BOLT-M8', 'CFG-RM-BOLT-M8'),
    ('RM-NUT-M8', 'Nut M8', 'M8 nut', 'FASTENERS', 'PCS', 0.010, 0.0010, FALSE, FALSE, 'Active', 'SKU-RM-NUT-M8', 'CFG-RM-NUT-M8'),
    ('RM-PAINT-BLUE', 'Industrial Paint Blue', 'Blue paint 20L can', 'CHEMICALS', 'PCS', 24.000, 0.0200, TRUE, TRUE, 'Active', 'SKU-RM-PAINT-BLUE', 'CFG-RM-PAINT-BLUE')
) AS src("InternalSKU", "Name", "Description", "CategoryCode", "BaseUoM", "Weight", "Volume", "RequiresLotTracking", "RequiresQC", "Status", "PrimaryBarcode", "ProductConfigId")
INNER JOIN public.item_categories cat ON cat."Code" = src."CategoryCode"
ON CONFLICT ("InternalSKU") DO UPDATE
SET "Name" = EXCLUDED."Name",
    "Description" = EXCLUDED."Description",
    "CategoryId" = EXCLUDED."CategoryId",
    "BaseUoM" = EXCLUDED."BaseUoM",
    "Weight" = EXCLUDED."Weight",
    "Volume" = EXCLUDED."Volume",
    "RequiresLotTracking" = EXCLUDED."RequiresLotTracking",
    "RequiresQC" = EXCLUDED."RequiresQC",
    "Status" = EXCLUDED."Status",
    "PrimaryBarcode" = EXCLUDED."PrimaryBarcode",
    "ProductConfigId" = EXCLUDED."ProductConfigId";

-- Demo handling units shown on warehouse map
DO $$
BEGIN
    IF to_regclass('public.handling_units') IS NULL
       OR to_regclass('public.handling_unit_lines') IS NULL THEN
        RAISE NOTICE 'Skipping HU seed: public.handling_units or public.handling_unit_lines does not exist';
    ELSE
        WITH upserted_hu AS (
            INSERT INTO public.handling_units
            ("HUId", "LPN", "Type", "Status", "Location", "CreatedAt", "SealedAt", "Version")
            VALUES
            ('4a6e2e2d-8f5f-44ba-b812-e2a26d2f1001', 'LPN-DEMO-0001', 'BOX',    'OPEN', 'R3-C6-L1-B1', NOW(), NULL, 1),
            ('4a6e2e2d-8f5f-44ba-b812-e2a26d2f1002', 'LPN-DEMO-0002', 'BOX',    'OPEN', 'R3-C6-L1-B2', NOW(), NULL, 1),
            ('4a6e2e2d-8f5f-44ba-b812-e2a26d2f1003', 'LPN-DEMO-0003', 'PALLET', 'OPEN', 'R3-C6-L2-B1', NOW(), NULL, 1),
            ('4a6e2e2d-8f5f-44ba-b812-e2a26d2f1004', 'LPN-DEMO-0004', 'BOX',    'OPEN', 'R3-C7-L1-B1', NOW(), NULL, 1)
            ON CONFLICT ("LPN") DO UPDATE
            SET "Type" = EXCLUDED."Type",
                "Status" = EXCLUDED."Status",
                "Location" = EXCLUDED."Location",
                "Version" = EXCLUDED."Version"
            RETURNING "HUId", "LPN"
        ),
        cleared_lines AS (
            DELETE FROM public.handling_unit_lines hul
            USING upserted_hu hu
            WHERE hul."HUId" = hu."HUId"
            RETURNING hul."HUId"
        )
        INSERT INTO public.handling_unit_lines ("HUId", "SKU", "Quantity")
        SELECT hu."HUId", payload."SKU", payload."Quantity"
        FROM upserted_hu hu
        INNER JOIN (
            VALUES
            ('LPN-DEMO-0001', 'RM-BOLT-M8', 120.0000),
            ('LPN-DEMO-0001', 'RM-NUT-M8',  120.0000),
            ('LPN-DEMO-0002', 'RM-BOLT-M8',  80.0000),
            ('LPN-DEMO-0003', 'RM-PAINT-BLUE', 24.0000),
            ('LPN-DEMO-0004', 'RM-NUT-M8',  200.0000)
        ) AS payload("LPN", "SKU", "Quantity")
            ON payload."LPN" = hu."LPN";
    END IF;
END $$;

COMMIT;
