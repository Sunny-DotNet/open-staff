import { unwrapJsonResultEnvelope } from '@openstaff/api';

export type TalentMarketSourceDto = {
  displayName?: string;
  sourceKey?: string;
};

export type TalentMarketRoleSummaryDto = {
  avatar?: null | string;
  canOverwrite?: boolean;
  description?: null | string;
  file?: null | string;
  isActive?: boolean;
  isBuiltin?: boolean;
  isHired?: boolean;
  job?: null | string;
  jobTitle?: null | string;
  matchedRoleId?: null | string;
  matchedRoleName?: null | string;
  mcpCount?: number;
  modelName?: null | string;
  name?: string;
  skillCount?: number;
  source?: null | string;
  sourceKey?: string;
  templateId?: string;
};

export type TalentMarketSearchResultDto = {
  items?: TalentMarketRoleSummaryDto[];
  totalCount?: number;
};

export type TalentMarketMcpRequirementDto = {
  configCount?: number;
  key?: null | string;
  matchStrategy?: null | string;
  matchedServerId?: null | string;
  matchedServerName?: null | string;
  matchedServerSource?: null | string;
  message?: null | string;
  name?: null | string;
  npmPackage?: null | string;
  pypiPackage?: null | string;
  status?: string;
};

export type TalentMarketSkillRequirementDto = {
  displayName?: null | string;
  installKey?: null | string;
  key?: null | string;
  matchStrategy?: null | string;
  message?: null | string;
  owner?: null | string;
  repo?: null | string;
  skillId?: string;
  source?: null | string;
  sourceKey?: null | string;
  status?: string;
};

export type TalentMarketRolePreviewDto = {
  avatar?: null | string;
  description?: null | string;
  externalId?: null | string;
  jobTitle?: null | string;
  modelConfig?: null | string;
  modelName?: null | string;
  name?: string;
  soul?: Record<string, unknown> | null;
};

export type TalentMarketHirePreviewDto = {
  canOverwrite?: boolean;
  matchedRoleId?: null | string;
  matchedRoleName?: null | string;
  overwriteBlockedReason?: null | string;
  preview?: {
    mcps?: TalentMarketMcpRequirementDto[];
    role?: TalentMarketRolePreviewDto;
    skills?: TalentMarketSkillRequirementDto[];
  };
  requiresOverwriteConfirmation?: boolean;
  sourceKey?: string;
  template?: TalentMarketRoleSummaryDto;
};

export type TalentMarketHireResultDto = {
  addedMcpBindings?: number;
  addedSkillBindings?: number;
  preview?: TalentMarketHirePreviewDto['preview'];
  role?: {
    id?: string;
    name?: string;
  };
};

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers);
  const locale = document.documentElement.lang || navigator.language;
  if (locale) {
    headers.set('Accept-Language', locale);
  }

  if (init?.body && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json');
  }

  const response = await fetch(path, {
    ...init,
    headers,
  });

  const contentType = response.headers.get('content-type') ?? '';
  const body = contentType.includes('json') ? await response.json() : undefined;

  if (!response.ok) {
    throw new Error(extractErrorMessage(body) || `Request failed (${response.status})`);
  }

  return body === undefined ? (undefined as T) : unwrapJsonResultEnvelope(body) as T;
}

function extractErrorMessage(value: unknown) {
  if (!value || typeof value !== 'object') {
    return undefined;
  }

  const candidate = value as {
    data?: { error?: string; message?: string };
    error?: string;
    message?: string;
  };

  return (
    candidate.message ??
    candidate.error ??
    candidate.data?.message ??
    candidate.data?.error
  );
}

function buildQuery(params: Record<string, number | string | undefined>) {
  const search = new URLSearchParams();
  Object.entries(params).forEach(([key, value]) => {
    if (value === undefined || value === null || value === '') {
      return;
    }

    search.set(key, String(value));
  });

  const query = search.toString();
  return query ? `?${query}` : '';
}

export function getTalentMarketSources() {
  return request<TalentMarketSourceDto[]>('/api/talent-market/sources');
}

export function searchTalentMarket(query: {
  keyword?: string;
  page?: number;
  pageSize?: number;
  sourceKey?: string;
}) {
  return request<TalentMarketSearchResultDto>(
    `/api/talent-market/search${buildQuery({
      keyword: query.keyword,
      page: query.page,
      pageSize: query.pageSize,
      sourceKey: query.sourceKey,
    })}`,
  );
}

export function previewTalentMarketHire(input: {
  sourceKey: string;
  templateId: string;
}) {
  return request<TalentMarketHirePreviewDto>('/api/talent-market/preview-hire', {
    method: 'POST',
    body: JSON.stringify(input),
  });
}

export function hireTalentMarketRole(input: {
  overwriteExisting?: boolean;
  sourceKey: string;
  templateId: string;
}) {
  return request<TalentMarketHireResultDto>('/api/talent-market/hire', {
    method: 'POST',
    body: JSON.stringify(input),
  });
}
