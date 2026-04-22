import { unwrapJsonResultEnvelope } from '@openstaff/api';

export type SkillCatalogSourceDto = {
  displayName?: string;
  source?: string;
  sourceKey?: string;
};

export type SkillCatalogItemDto = {
  description?: null | string;
  displayName?: string;
  githubUrl?: null | string;
  installs?: number;
  isInstalled?: boolean;
  name?: string;
  owner?: string;
  repo?: string;
  skillId?: string;
  source?: string;
  sourceKey?: string;
};

export type SkillCatalogPageDto = {
  items?: SkillCatalogItemDto[];
  page?: number;
  pageSize?: number;
  scrapedAt?: null | string;
  total?: number;
  totalPages?: number;
};

export type InstalledSkillDto = {
  createdAt?: string;
  description?: null | string;
  displayName?: string;
  githubUrl?: null | string;
  id?: string;
  installKey?: string;
  installRootPath?: string;
  name?: string;
  owner?: string;
  repo?: string;
  skillId?: string;
  source?: string;
  sourceKey?: string;
  status?: string;
  updatedAt?: string;
};

export type AgentRoleSkillBindingDto = {
  id?: string;
  agentRoleId?: string;
  skillInstallKey?: string;
  skillId?: string;
  name?: string;
  displayName?: string;
  source?: string;
  owner?: string;
  repo?: string;
  githubUrl?: null | string;
  isEnabled?: boolean;
  resolutionStatus?: string;
  resolutionMessage?: null | string;
  installRootPath?: null | string;
  createdAt?: string;
  updatedAt?: string;
};

export type AgentRoleSkillBindingInput = {
  skillInstallKey: string;
  skillId: string;
  name: string;
  displayName: string;
  source: string;
  owner: string;
  repo: string;
  githubUrl?: null | string;
  isEnabled?: boolean;
};

export type InstallSkillInput = {
  owner: string;
  overwriteExisting?: boolean;
  repo: string;
  skillId: string;
  sourceKey?: string;
};

export type UninstallSkillInput = {
  owner: string;
  repo: string;
  skillId: string;
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

export function getSkillSources() {
  return request<SkillCatalogSourceDto[]>('/api/skills/sources');
}

export function searchSkillCatalog(query: {
  owner?: string;
  page?: number;
  pageSize?: number;
  query?: string;
  repo?: string;
}) {
  return request<SkillCatalogPageDto>(
    `/api/skills/catalog${buildQuery({
      owner: query.owner,
      page: query.page,
      pageSize: query.pageSize,
      query: query.query,
      repo: query.repo,
    })}`,
  );
}

export function getSkillCatalogItem(owner: string, repo: string, skillId: string) {
  return request<null | SkillCatalogItemDto>(`/api/skills/catalog/${owner}/${repo}/${skillId}`);
}

export function getInstalledSkills(query?: string) {
  return request<InstalledSkillDto[]>(`/api/skills/installed${buildQuery({ query })}`);
}

export function installSkill(input: InstallSkillInput) {
  return request<InstalledSkillDto>('/api/skills/install', {
    method: 'POST',
    body: JSON.stringify(input),
  });
}

export function uninstallSkill(input: UninstallSkillInput) {
  return request<boolean>('/api/skills/uninstall', {
    method: 'POST',
    body: JSON.stringify(input),
  });
}

export function getAgentRoleSkillBindings(agentRoleId: string) {
  return request<AgentRoleSkillBindingDto[]>(`/api/skills/agent-bindings/${agentRoleId}`);
}

export function replaceAgentRoleSkillBindings(
  agentRoleId: string,
  bindings: AgentRoleSkillBindingInput[],
) {
  return request<void>(`/api/skills/agent-bindings/${agentRoleId}`, {
    method: 'PUT',
    body: JSON.stringify(bindings),
  });
}
