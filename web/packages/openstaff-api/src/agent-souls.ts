import type { JsonResultEnvelope } from './services';
import { unwrapJsonResultEnvelope } from './services';

export interface AgentSoulOptionDto {
  key?: string;
  label?: string;
}

export interface AgentSoulCatalogDto {
  attitudes?: AgentSoulOptionDto[];
  styles?: AgentSoulOptionDto[];
  traits?: AgentSoulOptionDto[];
}

function buildUrl(locale?: string) {
  if (!locale) {
    return '/api/agent-souls/options';
  }

  const params = new URLSearchParams({ locale });
  return `/api/agent-souls/options?${params.toString()}`;
}

function getErrorMessage(value: unknown, status: number) {
  if (value && typeof value === 'object') {
    const record = value as {
      data?: { error?: string; message?: string };
      error?: string;
      message?: string;
    };

    return (
      record.message ??
      record.error ??
      record.data?.message ??
      record.data?.error ??
      `Request failed (${status})`
    );
  }

  return `Request failed (${status})`;
}

export async function getApiAgentSoulsOptions(options?: { locale?: string }) {
  const response = await fetch(buildUrl(options?.locale), {
    headers: options?.locale ? { 'Accept-Language': options.locale } : undefined,
  });

  if (!response.ok) {
    let payload: unknown;
    try {
      payload = await response.clone().json();
    } catch {
      payload = undefined;
    }

    throw new Error(getErrorMessage(payload, response.status));
  }

  const json = (await response.json()) as
    | AgentSoulCatalogDto
    | JsonResultEnvelope<AgentSoulCatalogDto>;
  return unwrapJsonResultEnvelope(json);
}
