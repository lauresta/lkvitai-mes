# Trapezoid document: recent orders

The G05 document screen offers the 20 most recent orders with a trapezoid item in a GU production task. The list is ordered by legacy order date and order ID, newest first. For an order present in several GU tasks, its latest GU is used. The selector is available even when no production task was opened first.

`GET /api/shopfloor/trapezoid/recent-orders` reads legacy SQL under the mapped user login. It applies both `Loc_leidimas()` to the order series and the existing whole-GU visibility rule. Selecting an order reloads that GU through the existing guarded task and trapezoid item endpoints. Direct task, item, version and PDF URLs keep their own authorization checks; a selected option grants no additional access.

The selector does not create a production task or calculate a new version. It opens the existing document and its saved versions for reprinting. Orders without a GU are absent because there is no production document to reprint.
