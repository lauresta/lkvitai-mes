-- Master data reference seed
-- Safe to run multiple times (idempotent)

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
