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

/** 创建会话（异步启动，返回 sessionId） */
export async function createSessionApi(
  params: AgentApi.CreateSessionParams,
): Promise<AgentApi.SessionInfo> {
  const resp = await requestClient.post('/sessions', params);
  return (resp as any)?.data ?? resp;
}

/** 订阅会话事件流（SSE） */
export function subscribeSessionStream(
  sessionId: string,
  onEvent: (event: AgentApi.SessionEvent) => void,
  onDone?: () => void,
  onError?: (error: Event) => void,
): EventSource {
  const baseUrl =
    (requestClient as any).defaults?.baseURL ?? '/api';
  const es = new EventSource(`${baseUrl}/sessions/${sessionId}/stream`);

  // 监听所有已知事件类型
  const eventTypes = [
    'session_created',
    'session_completed',
    'session_cancelled',
    'session_error',
    'frame_pushed',
    'frame_completed',
    'frame_popped',
    'thought',
    'decision',
    'message',
    'action',
    'tool_call',
    'tool_result',
    'error',
    'routing',
    'user_input',
  ];

  for (const type of eventTypes) {
    es.addEventListener(type, (e: MessageEvent) => {
      try {
        const data = JSON.parse(e.data);
        onEvent(data);
      } catch {
        // ignore parse errors
      }
    });
  }

  es.addEventListener('done', () => {
    es.close();
    onDone?.();
  });

  es.onerror = (e) => {
    onError?.(e);
    es.close();
  };

  return es;
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
