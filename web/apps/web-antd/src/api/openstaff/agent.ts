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

  export interface TestChatResult {
    sessionId: string;
  }

  export interface ChatMessagePage {
    messages: Array<{
      id: string;
      role: string;
      agent?: string;
      agentRole?: string;
      content: string;
      text?: string;
      createdAt: string;
      timestamp?: string;
    }>;
    total: number;
  }
}

// ===== 代理体角色配置 API =====

/** 获取所有代理体角色 */
export async function getAgentRolesApi(): Promise<AgentApi.AgentRole[]> {
  return requestClient.get<AgentApi.AgentRole[]>('/agent-roles');
}

/** 获取单个代理体角色 */
export async function getAgentRoleApi(
  id: string,
): Promise<AgentApi.AgentRole> {
  return requestClient.get<AgentApi.AgentRole>(`/agent-roles/${id}`);
}

/** 更新代理体角色 */
export async function updateAgentRoleApi(
  id: string,
  data: AgentApi.UpdateAgentRoleParams,
): Promise<AgentApi.AgentRole> {
  return requestClient.put<AgentApi.AgentRole>(`/agent-roles/${id}`, data);
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
  return requestClient.post<AgentApi.AgentRole>('/agent-roles', data);
}

/** 删除代理体角色（软删除） */
export async function deleteAgentRoleApi(id: string): Promise<void> {
  await requestClient.delete(`/agent-roles/${id}`);
}

/** 测试代理体对话（异步启动，返回 sessionId，通过 SignalR 订阅结果） */
export async function testAgentChatApi(
  roleId: string,
  message: string,
): Promise<AgentApi.TestChatResult> {
  return requestClient.post<AgentApi.TestChatResult>(
    `/agent-roles/${roleId}/test-chat`,
    { message },
  );
}

/** 创建会话（异步启动，返回 sessionId） */
export async function createSessionApi(
  params: AgentApi.CreateSessionParams,
): Promise<AgentApi.SessionInfo> {
  return requestClient.post<AgentApi.SessionInfo>('/sessions', params);
}

/** 取消会话 */
export async function cancelSessionApi(sessionId: string): Promise<void> {
  await requestClient.post(`/sessions/${sessionId}/cancel`);
}

/** Pop 当前帧 */
export async function popSessionFrameApi(sessionId: string): Promise<void> {
  await requestClient.post(`/sessions/${sessionId}/pop`);
}

/** 获取会话详情 */
export async function getSessionApi(
  sessionId: string,
): Promise<AgentApi.SessionInfo> {
  return requestClient.get<AgentApi.SessionInfo>(`/sessions/${sessionId}`);
}

/** 获取项目会话列表 */
export async function getProjectSessionsApi(
  projectId: string,
  limit = 20,
): Promise<AgentApi.SessionInfo[]> {
  return requestClient.get<AgentApi.SessionInfo[]>(
    `/sessions/by-project/${projectId}?limit=${limit}`,
  );
}

// ===== 会话消息 API =====

/** 向已有 Session 发送消息（群聊追加） */
export async function sendSessionMessageApi(
  sessionId: string,
  input: string,
): Promise<void> {
  await requestClient.post(`/sessions/${sessionId}/messages`, { input });
}

/** 获取群聊消息历史（分页） */
export async function getChatMessagesApi(
  sessionId: string,
  skip = 0,
  take = 50,
): Promise<AgentApi.ChatMessagePage> {
  return requestClient.get<AgentApi.ChatMessagePage>(
    `/sessions/${sessionId}/chat-messages?skip=${skip}&take=${take}`,
  );
}

/** 获取会话事件列表 */
export async function getSessionEventsApi(
  sessionId: string,
): Promise<AgentApi.SessionEvent[]> {
  return requestClient.get<AgentApi.SessionEvent[]>(
    `/sessions/${sessionId}/events`,
  );
}
