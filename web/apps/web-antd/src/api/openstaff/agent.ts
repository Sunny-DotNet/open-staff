import { requestClient } from '#/api/request';

export namespace AgentApi {
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
    type: 'chat' | 'system' | 'code' | 'error';
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

/** 获取项目的智能体列表 */
export async function getAgentsApi(projectId: string) {
  return requestClient.get<AgentApi.Agent[]>(
    `/projects/${projectId}/agents`,
  );
}

/** 向智能体发送消息 */
export async function sendAgentMessageApi(
  projectId: string,
  agentId: string,
  data: AgentApi.SendMessageParams,
) {
  return requestClient.post<AgentApi.Message>(
    `/projects/${projectId}/agents/${agentId}/message`,
    data,
  );
}

/** 获取智能体事件流 */
export async function getAgentEventsApi(projectId: string, agentId: string) {
  return requestClient.get<AgentApi.AgentEvent[]>(
    `/projects/${projectId}/agents/${agentId}/events`,
  );
}

/** 获取项目消息历史 */
export async function getMessagesApi(projectId: string) {
  return requestClient.get<AgentApi.Message[]>(
    `/projects/${projectId}/messages`,
  );
}
