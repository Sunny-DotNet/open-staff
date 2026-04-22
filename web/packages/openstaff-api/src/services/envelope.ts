export interface JsonResultEnvelope<T = unknown> {
  success: boolean;
  data: T;
  message?: null | string;
}

export function isJsonResultEnvelope<T>(
  value: JsonResultEnvelope<T> | T,
): value is JsonResultEnvelope<T> {
  return (
    !!value &&
    typeof value === 'object' &&
    'success' in value &&
    'data' in value &&
    'message' in value
  );
}

export function unwrapJsonResultEnvelope<T>(
  value: JsonResultEnvelope<T> | T,
): T {
  return isJsonResultEnvelope(value) ? value.data : value;
}

export function unwrapClientEnvelope<T>(value: {
  data?: JsonResultEnvelope<T> | T;
}) {
  if (value.data === undefined) {
    throw new Error('API response did not contain a data payload.');
  }

  return unwrapJsonResultEnvelope(value.data);
}

export function unwrapCollection<T>(value: Array<T> | { items?: Array<T> | null }) {
  return Array.isArray(value) ? value : (value.items ?? []);
}
