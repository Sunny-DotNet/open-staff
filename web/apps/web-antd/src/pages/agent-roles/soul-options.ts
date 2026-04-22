import type { AgentSoulCatalogDto, AgentSoulDto, AgentSoulOptionDto } from '@openstaff/api';
import { getApiAgentSoulsOptions, unwrapClientEnvelope } from '@openstaff/api';

export interface SoulFormValue {
  attitudes: string[];
  custom: string;
  style: string;
  traits: string[];
}

export type { AgentSoulCatalogDto, AgentSoulOptionDto };

export function createEmptySoulForm(): SoulFormValue {
  return {
    attitudes: [],
    custom: '',
    style: '',
    traits: [],
  };
}

export function soulDtoToFormValue(soul?: AgentSoulDto | null): SoulFormValue {
  return {
    attitudes: [...(soul?.attitudes ?? [])],
    custom: soul?.custom ?? '',
    style: soul?.style ?? '',
    traits: [...(soul?.traits ?? [])],
  };
}

export function buildSoulPayloadFromForm(value: SoulFormValue): AgentSoulDto | undefined {
  const normalized = {
    attitudes: value.attitudes.map((item) => item.trim()).filter(Boolean),
    custom: value.custom.trim() || undefined,
    style: value.style.trim() || undefined,
    traits: value.traits.map((item) => item.trim()).filter(Boolean),
  };

  return normalized.attitudes.length ||
    normalized.custom ||
    normalized.style ||
    normalized.traits.length
    ? normalized
    : undefined;
}

export async function loadSoulOptions(locale: string): Promise<AgentSoulCatalogDto> {
  return unwrapClientEnvelope(
    await getApiAgentSoulsOptions({
      query: { locale },
    }),
  );
}

export function normalizeSoulFormValue(
  value: SoulFormValue,
  catalog?: AgentSoulCatalogDto | null,
): SoulFormValue {
  return {
    attitudes: normalizeSoulValues(value.attitudes, catalog?.attitudes),
    custom: value.custom,
    style: normalizeSoulValue(value.style, catalog?.styles),
    traits: normalizeSoulValues(value.traits, catalog?.traits),
  };
}

export function isSoulFormValueEqual(a: SoulFormValue, b: SoulFormValue) {
  return (
    a.custom === b.custom &&
    a.style === b.style &&
    a.traits.length === b.traits.length &&
    a.traits.every((item, index) => item === b.traits[index]) &&
    a.attitudes.length === b.attitudes.length &&
    a.attitudes.every((item, index) => item === b.attitudes[index])
  );
}

export function formatSoulDisplayValue(
  value: null | string | undefined,
  options?: AgentSoulOptionDto[] | null,
) {
  if (!value) {
    return '';
  }

  const normalized = value.trim();
  if (!normalized) {
    return '';
  }

  const matched = findSoulOption(normalized, options);
  return matched?.label ?? normalized;
}

export function formatSoulDisplayValues(
  values: Array<string> | null | undefined,
  options?: AgentSoulOptionDto[] | null,
) {
  return (values ?? [])
    .map((value) => formatSoulDisplayValue(value, options))
    .filter(Boolean);
}

export function withSelectedSoulOptions(
  options: AgentSoulOptionDto[] | null | undefined,
  selectedValues: Array<string> | null | undefined,
) {
  const result = [...(options ?? [])];
  const existingValues = new Set(result.map((item) => item.key ?? '').filter(Boolean));

  for (const selectedValue of selectedValues ?? []) {
    const normalized = selectedValue.trim();
    if (!normalized || existingValues.has(normalized)) {
      continue;
    }

    result.push({
      key: normalized,
      label: formatSoulDisplayValue(normalized, options),
    });
    existingValues.add(normalized);
  }

  return result;
}

function normalizeSoulValues(
  values: Array<string> | null | undefined,
  options?: AgentSoulOptionDto[] | null,
) {
  const results: string[] = [];
  for (const value of values ?? []) {
    const normalized = normalizeSoulValue(value, options);
    if (!normalized || results.includes(normalized)) {
      continue;
    }
    results.push(normalized);
  }
  return results;
}

function normalizeSoulValue(
  value: null | string | undefined,
  options?: AgentSoulOptionDto[] | null,
) {
  if (!value) {
    return '';
  }

  const normalized = value.trim();
  if (!normalized) {
    return '';
  }

  return findSoulOption(normalized, options)?.key ?? normalized;
}

function findSoulOption(
  value: string,
  options?: AgentSoulOptionDto[] | null,
) {
  return (options ?? []).find(
    (option) =>
      equalsSoulValue(option.key, value) ||
      equalsSoulValue(option.label, value),
  );
}

function equalsSoulValue(a: null | string | undefined, b: string) {
  return !!a && a.trim().localeCompare(b, undefined, { sensitivity: 'accent' }) === 0;
}
