// A small check of tool arguments against the subset of JSON Schema the tool table uses (object, string, integer, number, boolean, array, enum,
// minimum/maximum, minLength/maxLength, minItems/maxItems, pattern, required, additionalProperties: false). A failed check is a tool error the model
// can act on, before any request reaches an API.

export type Schema = Record<string, unknown>;

export class ArgumentError extends Error {}

function typeOk(value: unknown, type: unknown): boolean {
  const types = Array.isArray(type) ? type : [type];
  return types.some((t) => {
    switch (t) {
      case 'string':
        return typeof value === 'string';
      case 'integer':
        return typeof value === 'number' && Number.isInteger(value);
      case 'number':
        return typeof value === 'number' && Number.isFinite(value);
      case 'boolean':
        return typeof value === 'boolean';
      case 'array':
        return Array.isArray(value);
      case 'object':
        return value !== null && typeof value === 'object' && !Array.isArray(value);
      case 'null':
        return value === null;
      default:
        return true;
    }
  });
}

function check(value: unknown, schema: Schema, path: string): void {
  if (schema.type !== undefined && !typeOk(value, schema.type)) {
    throw new ArgumentError(`${path} must be ${Array.isArray(schema.type) ? schema.type.join(' or ') : String(schema.type)}`);
  }
  if (Array.isArray(schema.enum) && !schema.enum.includes(value)) {
    throw new ArgumentError(`${path} must be one of: ${schema.enum.map((v) => JSON.stringify(v)).join(', ')}`);
  }
  if (typeof value === 'number') {
    if (typeof schema.minimum === 'number' && value < schema.minimum) throw new ArgumentError(`${path} must be at least ${schema.minimum}`);
    if (typeof schema.maximum === 'number' && value > schema.maximum) throw new ArgumentError(`${path} must be at most ${schema.maximum}`);
  }
  if (typeof value === 'string') {
    if (typeof schema.minLength === 'number' && value.length < schema.minLength) throw new ArgumentError(`${path} must not be empty`);
    if (typeof schema.maxLength === 'number' && value.length > schema.maxLength) throw new ArgumentError(`${path} is longer than ${schema.maxLength} characters`);
    if (typeof schema.pattern === 'string' && !new RegExp(schema.pattern).test(value)) {
      throw new ArgumentError(`${path} has the wrong format${typeof schema.description === 'string' ? ` (${schema.description})` : ''}`);
    }
  }
  if (Array.isArray(value)) {
    if (typeof schema.minItems === 'number' && value.length < schema.minItems) throw new ArgumentError(`${path} needs at least ${schema.minItems} item(s)`);
    if (typeof schema.maxItems === 'number' && value.length > schema.maxItems) throw new ArgumentError(`${path} has more than ${schema.maxItems} items`);
    if (schema.items && typeof schema.items === 'object') value.forEach((item, index) => check(item, schema.items as Schema, `${path}[${index}]`));
  }
  if (value !== null && typeof value === 'object' && !Array.isArray(value) && schema.properties) {
    const properties = schema.properties as Record<string, Schema>;
    const object = value as Record<string, unknown>;
    for (const name of (schema.required as string[] | undefined) ?? []) {
      if (object[name] === undefined || object[name] === null) throw new ArgumentError(`${path === '$' ? '' : `${path}.`}${name} is required`);
    }
    for (const [name, child] of Object.entries(object)) {
      const childSchema = properties[name];
      if (!childSchema) {
        if (schema.additionalProperties === false) throw new ArgumentError(`unknown argument ${path === '$' ? '' : `${path}.`}${name}`);
        continue;
      }
      if (child === undefined) continue;
      check(child, childSchema, path === '$' ? name : `${path}.${name}`);
    }
  }
}

export function validateArgs(args: unknown, schema: Schema): Record<string, unknown> {
  const value = args ?? {};
  check(value, schema, '$');
  return value as Record<string, unknown>;
}
