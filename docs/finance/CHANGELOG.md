# Finance changes

## 2026-11-06

- Added the Finance module shell, test routes, Portal tile, and EN/LT localization.
- Added the `Bankiniai pavedimai` table, sorting with initial `NR DESC`, filters, Excel paste preview, import, guarded delete, and immediate last-import undo.
- Connected the form to the verified legacy `dbo.BankTransfersTable` through the mapped Cikada user's SQL login; kept the shared production schema unchanged.
