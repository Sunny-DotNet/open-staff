import { requestClient } from '#/api/request';

export namespace AgentApi {
  export interface AgentRole {
    id: string;
    name: string;
    roleType: string;
    description: string | null;
    systemPrompt: string | null;
    modelProviderId: string | null;
    modelName: string | null;
    modelProviderName: string | null;
    isBuiltin: boolean;
    config: string | null;
    createdAt: string;
    updatedAt: string;
  }

  export interface AgentRoleConfig {
    modelParameters?: {
      maxTokens?: number;
      temperature?: number;
    };
    routing?: {
      defaultNext?: string;
      markers?: Record<string, string>;
    };
    tools?: string[];
  }

  export interface UpdateAgentRoleParams {
    config?: string;
    description?: string;
    modelName?: string;
    modelProviderId?: string;
    name?: string;
    systemPrompt?: string;
  }

  export interface TestChatMessage {
    content: string;
    errors?: string[];
    model: string;
    provider: string;
    success: boolean;
  }

  // Legacy interfaces for project agent instances
  export interface Agent {
    id: string;
    name: string;
    role: string;
    status: 'idle' | 'thinking' | 'working';
    avatar?: string;
  }

  export interface Message {
    id: string;
    agentId: string;
    agentName: string;
    content: string;
    type: 'chat' | 'code' | 'error' | 'system';
    timestamp: string;
  }

  export interface SendMessageParams {
    content: string;
    type?: string;
  }

  export interface AgentEvent {
    id: string;
    type: string;
    data: Record<string, unknown>;
    timestamp: string;
  }
}

// ===== 代理体角色配置 API =====

/** 获取所有代理体角色 */
export async function getAgentRolesApi(): Promise<AgentApi.AgentRole[]> {
  const resp = await requestClient.get('/agent-roles');
  return (resp as any)?.data ?? resp;
}

/** 获取单个代理体角色 */
export async function getAgentRoleApi(
  id: string,
): Promise<AgentApi.AgentRole> {
  const resp = await requestClient.get(`/agent-roles/${id}`);
  return (resp as any)?.data ?? resp;
}

/** 更新代理体角色 */
export async function updateAgentRoleApi(
  id: string,
  data: AgentApi.UpdateAgentRoleParams,
): Promise<AgentApi.AgentRole> {
  const resp = await requestClient.put(`/agent-roles/${id}`, data);
  return (resp as any)?.data ?? resp;
}

/** 对话测试 */
export async function testChatApi(
  roleId: string,
  message: string,
): Promise<AgentApi.TestChatMessage> {
  const resp = await requestClient.post(`/agent-roles/${roleId}/test-chat`, {
    message,
  });
  return (resp as any)?.data ?? resp;
}

// ===== 项目智能体实例 API =====

/** 获取项目的智能体列表 */
export async function getAgentsApi(projectId: string) {
  const resp = await requestClient.get(`/projects/${projectId}/agents`);
  return (resp as any)?.data ?? resp;
}

/** 向智能体发送消息 */
export async function sendAgentMessageApi(
  projectId: string,
  agentId: string,
  data: AgentApi.SendMessageParams,
) {
  const resp = await requestClient.post(
    `/projects/${projectId}/agents/${agentId}/message`,
    data,
  );
  return (resp as any)?.data ?? resp;
}

/** 获取智能体事件流 */
export async function getAgentEventsApi(
  projectId: string,
  agentId: string,
) {
  const resp = await requestClient.get(
    `/projects/${projectId}/agents/${agentId}/events`,
  );
  return (resp as any)?.data ?? resp;
}

/** 获取项目消息历史 */
export async function getMessagesApi(projectId: string) {
  const resp = await requestClient.get(`/projects/${projectId}/messages`);
  return (resp as any)?.data ?? resp;
}
