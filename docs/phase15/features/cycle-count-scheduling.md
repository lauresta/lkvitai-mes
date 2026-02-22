# Cycle Count Scheduling (Sprint 7)

## API
- `POST /api/warehouse/v1/cycle-counts/schedule`
- `GET /api/warehouse/v1/cycle-counts`
- `GET /api/warehouse/v1/cycle-counts/{id}`

## Scheduling Request
- `scheduledDate` (`>= today`)
- `abcClass` (`A`, `B`, `C`, `ALL`)
- `assignedOperator` (required)
- `locationIds` (optional explicit location selection)

## Behavior
- Filters items by ABC class prefix on category code (`A*`, `B*`, `C*`) or all active items.
- Validates active physical locations and uses provided location ids when supplied.
- Generates count number format: `CC-yyyyMMdd-###`.
- Creates cycle count lines with system quantity from available-stock resolver.
