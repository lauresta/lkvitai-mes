# Sales order editor · 2c handoff

## 2026-11-04

The order editor uses the designer's three-column 2c layout: order context on the left, product lines in the centre, receipt and payments on the right. The customer, address, employee and note-history editors open in a right drawer; the order note remains inline. Product rows retain their existing edit, accessory and removal handlers. Save, confirmation, cancellation, payments, privacy notice and interrupted-command recovery retain their existing behaviour.

The product table scrolls inside its panel. At 1440×900 and 1366×768, five product rows plus one accessory fit without scrolling the order page. The test-environment strip is 22 px high. The `N` shortcut opens the product picker when focus is outside a form control; Escape closes an open context drawer and returns focus to its opener.

Source: `zakaz.zip` → `design_handoff_sales_order_screen/README.md`, variant 2c in `Sales Product Picker Options.dc.html`. The original mock data and script are design reference only; runtime data and write rules still come from Sales.
