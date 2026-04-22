import { unwrapJsonResultEnvelope } from '@openstaff/api';

export type McpSourceDto = {
  sourceKey?: string;
  displayName?: string;
};

export type McpInstallChannelDto = {
  channelId?: string;
  channelType?: string;
  transportType?: string;
  version?: null | string;
  entrypointHint?: null | string;
  packageIdentifier?: null | string;
  artifactUrl?: null | string;
  metadata?: Record<string, string>;
};

export type McpCatalogEntryDto = {
  entryId?: string;
  sourceKey?: string;
  name?: string;
  displayName?: string;
  description?: null | string;
  category?: null | string;
  version?: null | string;
  homepage?: null | string;
  repositoryUrl?: null | string;
  transportTypes?: string[];
  installChannels?: McpInstallChannelDto[];
  isInstalled?: boolean;
  installedState?: null | string;
  installedVersion?: null | string;
  installId?: null | string;
};

export type McpCatalogSearchQueryDto = {
  sourceKey?: string;
  keyword?: string;
  category?: string;
  transportType?: string;
  cursor?: string;
  page?: number;
  pageSize?: number;
};

export type McpCatalogSearchResultDto = {
  items?: McpCatalogEntryDto[];
  totalCount?: number;
  nextCursor?: null | string;
};

export type McpLaunchProfileView = {
  id?: string;
  displayName?: null | string;
  profileType?: string;
  transportType?: string;
  runnerKind?: null | string;
  runner?: null | string;
  ecosystem?: null | string;
  packageName?: null | string;
  packageVersion?: null | string;
  image?: null | string;
  imageTagTemplate?: null | string;
  command?: null | string;
  commandTemplate?: null | string;
  workingDirectoryTemplate?: null | string;
  urlTemplate?: null | string;
  argsTemplate?: string[];
  envTemplate?: Record<string, null | string>;
  headersTemplate?: Record<string, null | string>;
};

export type McpParameterSchemaItemView = {
  key?: string;
  label?: null | string;
  type?: string;
  required?: boolean;
  defaultValue?: unknown;
  defaultValueSource?: null | string;
  projectOverrideValueSource?: null | string;
  description?: null | string;
  appliesToProfiles?: string[];
};

export type McpServerView = {
  id?: string;
  name?: string;
  description?: null | string;
  icon?: null | string;
  logo?: null | string;
  category?: string;
  transportType?: string;
  mode?: string;
  source?: string;
  templateJson?: null | string;
  installId?: null | string;
  catalogEntryId?: null | string;
  installSourceKey?: null | string;
  installChannelId?: null | string;
  installChannelType?: null | string;
  installedVersion?: null | string;
  installedState?: null | string;
  installDirectory?: null | string;
  manifestPath?: null | string;
  lastInstallError?: null | string;
  isManagedInstall?: boolean;
  homepage?: null | string;
  npmPackage?: null | string;
  pypiPackage?: null | string;
  isEnabled?: boolean;
  configCount?: number;
  defaultProfileId?: null | string;
  profiles?: McpLaunchProfileView[];
  parameterSchema?: McpParameterSchemaItemView[];
};

export type McpServerConfigView = {
  id?: string;
  mcpServerId?: string;
  mcpServerName?: string;
  name?: string;
  description?: null | string;
  transportType?: string;
  selectedProfileId?: null | string;
  parameterValues?: null | string;
  hasEnvironmentVariables?: boolean;
  hasAuthConfig?: boolean;
  isEnabled?: boolean;
  createdAt?: string;
};

export type InstallMcpServerInput = {
  sourceKey: string;
  catalogEntryId: string;
  selectedChannelId?: string;
  requestedVersion?: string;
  name?: string;
  overwriteExisting?: boolean;
};

export type DeleteMcpServerResultDto = {
  serverId?: string;
  installId?: null | string;
  deleted?: boolean;
  uninstalled?: boolean;
  action?: string;
  message?: null | string;
  blockingReasons?: string[];
  referencedByConfigs?: string[];
  referencedByProjectBindings?: string[];
  referencedByRoleBindings?: string[];
};

export type McpUninstallCheckResultDto = {
  canUninstall?: boolean;
  blockingReasons?: string[];
  referencedByConfigs?: string[];
  referencedByProjectBindings?: string[];
  referencedByRoleBindings?: string[];
};

export type McpRepairResultDto = {
  repaired?: boolean;
  message?: null | string;
  server?: McpServerView;
};

export type TestMcpConnectionDraftInput = {
  mcpServerId: string;
  selectedProfileId?: string;
  parameterValues?: string;
};

export type TestMcpConnectionResult = {
  success?: boolean;
  message?: null | string;
  tools?: Array<{
    name?: string;
    description?: null | string;
    inputSchema?: null | string;
  }>;
};

export type CreateMcpBindingDraftInput = {
  mcpServerId: string;
  scope: string;
  projectAgentRoleId?: string;
  agentRoleId?: string;
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

function buildQuery(params: Record<string, boolean | number | string | undefined>) {
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

export function getMcpSources() {
  return request<McpSourceDto[]>('/api/mcp/sources');
}

export function searchMcpCatalog(query: McpCatalogSearchQueryDto) {
  return request<McpCatalogSearchResultDto>(
    `/api/mcp/catalog/search${buildQuery({
      sourceKey: query.sourceKey,
      keyword: query.keyword,
      category: query.category,
      transportType: query.transportType,
      cursor: query.cursor,
      page: query.page,
      pageSize: query.pageSize,
    })}`,
  );
}

export function installMcpServer(input: InstallMcpServerInput) {
  return request<McpServerView>('/api/mcp/install', {
    method: 'POST',
    body: JSON.stringify(input),
  });
}

export function getMcpServers(filters: {
  source?: string;
  category?: string;
  search?: string;
  enabledState?: boolean;
  installedState?: string;
}) {
  return request<McpServerView[]>(
    `/api/mcp/servers${buildQuery(filters)}`,
  );
}

export function createMcpServer(body: Record<string, unknown>) {
  return request<McpServerView>('/api/mcp/servers', {
    method: 'POST',
    body: JSON.stringify(body),
  });
}

export function updateMcpServer(id: string, body: Record<string, unknown>) {
  return request<McpServerView>(`/api/mcp/servers/${id}`, {
    method: 'PUT',
    body: JSON.stringify(body),
  });
}

export function checkMcpUninstall(id: string) {
  return request<McpUninstallCheckResultDto>(`/api/mcp/servers/${id}/uninstall-check`);
}

export function deleteMcpServer(id: string) {
  return request<DeleteMcpServerResultDto>(`/api/mcp/servers/${id}`, {
    method: 'DELETE',
  });
}

export function repairMcpInstall(id: string) {
  return request<McpRepairResultDto>(`/api/mcp/servers/${id}/repair`, {
    method: 'POST',
  });
}

export function getMcpConfigs() {
  return request<McpServerConfigView[]>('/api/mcp/configs');
}

export function createMcpConfig(body: Record<string, unknown>) {
  return request<McpServerConfigView>('/api/mcp/configs', {
    method: 'POST',
    body: JSON.stringify(body),
  });
}

export function updateMcpConfig(id: string, body: Record<string, unknown>) {
  return request<McpServerConfigView>(`/api/mcp/configs/${id}`, {
    method: 'PUT',
    body: JSON.stringify(body),
  });
}

export function deleteMcpConfig(id: string) {
  return request<void>(`/api/mcp/configs/${id}`, {
    method: 'DELETE',
  });
}

export function testSavedMcpConfig(id: string) {
  return request<TestMcpConnectionResult>(`/api/mcp/configs/${id}/test`, {
    method: 'POST',
  });
}

export function testDraftMcpConfig(body: TestMcpConnectionDraftInput) {
  return request<TestMcpConnectionResult>('/api/mcp/configs/test-draft', {
    method: 'POST',
    body: JSON.stringify(body),
  });
}

export function getAgentRoleBindings(agentRoleId: string) {
  return request<any[]>(`/api/mcp/agent-bindings/${agentRoleId}`);
}

export function replaceAgentRoleBindings(agentRoleId: string, bindings: unknown[]) {
  return request<void>(`/api/mcp/agent-bindings/${agentRoleId}`, {
    method: 'PUT',
    body: JSON.stringify(bindings),
  });
}

export function getProjectAgentBindings(projectAgentId: string) {
  return request<any[]>(`/api/mcp/project-agent-bindings/${projectAgentId}`);
}

export function replaceProjectAgentBindings(projectAgentId: string, bindings: unknown[]) {
  return request<void>(`/api/mcp/project-agent-bindings/${projectAgentId}`, {
    method: 'PUT',
    body: JSON.stringify(bindings),
  });
}

export function createMcpBindingDraft(body: CreateMcpBindingDraftInput) {
  return request<{
    mcpServerId?: string;
    toolFilter?: null | string;
    selectedProfileId?: null | string;
    parameterValues?: null | string;
    isEnabled?: boolean;
  }>('/api/mcp/binding-draft', {
    method: 'POST',
    body: JSON.stringify(body),
  });
}
