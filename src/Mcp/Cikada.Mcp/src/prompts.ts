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
        '4. Only then: order_create, order_product_add + order_product_update for each product (always pass the latest revision), order_address_set if I gave an address.',
        '5. Finish with order_entry_get and show the saved order. Do not confirm it for production unless I ask (order_confirm).',
      ].join('\n'),
  },
];

export const promptsFor = (orderEntry: boolean): readonly PromptDef[] => PROMPTS.filter((p) => orderEntry || !p.orderEntry);
