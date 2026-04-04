import { type Ref, ref, shallowRef } from 'vue';

import * as signalR from '@microsoft/signalr';

export interface UseSignalROptions {
  /** 自动重连 */
  autoReconnect?: boolean;
}

export interface UseSignalRReturn {
  /** 连接实例 */
  connection: Ref<signalR.HubConnection | null>;
  /** 连接状态 */
  connected: Ref<boolean>;
  /** 加入项目房间 */
  joinProject: (projectId: string) => Promise<void>;
  /** 离开项目房间 */
  leaveProject: (projectId: string) => Promise<void>;
  /** 注册智能体消息回调 */
  onAgentMessage: (
    callback: (data: {
      agentId: string;
      content: string;
      agentName: string;
      type: string;
    }) => void,
  ) => void;
  /** 注册智能体思考状态回调 */
  onAgentThinking: (
    callback: (data: { agentId: string; isThinking: boolean }) => void,
  ) => void;
  /** 注册任务状态变更回调 */
  onTaskStatusChanged: (
    callback: (data: {
      taskId: string;
      status: string;
      agentId?: string;
    }) => void,
  ) => void;
  /** 启动连接 */
  start: () => Promise<void>;
  /** 停止连接 */
  stop: () => Promise<void>;
}

/**
 * SignalR 连接组合式函数
 * @param hubUrl SignalR Hub 地址
 * @param options 配置选项
 */
export function useSignalR(
  hubUrl: string,
  options: UseSignalROptions = {},
): UseSignalRReturn {
  const { autoReconnect = true } = options;

  const connection = shallowRef<signalR.HubConnection | null>(null);
  const connected = ref(false);

  function buildConnection() {
    let builder = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl)
      .configureLogging(signalR.LogLevel.Information);

    if (autoReconnect) {
      builder = builder.withAutomaticReconnect();
    }

    const conn = builder.build();

    conn.onclose(() => {
      connected.value = false;
    });

    conn.onreconnected(() => {
      connected.value = true;
    });

    conn.onreconnecting(() => {
      connected.value = false;
    });

    return conn;
  }

  async function start() {
    if (!connection.value) {
      connection.value = buildConnection();
    }
    try {
      await connection.value.start();
      connected.value = true;
    } catch (error) {
      console.error('SignalR 连接失败:', error);
      connected.value = false;
    }
  }

  async function stop() {
    if (connection.value) {
      await connection.value.stop();
      connected.value = false;
    }
  }

  async function joinProject(projectId: string) {
    if (connection.value && connected.value) {
      await connection.value.invoke('JoinProject', projectId);
    }
  }

  async function leaveProject(projectId: string) {
    if (connection.value && connected.value) {
      await connection.value.invoke('LeaveProject', projectId);
    }
  }

  function onAgentMessage(
    callback: (data: {
      agentId: string;
      content: string;
      agentName: string;
      type: string;
    }) => void,
  ) {
    if (connection.value) {
      connection.value.on('AgentMessage', callback);
    }
  }

  function onAgentThinking(
    callback: (data: { agentId: string; isThinking: boolean }) => void,
  ) {
    if (connection.value) {
      connection.value.on('AgentThinking', callback);
    }
  }

  function onTaskStatusChanged(
    callback: (data: {
      taskId: string;
      status: string;
      agentId?: string;
    }) => void,
  ) {
    if (connection.value) {
      connection.value.on('TaskStatusChanged', callback);
    }
  }

  return {
    connected,
    connection,
    joinProject,
    leaveProject,
    onAgentMessage,
    onAgentThinking,
    onTaskStatusChanged,
    start,
    stop,
  };
}
