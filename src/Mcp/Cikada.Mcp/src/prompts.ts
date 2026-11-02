// Ready-made requests a client can show (ChatGPT and Claude list them as prompts / slash commands).

export interface PromptArgument {
  name: string;
  description: string;
  required: boolean;
}

export interface PromptDef {
  name: string;
  title: string;
  description: string;
  arguments: readonly PromptArgument[];
  /** Offered only with order entry on. */
  orderEntry?: boolean;
  text: (args: Readonly<Record<string, string | undefined>>) => string;
}

const given = (value: string | undefined, ask: string): string => (value?.trim() ? value.trim() : `(not given yet: ${ask})`);

export const PROMPTS: readonly PromptDef[] = [
  {
    name: 'production_status',
    title: 'Production status today',
    description: 'What is in production now, what was made in the last days, what is stopped and what is made but waits too long.',
    arguments: [{ name: 'branch', description: 'Only one branch (optional).', required: false }],
    text: (args) =>
      [
        `Give me the production status${args.branch?.trim() ? ` for ${args.branch.trim()}` : ''}.`,
        '1. production_queue with maxOrderAgeDays 180: a table state × product type (pieces), and the products waiting longest.',
        '2. report_production state "Pagamintas", type "(vnt)", granularity day, groupBy period and type, for the last 14 days: made per day (chart) and the average per working day.',
        '3. stopped_products: count and the five oldest, with the notes (no stop reason is stored).',
        '4. made_not_delivered with days 20: how many orders wait to be handed out or installed, the ten oldest.',
        'Finish with a short list of what needs attention today.',
      ].join('\n'),
  },
  {
    name: 'debt_review',
    title: 'Debt review',
    description: 'Who owes money: largest debtors, long unpaid handed-out orders, overdue by the legacy rule.',
    arguments: [],
    text: () =>
      [
        'Review the outstanding debt.',
        '1. customer_debt with onlyHandedOut true, top 15: the largest debtors (wholesale), with the oldest debt age.',
        '2. unpaid_orders with onlyLegacyOverdue true: the overdue handed-out orders, oldest first, grouped by branch with totals.',
        '3. A bar chart of overdue debt by branch.',
        'Retail sales sit on one customer per branch, so name them as the branch. Finish with the five calls to make first.',
      ].join('\n'),
  },
  {
    name: 'management_report',
    title: 'Management report (month)',
    description: 'The three management reports for a month (production, payments received, work) compared with the month before and the same month last year, with charts.',
    arguments: [{ name: 'month', description: 'The month, e.g. "2026-10" (default: the last full month).', required: false }],
    text: (args) =>
      [
        `Make the Lauresta management report for ${given(args.month, 'use the last full month')}.`,
        '1. Production: report_production with state "Pagamintas", groupBy type, for the month, the month before and the same month last year. Show made pieces per product (the "(vnt)" types only) in one table with the change in %, then a bar chart of the top 10 products.',
        '2. Payments: report_payments groupBy method and branch for the month and the month before; a table branch × method with totals, and a stacked bar chart.',
        '3. Production by day: report_production state "Pagamintas", type "(vnt)", granularity day, groupBy period, for the month; a line chart and the average per working day.',
        '4. Work: report_work groupBy duty for the month (totals only, no per-person pay unless I ask).',
        'End with three short findings (what grew, what fell, anything unusual). Numbers only from the tools; say so if a report is empty or refused.',
      ].join('\n'),
  },
  {
    name: 'daily_orders_review',
    title: 'Daily orders review',
    description: 'Review today\'s sales orders for a store: new orders, orders with debt or overdue, and what needs a call or follow-up.',
    arguments: [{ name: 'store', description: 'Store / branch, e.g. Vilnius. Empty: all stores.', required: false }],
    text: (args) =>
      [
        'Do my daily Cikada sales orders review.',
        `Store: ${args.store?.trim() || 'all stores'}.`,
        '1. Call orders_filters to get the exact status and store labels.',
        '2. Call orders_search for the store (pageSize 100) and pick out orders entered today and yesterday.',
        '3. Call orders_search with hasDebt=true for the store and list overdue orders (isOverdue) first, with customer, number, debt and date.',
        '4. For at most five orders that look unusual (very large amount, VIP, a note, overdue debt), call order_get and say in one line what needs attention.',
        'Answer with a short table per section (New, Overdue debt, Needs attention) and end with a to-do list of calls to make. Do not change anything.',
      ].join('\n'),
  },
  {
    name: 'take_new_order',
    title: 'Take a new order',
    description: 'Walk through entering a new sales order: customer, series and number, products with prices and stock, address, then a summary to confirm.',
    arguments: [
      { name: 'customer', description: 'The customer (name or code).', required: false },
      { name: 'products', description: 'What the customer wants, e.g. "2 roller blinds 1200x1500, white".', required: false },
    ],
    orderEntry: true,
    text: (args) =>
      [
        'Help me enter a new Cikada sales order.',
        `Customer: ${given(args.customer, 'ask me')}.`,
        `Products: ${given(args.products, 'ask me')}.`,
        'Rules: never invent an id, a price or a number; take ids only from tool answers, and ask me when something is ambiguous.',
        '1. Find the customer with order_entry_catalog (section customers) and the series with section series; propose the series defaults (store, manager, consultant) and ask me for the order number and the due date.',
        '2. For each product find the type (section types) and the colour (section colours, check free stock), read the type with order_entry_references kind=type, get the list price with order_entry_unit_price and preview it with order_entry_product_preview.',
        '3. Show me a summary table (type, size, quantity, colour, unit price, amount, stock warning) and the header, and wait for my explicit yes.',
        '4. Only then: order_create, order_product_add + order_product_update for each product (always pass the latest revision), order_accessory_set for accessories I asked for, order_address_set if I gave an address, order_final_price_set only if I named a final total.',
        '5. Finish with order_entry_get and show the saved order. Do not confirm it for production unless I ask (order_confirm).',
      ].join('\n'),
  },
];

export const promptsFor = (orderEntry: boolean): readonly PromptDef[] => PROMPTS.filter((p) => orderEntry || !p.orderEntry);
