export function normalizeOptionalJson(value?: null | string) {
  const trimmed = value?.trim() ?? '';
  return trimmed ? trimmed : null;
}
