# UI Verify Steps

- `/warehouse/stock/adjustments`
  - Select item, location, qty delta, and reason code.
  - Submit adjustment and confirm success toast.
  - Confirm history table refreshes with new row and timestamp.

- `/warehouse/putaway`
  - Open task list and confirm RECEIVING rows are visible.
  - Select destination location for a row and execute putaway.
  - Confirm success toast and row disappears or quantities refresh.

- `/warehouse/picking/tasks`
  - Create a task with order id, item, qty.
  - Load location suggestions for created task id.
  - Complete task with source location id and picked qty; confirm success.

- `/warehouse/labels`
  - Load templates and queue.
  - Submit preview and confirm PDF download.
  - Submit print, then retry a queue item if present.
  - Use file-name download to retrieve generated PDF artifact.
