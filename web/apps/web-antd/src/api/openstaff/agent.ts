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

  // Session-based conversation interfaces
  export interface CreateSessionParams {
    projectId: string;
    input: string;
    contextStrategy?: 'full' | 'hybrid' | 'summary';
  }

  export interface SessionInfo {
    sessionId: string;
    status: string;
    createdAt: string;
  }

  export interface SessionEvent {
    id: string;
    sessionId: string;
    frameId: string | null;
    eventType: string;
    payload: string | null;
    sequenceNo: number;
    createdAt: string;
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

/** 创建代理体角色 */
export async function createAgentRoleApi(data: {
  name: string;
  roleType: string;
  description?: string;
  systemPrompt?: string;
  modelProviderId?: string;
  modelName?: string;
  config?: string;
}): Promise<AgentApi.AgentRole> {
  const resp = await requestClient.post('/agent-roles', data);
  return (resp as any)?.data ?? resp;
}

/** 删除代理体角色（软删除） */
export async function deleteAgentRoleApi(id: string): Promise<void> {
  await requestClient.delete(`/agent-roles/${id}`);
}

/** 测试代理体对话（异步启动，返回 sessionId，通过 SignalR 订阅结果） */
export async function testAgentChatApi(
  roleId: string,
  message: string,
): Promise<{ sessionId: string }> {
  const resp = await requestClient.post(`/agent-roles/${roleId}/test-chat`, { message });
  return (resp as any)?.data ?? resp;
}

/** 创建会话（异步启动，返回 sessionId） */
export async function createSessionApi(
  params: AgentApi.CreateSessionParams,
): Promise<AgentApi.SessionInfo> {
  const resp = await requestClient.post('/sessions', params);
  return (resp as any)?.data ?? resp;
}

/** 取消会话 */
export async function cancelSessionApi(sessionId: string) {
  const resp = await requestClient.post(`/sessions/${sessionId}/cancel`);
  return (resp as any)?.data ?? resp;
}

/** Pop 当前帧 */
export async function popSessionFrameApi(sessionId: string) {
  const resp = await requestClient.post(`/sessions/${sessionId}/pop`);
  return (resp as any)?.data ?? resp;
}

/** 获取会话详情 */
export async function getSessionApi(sessionId: string) {
  const resp = await requestClient.get(`/sessions/${sessionId}`);
  return (resp as any)?.data ?? resp;
}

/** 获取项目会话列表 */
export async function getProjectSessionsApi(
  projectId: string,
  limit = 20,
) {
  const resp = await requestClient.get(
    `/sessions/by-project/${projectId}?limit=${limit}`,
  );
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

// ===== 群聊相关 API =====

/** 向已有 Session 发送消息（群聊追加） */
export async function sendSessionMessageApi(
  sessionId: string,
  input: string,
) {
  const resp = await requestClient.post(`/sessions/${sessionId}/messages`, {
    input,
  });
  return (resp as any)?.data ?? resp;
}

/** 获取群聊消息历史（分页） */
export async function getChatMessagesApi(
  sessionId: string,
  skip = 0,
  take = 50,
) {
  const resp = await requestClient.get(
    `/sessions/${sessionId}/chat-messages?skip=${skip}&take=${take}`,
  );
  return (resp as any)?.data ?? resp;
}

/** 获取会话事件列表 */
export async function getSessionEventsApi(sessionId: string) {
  const resp = await requestClient.get(`/sessions/${sessionId}/events`);
  return (resp as any)?.data ?? resp;
}
