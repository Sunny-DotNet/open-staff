import { requestClient } from '#/api/request';

export namespace McpApi {
  export interface McpServer {
    id: string;
    name: string;
    description: string | null;
    icon: string | null;
    category: string;
    transportType: 'stdio' | 'http';
    source: 'builtin' | 'custom' | 'marketplace';
    defaultConfig: string | null;
    marketplaceUrl: string | null;
  }

  export interface McpServerConfig {
    id: string;
    mcpServerId: string;
    serverName: string;
    name: string;
    description: string | null;
    transportType: 'stdio' | 'http';
    connectionConfig: string;
    environmentVariables: string | null;
    authConfig: string | null;
    isEnabled: boolean;
    createdAt: string;
  }

  export interface CreateConfigInput {
    mcpServerId: string;
    name: string;
    description?: string;
    transportType: 'stdio' | 'http';
    connectionConfig: string;
    environmentVariables?: string;
    authConfig?: string;
  }

  export interface UpdateConfigInput {
    name?: string;
    description?: string;
    transportType?: 'stdio' | 'http';
    connectionConfig?: string;
    environmentVariables?: string;
    authConfig?: string;
    isEnabled?: boolean;
  }

  export interface TestConnectionResult {
    success: boolean;
    message: string;
    tools: McpTool[];
  }

  export interface McpTool {
    name: string;
    description: string;
    inputSchema: string;
  }

  export interface AgentMcpBinding {
    agentRoleId: string;
    mcpServerConfigId: string;
    configName: string;
    serverName: string;
    toolFilter: string | null;
  }
}

// ===== MCP 服务器 =====

/** 获取所有 MCP 服务器 */
export async function getMcpServersApi(): Promise<McpApi.McpServer[]> {
  return requestClient.get<McpApi.McpServer[]>('/mcp/servers');
}

/** 创建自定义 MCP 服务器 */
export async function createMcpServerApi(
  data: Partial<McpApi.McpServer>,
): Promise<McpApi.McpServer> {
  return requestClient.post<McpApi.McpServer>('/mcp/servers', data);
}

/** 删除 MCP 服务器 */
export async function deleteMcpServerApi(id: string): Promise<void> {
  await requestClient.delete(`/mcp/servers/${id}`);
}

// ===== MCP 配置 =====

/** 获取所有配置（可选按服务器过滤） */
export async function getMcpConfigsApi(
  serverId?: string,
): Promise<McpApi.McpServerConfig[]> {
  const query = serverId ? `?serverId=${serverId}` : '';
  return requestClient.get<McpApi.McpServerConfig[]>(`/mcp/configs${query}`);
}

/** 创建配置 */
export async function createMcpConfigApi(
  data: McpApi.CreateConfigInput,
): Promise<McpApi.McpServerConfig> {
  return requestClient.post<McpApi.McpServerConfig>('/mcp/configs', data);
}

/** 更新配置 */
export async function updateMcpConfigApi(
  id: string,
  data: McpApi.UpdateConfigInput,
): Promise<McpApi.McpServerConfig> {
  return requestClient.put<McpApi.McpServerConfig>(`/mcp/configs/${id}`, data);
}

/** 删除配置 */
export async function deleteMcpConfigApi(id: string): Promise<void> {
  await requestClient.delete(`/mcp/configs/${id}`);
}

/** 测试连接 */
export async function testMcpConnectionApi(
  configId: string,
): Promise<McpApi.TestConnectionResult> {
  return requestClient.post<McpApi.TestConnectionResult>(
    `/mcp/configs/${configId}/test`,
    undefined,
    { timeout: 60_000 },
  );
}

// ===== Agent MCP 绑定 =====

/** 获取代理的 MCP 绑定 */
export async function getAgentMcpBindingsApi(
  agentRoleId: string,
): Promise<McpApi.AgentMcpBinding[]> {
  return requestClient.get<McpApi.AgentMcpBinding[]>(
    `/mcp/agent-bindings/${agentRoleId}`,
  );
}

/** 创建绑定 */
export async function createAgentMcpBindingApi(data: {
  agentRoleId: string;
  mcpServerConfigId: string;
  toolFilter?: string;
}): Promise<McpApi.AgentMcpBinding> {
  return requestClient.post<McpApi.AgentMcpBinding>(
    '/mcp/agent-bindings',
    data,
  );
}

/** 删除绑定 */
export async function deleteAgentMcpBindingApi(
  agentRoleId: string,
  configId: string,
): Promise<void> {
  await requestClient.delete(
    `/mcp/agent-bindings/${agentRoleId}/${configId}`,
  );
}

// ===== 市场 =====

export namespace McpMarketplaceApi {
  export interface MarketplaceSource {
    sourceKey: string;
    displayName: string;
    iconUrl: string | null;
  }

  export interface MarketplaceServer {
    id: string;
    name: string;
    description: string | null;
    icon: string | null;
    category: string;
    transportTypes: string[];
    source: string;
    version: string | null;
    repositoryUrl: string | null;
    homepage: string | null;
    npmPackage: string | null;
    pypiPackage: string | null;
    defaultConfig: string | null;
    isInstalled: boolean;
  }

  export interface SearchResult {
    items: MarketplaceServer[];
    totalCount: number;
    nextCursor: string | null;
  }
}

/** 获取所有市场数据源 */
export async function getMarketplaceSourcesApi(): Promise<
  McpMarketplaceApi.MarketplaceSource[]
> {
  return requestClient.get<McpMarketplaceApi.MarketplaceSource[]>(
    '/mcp/marketplace/sources',
  );
}

/** 搜索市场 MCP Server */
export async function searchMarketplaceApi(params: {
  sourceKey?: string;
  keyword?: string;
  category?: string;
  cursor?: string;
  page?: number;
  pageSize?: number;
}): Promise<McpMarketplaceApi.SearchResult> {
  const query = new URLSearchParams();
  if (params.sourceKey) query.set('sourceKey', params.sourceKey);
  if (params.keyword) query.set('keyword', params.keyword);
  if (params.category) query.set('category', params.category);
  if (params.cursor) query.set('cursor', params.cursor);
  if (params.page) query.set('page', String(params.page));
  if (params.pageSize) query.set('pageSize', String(params.pageSize));
  return requestClient.get<McpMarketplaceApi.SearchResult>(
    `/mcp/marketplace/search?${query.toString()}`,
  );
}

/** 从外部源安装 MCP Server 到本地 */
export async function installFromMarketplaceApi(data: {
  sourceKey: string;
  serverId: string;
  name?: string;
}): Promise<McpMarketplaceApi.MarketplaceServer> {
  return requestClient.post<McpMarketplaceApi.MarketplaceServer>(
    '/mcp/marketplace/install',
    data,
  );
}
