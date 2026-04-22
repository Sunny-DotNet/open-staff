import type { McpParameterSchemaItemView, McpServerView } from './api';

export type McpParameterValues = Record<string, boolean | number | string | null | undefined>;

export function parseParameterValues(value?: null | string): McpParameterValues {
  if (!value?.trim()) {
    return {};
  }

  try {
    const parsed = JSON.parse(value) as Record<string, unknown>;
    return Object.fromEntries(
      Object.entries(parsed).map(([key, item]) => [key, normalizeParameterValue(item)]),
    );
  } catch {
    return {};
  }
}

export function stringifyParameterValues(value: McpParameterValues) {
  const normalized = Object.fromEntries(
    Object.entries(value).filter(([, item]) => item !== undefined),
  );
  return JSON.stringify(normalized, null, 2);
}

export function resolveSelectedProfileId(server?: null | McpServerView, selectedProfileId?: null | string) {
  if (!server) {
    return selectedProfileId || '';
  }

  const profiles = server.profiles ?? [];
  if (selectedProfileId && profiles.some((profile) => profile.id === selectedProfileId)) {
    return selectedProfileId;
  }

  return server.defaultProfileId || profiles[0]?.id || '';
}

export function buildDefaultParameterValues(
  schema: McpParameterSchemaItemView[] | undefined,
  selectedProfileId?: null | string,
) {
  return Object.fromEntries(
    (schema ?? [])
      .filter((item) => appliesToProfile(item, selectedProfileId))
      .filter((item) => item.defaultValue !== undefined && item.defaultValue !== null && item.defaultValue !== '')
      .map((item) => [item.key || '', normalizeParameterValue(item.defaultValue)]),
  ) as McpParameterValues;
}

export function mergeParameterValues(
  defaults: McpParameterValues,
  current: McpParameterValues,
) {
  return {
    ...defaults,
    ...current,
  };
}

export function filterSchemaByProfile(
  schema: McpParameterSchemaItemView[] | undefined,
  selectedProfileId?: null | string,
) {
  return (schema ?? []).filter((item) => appliesToProfile(item, selectedProfileId));
}

function appliesToProfile(item: McpParameterSchemaItemView, selectedProfileId?: null | string) {
  if (!item.appliesToProfiles?.length || !selectedProfileId) {
    return true;
  }

  return item.appliesToProfiles.includes(selectedProfileId);
}

function normalizeParameterValue(value: unknown) {
  if (
    value === null
    || value === undefined
    || typeof value === 'boolean'
    || typeof value === 'number'
    || typeof value === 'string'
  ) {
    return value;
  }

  return JSON.stringify(value);
}
