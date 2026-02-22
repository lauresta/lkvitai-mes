# Production-Ready Warehouse Tasks - Master Index Part 2

**Continuation of Master Index**

---

### Epic M: Cycle Counting (PRD-0600 to PRD-0615) - 2 weeks

| TaskId | Title | Est | Dependencies | OwnerType | Phase |
|--------|-------|-----|--------------|-----------|-------|
| PRD-0600 | CycleCount Entity & Schema | M | PRD-0001 | Backend/API | 2 |
| PRD-0601 | Cycle Count Scheduling Logic (ABC) | M | PRD-0600 | Backend/API | 2 |
| PRD-0602 | Count Execution Command | M | PRD-0600 | Backend/API | 2 |
| PRD-0603 | Discrepancy Detection Logic | M | PRD-0602 | Backend/API | 2 |
| PRD-0604 | Auto-Adjustment Workflow | M | PRD-0603 | Backend/API | 2 |
| PRD-0605 | Cycle Count API Endpoints | M | PRD-0602, PRD-0604 | Backend/API | 2 |
| PRD-0606 | Cycle Count UI - Schedule Page | M | PRD-0605 | UI | 2 |
| PRD-0607 | Cycle Count UI - Execution Page | M | PRD-0605 | UI | 2 |
| PRD-0608 | Cycle Count UI - Discrepancy Report | M | PRD-0605 | UI | 2 |
| PRD-0609 | Cycle Count Reports (Accuracy, Variance) | M | PRD-0605 | UI | 2 |
| PRD-0610 | Cycle Count Security & RBAC | S | PRD-0005 | Backend/API | 2 |
| PRD-0611 | Cycle Count Integration Tests | M | PRD-0605 | QA | 2 |
| PRD-0612 | Cycle Count Migration & Seed Data | S | PRD-0600 | Infra/DevOps | 2 |
| PRD-0613 | Cycle Count Observability | S | PRD-0009 | Infra/DevOps | 2 |
| PRD-0614 | Cycle Count Documentation | S | PRD-0611 | QA | 2 |
| PRD-0615 | Cycle Count Notification (Assignments) | S | PRD-0601 | Integration | 2 |

### Epic N: Returns/RMA (PRD-0700 to PRD-0715) - 2 weeks

| TaskId | Title | Est | Dependencies | OwnerType | Phase |
|--------|-------|-----|--------------|-----------|-------|
| PRD-0700 | RMA Entity & Schema | M | PRD-0401 | Backend/API | 2 |
| PRD-0701 | RMA State Machine | M | PRD-0700 | Backend/API | 2 |
| PRD-0702 | Create RMA Command | M | PRD-0701 | Backend/API | 2 |
| PRD-0703 | Receive Return Command | M | PRD-0701 | Backend/API | 2 |
| PRD-0704 | Inspect Return Command | M | PRD-0701 | Backend/API | 2 |
| PRD-0705 | Disposition Logic (Restock/Scrap/Vendor) | M | PRD-0704 | Backend/API | 2 |
| PRD-0706 | RMA Events (Created, Received, Inspected) | M | PRD-0702, PRD-0704 | Backend/API | 2 |
| PRD-0707 | RMA API Endpoints | M | PRD-0702, PRD-0704 | Backend/API | 2 |
| PRD-0708 | RMA UI - List & Create | M | PRD-0707 | UI | 2 |
| PRD-0709 | RMA UI - Receive Return Page | M | PRD-0707 | UI | 2 |
| PRD-0710 | RMA UI - Inspection Page | M | PRD-0707 | UI | 2 |
| PRD-0711 | RMA Reports (Summary, Pending Inspection) | M | PRD-0707 | UI | 2 |
| PRD-0712 | RMA Security & RBAC | S | PRD-0005 | Backend/API | 2 |
| PRD-0713 | RMA Integration Tests | M | PRD-0707 | QA | 2 |
| PRD-0714 | RMA Migration & Seed Data | S | PRD-0700 | Infra/DevOps | 2 |
| PRD-0715 | RMA Documentation | S | PRD-0713 | QA | 2 |

### Epic G: Label Printing (PRD-0800 to PRD-0810) - 1 week

| TaskId | Title | Est | Dependencies | OwnerType | Phase |
|--------|-------|-----|--------------|-----------|-------|
| PRD-0800 | ZPL Template Engine | M | None | Backend/API | 2 |
| PRD-0801 | TCP 9100 Printer Integration | M | PRD-0800 | Backend/API | 2 |
| PRD-0802 | Print Queue Implementation | M | PRD-0801 | Backend/API | 2 |
| PRD-0803 | Label Templates (Location, HU, Item) | M | PRD-0800 | Backend/API | 2 |
| PRD-0804 | Print Job Retry Logic | S | PRD-0802 | Backend/API | 2 |
| PRD-0805 | PDF Fallback Generation | M | PRD-0800 | Backend/API | 2 |
| PRD-0806 | Label Printing API Endpoints | M | PRD-0802 | Backend/API | 2 |
| PRD-0807 | Label Printing UI - Preview & Print | M | PRD-0806 | UI | 2 |
| PRD-0808 | Label Printing Tests | M | PRD-0806 | QA | 2 |
| PRD-0809 | Label Printing Observability | S | PRD-0009 | Infra/DevOps | 2 |
| PRD-0810 | Label Printing Documentation | S | PRD-0808 | QA | 2 |

### Epic F: Inter-Warehouse Transfers (PRD-0900 to PRD-0910) - 1 week

| TaskId | Title | Est | Dependencies | OwnerType | Phase |
|--------|-------|-----|--------------|-----------|-------|
| PRD-0900 | Transfer Entity & Schema | M | PRD-0001 | Backend/API | 2 |
| PRD-0901 | Transfer State Machine | M | PRD-0900 | Backend/API | 2 |
| PRD-0902 | Create Transfer Command | M | PRD-0901 | Backend/API | 2 |
| PRD-0903 | Approve Transfer Command | M | PRD-0901 | Backend/API | 2 |
| PRD-0904 | Execute Transfer Command | M | PRD-0901 | Backend/API | 2 |
| PRD-0905 | Transfer API Endpoints | M | PRD-0902, PRD-0904 | Backend/API | 2 |
| PRD-0906 | Transfer UI - Request & Approval | M | PRD-0905 | UI | 2 |
| PRD-0907 | Transfer Reports | S | PRD-0905 | UI | 2 |
| PRD-0908 | Transfer Integration Tests | M | PRD-0905 | QA | 2 |
| PRD-0909 | Transfer Migration & Seed Data | S | PRD-0900 | Infra/DevOps | 2 |
| PRD-0910 | Transfer Documentation | S | PRD-0908 | QA | 2 |

### Epic O: Advanced Reporting (PRD-1000 to PRD-1015) - 2 weeks

| TaskId | Title | Est | Dependencies | OwnerType | Phase |
|--------|-------|-----|--------------|-----------|-------|
| PRD-1000 | Transaction Log Export API | M | None | Backend/API | 2 |
| PRD-1001 | Lot Traceability Report (Upstream) | M | PRD-1000 | Backend/API | 2 |
| PRD-1002 | Lot Traceability Report (Downstream) | M | PRD-1000 | Backend/API | 2 |
| PRD-1003 | Variance Analysis Report | M | PRD-1000 | Backend/API | 2 |
| PRD-1004 | Compliance Report (FDA 21 CFR Part 11) | L | PRD-1000 | Backend/API | 2 |
| PRD-1005 | Audit Trail Export (All Events) | M | PRD-1000 | Backend/API | 2 |
| PRD-1006 | Report Scheduling Engine | M | PRD-1000 | Backend/API | 2 |
| PRD-1007 | Report API Endpoints | M | PRD-1001, PRD-1003 | Backend/API | 2 |
| PRD-1008 | Reporting UI - Traceability | M | PRD-1007 | UI | 2 |
| PRD-1009 | Reporting UI - Variance Analysis | M | PRD-1007 | UI | 2 |
| PRD-1010 | Reporting UI - Compliance Reports | M | PRD-1007 | UI | 2 |
| PRD-1011 | Report Export Formats (CSV, PDF, Excel) | M | PRD-1007 | Backend/API | 2 |
| PRD-1012 | Reporting Security & RBAC | S | PRD-0005 | Backend/API | 2 |
| PRD-1013 | Reporting Integration Tests | M | PRD-1007 | QA | 2 |
| PRD-1014 | Reporting Observability | S | PRD-0009 | Infra/DevOps | 2 |
| PRD-1015 | Reporting Documentation | S | PRD-1013 | QA | 2 |

### Epic H: Wave Picking (PRD-1100 to PRD-1115) - 3 weeks

| TaskId | Title | Est | Dependencies | OwnerType | Phase |
|--------|-------|-----|--------------|-----------|-------|
| PRD-1100 | Wave Entity & Schema | M | PRD-0401 | Backend/API | 3 |
| PRD-1101 | Wave Creation Logic (Grouping) | L | PRD-1100 | Backend/API | 3 |
| PRD-1102 | Operator Assignment | M | PRD-1100 | Backend/API | 3 |
| PRD-1103 | Batch Pick List Generation | M | PRD-1101 | Backend/API | 3 |
| PRD-1104 | Route Optimization Algorithm | L | PRD-1103 | Backend/API | 3 |
| PRD-1105 | Post-Pick Sorting Logic | M | PRD-1103 | Backend/API | 3 |
| PRD-1106 | Wave API Endpoints | M | PRD-1101, PRD-1103 | Backend/API | 3 |
| PRD-1107 | Wave UI - Creation & Assignment | M | PRD-1106 | UI | 3 |
| PRD-1108 | Wave UI - Batch Pick Execution | L | PRD-1106 | UI | 3 |
| PRD-1109 | Wave UI - Post-Pick Sorting | M | PRD-1106 | UI | 3 |
| PRD-1110 | Wave Reports (Efficiency, Throughput) | M | PRD-1106 | UI | 3 |
| PRD-1111 | Wave Security & RBAC | S | PRD-0005 | Backend/API | 3 |
| PRD-1112 | Wave Integration Tests | L | PRD-1106 | QA | 3 |
| PRD-1113 | Wave Migration & Seed Data | S | PRD-1100 | Infra/DevOps | 3 |
| PRD-1114 | Wave Observability | M | PRD-0009 | Infra/DevOps | 3 |
| PRD-1115 | Wave Documentation | S | PRD-1112 | QA | 3 |

### Epic I: Cross-Docking (PRD-1200 to PRD-1210) - 2 weeks

| TaskId | Title | Est | Dependencies | OwnerType | Phase |
|--------|-------|-----|--------------|-----------|-------|
| PRD-1200 | Cross-Dock Flag on InboundShipment | S | None | Backend/API | 3 |
| PRD-1201 | Cross-Dock Routing Logic | M | PRD-1200 | Backend/API | 3 |
| PRD-1202 | Match Inbound to Outbound Orders | M | PRD-1201 | Backend/API | 3 |
| PRD-1203 | Cross-Dock Workflow (Receive → Ship) | M | PRD-1201 | Backend/API | 3 |
| PRD-1204 | Cross-Dock API Endpoints | M | PRD-1203 | Backend/API | 3 |
| PRD-1205 | Cross-Dock UI - Configuration | M | PRD-1204 | UI | 3 |
| PRD-1206 | Cross-Dock UI - Execution Dashboard | M | PRD-1204 | UI | 3 |
| PRD-1207 | Cross-Dock Reports (Throughput) | M | PRD-1204 | UI | 3 |
| PRD-1208 | Cross-Dock Integration Tests | M | PRD-1204 | QA | 3 |
| PRD-1209 | Cross-Dock Observability | S | PRD-0009 | Infra/DevOps | 3 |
| PRD-1210 | Cross-Dock Documentation | S | PRD-1208 | QA | 3 |

### Epic J: Multi-Level QC (PRD-1300 to PRD-1315) - 2 weeks

| TaskId | Title | Est | Dependencies | OwnerType | Phase |
|--------|-------|-----|--------------|-----------|-------|
| PRD-1300 | QC Checklist Template Model | M | None | Backend/API | 3 |
| PRD-1301 | QC Approval Workflow (3 Levels) | M | PRD-1300 | Backend/API | 3 |
| PRD-1302 | Defect Taxonomy Configuration | M | PRD-1300 | Backend/API | 3 |
| PRD-1303 | Photo/Document Attachment Storage | M | PRD-1300 | Backend/API | 3 |
| PRD-1304 | QC Escalation Logic | M | PRD-1301 | Backend/API | 3 |
| PRD-1305 | QC API Endpoints | M | PRD-1301, PRD-1303 | Backend/API | 3 |
| PRD-1306 | QC UI - Checklist Execution | M | PRD-1305 | UI | 3 |
| PRD-1307 | QC UI - Approval Dashboard | M | PRD-1305 | UI | 3 |
| PRD-1308 | QC UI - Defect Categorization | M | PRD-1305 | UI | 3 |
| PRD-1309 | QC UI - Photo Upload | M | PRD-1305 | UI | 3 |
| PRD-1310 | QC Reports (Defect Analysis) | M | PRD-1305 | UI | 3 |
| PRD-1311 | QC Security & RBAC | S | PRD-0005 | Backend/API | 3 |
| PRD-1312 | QC Integration Tests | M | PRD-1305 | QA | 3 |
| PRD-1313 | QC Migration & Seed Data | S | PRD-1300 | Infra/DevOps | 3 |
| PRD-1314 | QC Observability | S | PRD-0009 | Infra/DevOps | 3 |
| PRD-1315 | QC Documentation | S | PRD-1312 | QA | 3 |

### Epic K: HU Hierarchy (PRD-1400 to PRD-1415) - 2 weeks

| TaskId | Title | Est | Dependencies | OwnerType | Phase |
|--------|-------|-----|--------------|-----------|-------|
| PRD-1400 | HandlingUnit.ParentHUId Schema | S | None | Backend/API | 3 |
| PRD-1401 | HU Hierarchy Validation Logic | M | PRD-1400 | Backend/API | 3 |
| PRD-1402 | Split HU Operation | M | PRD-1400 | Backend/API | 3 |
| PRD-1403 | Merge HU Operation | M | PRD-1400 | Backend/API | 3 |
| PRD-1404 | HU Tree Query (Parent/Children) | M | PRD-1400 | Backend/API | 3 |
| PRD-1405 | HU Hierarchy API Endpoints | M | PRD-1402, PRD-1403 | Backend/API | 3 |
| PRD-1406 | HU Hierarchy UI - Tree View | M | PRD-1405 | UI | 3 |
| PRD-1407 | HU Hierarchy UI - Split Operation | M | PRD-1405 | UI | 3 |
| PRD-1408 | HU Hierarchy UI - Merge Operation | M | PRD-1405 | UI | 3 |
| PRD-1409 | HU Hierarchy Reports | M | PRD-1405 | UI | 3 |
| PRD-1410 | HU Hierarchy Security & RBAC | S | PRD-0005 | Backend/API | 3 |
| PRD-1411 | HU Hierarchy Integration Tests | M | PRD-1405 | QA | 3 |
| PRD-1412 | HU Hierarchy Migration | S | PRD-1400 | Infra/DevOps | 3 |
| PRD-1413 | HU Hierarchy Observability | S | PRD-0009 | Infra/DevOps | 3 |
| PRD-1414 | HU Hierarchy Documentation | S | PRD-1411 | QA | 3 |
| PRD-1415 | HU Hierarchy Projection Updates | M | PRD-1400 | Projections | 3 |

### Epic L: Serial Tracking (PRD-1500 to PRD-1520) - 3 weeks

| TaskId | Title | Est | Dependencies | OwnerType | Phase |
|--------|-------|-----|--------------|-----------|-------|
| PRD-1500 | SerialNumber Entity & Schema | M | None | Backend/API | 3 |
| PRD-1501 | Serial Lifecycle State Machine | M | PRD-1500 | Backend/API | 3 |
| PRD-1502 | Serial → Lot Mapping | M | PRD-1500 | Backend/API | 3 |
| PRD-1503 | Serial Activation on Receipt | M | PRD-1501 | Backend/API | 3 |
| PRD-1504 | Serial Tracking in Picking | M | PRD-1501 | Backend/API | 3 |
| PRD-1505 | Serial Warranty Tracking | M | PRD-1501 | Backend/API | 3 |
| PRD-1506 | Serial Recall Management | M | PRD-1501 | Backend/API | 3 |
| PRD-1507 | Serial API Endpoints | M | PRD-1503, PRD-1504 | Backend/API | 3 |
| PRD-1508 | Serial UI - Registration | M | PRD-1507 | UI | 3 |
| PRD-1509 | Serial UI - Lifecycle Tracking | M | PRD-1507 | UI | 3 |
| PRD-1510 | Serial UI - Warranty Management | M | PRD-1507 | UI | 3 |
| PRD-1511 | Serial UI - Recall Dashboard | M | PRD-1507 | UI | 3 |
| PRD-1512 | Serial Reports (Lifecycle, Warranty) | M | PRD-1507 | UI | 3 |
| PRD-1513 | Serial Security & RBAC | S | PRD-0005 | Backend/API | 3 |
| PRD-1514 | Serial Integration Tests | L | PRD-1507 | QA | 3 |
| PRD-1515 | Serial Migration & Seed Data | S | PRD-1500 | Infra/DevOps | 3 |
| PRD-1516 | Serial Observability | M | PRD-0009 | Infra/DevOps | 3 |
| PRD-1517 | Serial Documentation | S | PRD-1514 | QA | 3 |
| PRD-1518 | Serial Projection (SerialInventory) | M | PRD-1500 | Projections | 3 |
| PRD-1519 | Serial Barcode Generation | M | PRD-1500 | Backend/API | 3 |
| PRD-1520 | Serial Import/Export | M | PRD-1507 | Backend/API | 3 |

### Epic P: Admin Config (PRD-1600 to PRD-1610) - 2 weeks

| TaskId | Title | Est | Dependencies | OwnerType | Phase |
|--------|-------|-----|--------------|-----------|-------|
| PRD-1600 | Warehouse Settings Model | M | None | Backend/API | 4 |
| PRD-1601 | Threshold Configuration (Capacity, etc) | M | PRD-1600 | Backend/API | 4 |
| PRD-1602 | Picking Strategy Configuration (FEFO/FIFO) | M | PRD-1600 | Backend/API | 4 |
| PRD-1603 | Approval Rules Configuration | M | PRD-1600 | Backend/API | 4 |
| PRD-1604 | Reason Code Management | M | PRD-1600 | Backend/API | 4 |
| PRD-1605 | User Role Management UI | M | PRD-1600 | UI | 4 |
| PRD-1606 | Admin Config API Endpoints | M | PRD-1601, PRD-1603 | Backend/API | 4 |
| PRD-1607 | Admin Config UI - Settings Page | M | PRD-1606 | UI | 4 |
| PRD-1608 | Admin Config Integration Tests | M | PRD-1606 | QA | 4 |
| PRD-1609 | Admin Config Migration | S | PRD-1600 | Infra/DevOps | 4 |
| PRD-1610 | Admin Config Documentation | S | PRD-1608 | QA | 4 |

### Epic Q: Security Hardening (PRD-1700 to PRD-1720) - 3 weeks

| TaskId | Title | Est | Dependencies | OwnerType | Phase |
|--------|-------|-----|--------------|-----------|-------|
| PRD-1700 | SSO Integration (Azure AD/Okta) | L | None | Backend/API | 4 |
| PRD-1701 | OAuth 2.0 Implementation | M | PRD-1700 | Backend/API | 4 |
| PRD-1702 | MFA (TOTP/SMS) | M | PRD-1700 | Backend/API | 4 |
| PRD-1703 | API Key Management System | M | None | Backend/API | 4 |
| PRD-1704 | API Key Rotation | M | PRD-1703 | Backend/API | 4 |
| PRD-1705 | API Key Scopes & Permissions | M | PRD-1703 | Backend/API | 4 |
| PRD-1706 | Granular RBAC Permissions | L | PRD-0005 | Backend/API | 4 |
| PRD-1707 | Audit Log (All User Actions) | M | None | Backend/API | 4 |
| PRD-1708 | PII Encryption at Rest | M | None | Backend/API | 4 |
| PRD-1709 | GDPR Compliance (Right to Erasure) | M | PRD-1708 | Backend/API | 4 |
| PRD-1710 | Security API Endpoints | M | PRD-1703, PRD-1707 | Backend/API | 4 |
| PRD-1711 | Security UI - SSO Configuration | M | PRD-1710 | UI | 4 |
| PRD-1712 | Security UI - API Key Management | M | PRD-1710 | UI | 4 |
| PRD-1713 | Security UI - Audit Log Viewer | M | PRD-1710 | UI | 4 |
| PRD-1714 | Security UI - User Permissions | M | PRD-1710 | UI | 4 |
| PRD-1715 | Security Integration Tests | L | PRD-1710 | QA | 4 |
| PRD-1716 | Security Penetration Testing | L | PRD-1715 | QA | 4 |
| PRD-1717 | Security Migration | M | PRD-1700 | Infra/DevOps | 4 |
| PRD-1718 | Security Observability | M | PRD-0009 | Infra/DevOps | 4 |
| PRD-1719 | Security Documentation (SOC 2, ISO 27001) | M | PRD-1715 | QA | 4 |
| PRD-1720 | Security Compliance Audit | L | PRD-1719 | QA | 4 |

---

## Summary Statistics

**Total Tasks:** 180+  
**Total Epics:** 17 (Foundation + A-Q)  
**Total Duration:** ~39 weeks (9.75 months)

**Phase Breakdown:**
- Phase 1.5 (Must-Have): 60 tasks, 14 weeks
- Phase 2 (Operational Excellence): 50 tasks, 8 weeks
- Phase 3 (Advanced Features): 50 tasks, 12 weeks
- Phase 4 (Enterprise Hardening): 30 tasks, 5 weeks

**Owner Type Distribution:**
- Backend/API: ~80 tasks (44%)
- UI: ~50 tasks (28%)
- QA: ~30 tasks (17%)
- Integration: ~15 tasks (8%)
- Infra/DevOps: ~15 tasks (8%)
- Projections: ~10 tasks (6%)

