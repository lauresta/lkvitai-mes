# Intrastat arrivals XML (IDAIS)

Writer: `Domain/Purchasing/IntrastatXmlWriter.cs`. Message: `INTRASTATReportSubmission`, trade direction `IMPORT`.

## Source of the format

- Customs filling instruction "Intrastato ataskaitos XML formatu pildymo instrukcija", v1.00, 2026-03-13:
  https://muitine.lrv.lt/public/canonical/1774861713/1959/XML%20pildymo%20instrukcija_v1.00.pdf (read 2026-11-22).
- Customs overview: https://mano.muitine.lt/en/knowledge-bank/customs-formalities-and-customs-data/trade-eu-intrastat

## What is NOT here

`INTRASTATReportSubmission.xsd` is attachment 1 of the instruction. It is not on the public customs pages
(mano.muitine.lt, muitine.lrv.lt, osp.stat.gov.lt) and the IDAIS portal (intrastatas.muitine.lt) is a login-only application,
so the schema could not be downloaded. The classifier lists (16LT, 305NOV-311NOV) are published at
`muitine.lrv.lt/lt/verslui/sarasai-ir-klasifikatoriai/` but the Intrastat classifier page was not reachable (404).

Consequences: the structure was taken from the instruction, **not validated against the XSD**; the XML namespace (if the XSD
defines one) is unknown and none is written; the 3-character supplementary unit code (classifier 311NOV) is not checked
against the list. When the XSD is available, add it here and add a validation test.

## Decisions taken from the instruction

- One file = one VAT payer, one direction (IMPORT), one month. The file has no version element; the version is managed by the
  portal upload, and Cikada numbers its own versions (table `intrastat_xml_exports`).
- `ZeroTradeIndicator` is `true` (no item list) only for a month with no declared rows, no skipped lines and no returns.
- Item order of elements: ReportItemNumber, CombinedNomenclatureCode, CountryOfDispatchOrDestinationCode, CountryOfOriginCode,
  NatureOfTransactionCode, DeliveryTermsCode, ModeOfTransportCode, NetMass, QuantityInSupplementaryUnitOfMeasure,
  SupplementaryUnitOfMeasureCode, InvoicedAmount. Not written for imports: PartnerIdentifier, RegionCode, StatisticalValue.
- NetMass and supplementary quantity: up to 3 decimals; InvoicedAmount: 2 decimals; invariant decimal point.
- Returns to suppliers (nature 21): the instruction has no rule, so they are left out of the file and shown as a note.
