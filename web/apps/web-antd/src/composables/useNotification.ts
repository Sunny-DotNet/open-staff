import { type Ref, onUnmounted, ref, shallowRef } from 'vue';

import * as signalR from '@microsoft/signalr';

import type { AgentApi } from '#/api/openstaff/agent';

// ===== 通知消息类型 =====

export interface NotificationMessage {
  channel: string;
  eventType: string;
  payload: unknown;
  sequenceNo?: number;
  timestamp: string;
}

export type NotifyHandler = (msg: NotificationMessage) => void;
export type SessionEventHandler = (evt: AgentApi.SessionEvent) => void;

// ===== 单例连接管理 =====

let _connection: signalR.HubConnection | null = null;
let _connectionPromise: Promise<void> | null = null;
const _connected = ref(false);
const _handlers = new Map<string, Set<NotifyHandler>>();

function getOrCreateConnection(hubUrl: string): signalR.HubConnection {
  if (_connection) return _connection;

  _connection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl)
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Information)
    .build();

  _connection.onclose(() => {
    _connected.value = false;
  });

  _connection.onreconnected(() => {
    _connected.value = true;
  });

  _connection.onreconnecting(() => {
    _connected.value = false;
  });

  // 统一 Notify 分发
  _connection.on('Notify', (msg: NotificationMessage) => {
    // 按 channel 分发
    const channelHandlers = _handlers.get(msg.channel);
    if (channelHandlers) {
      for (const handler of channelHandlers) {
        try {
          handler(msg);
        } catch {
          // ignore handler errors
        }
      }
    }

    // 按 eventType 分发（全局监听）
    const typeHandlers = _handlers.get(`@type:${msg.eventType}`);
    if (typeHandlers) {
      for (const handler of typeHandlers) {
        try {
          handler(msg);
        } catch {
          // ignore handler errors
        }
      }
    }

    // 全局通配符
    const globalHandlers = _handlers.get('*');
    if (globalHandlers) {
      for (const handler of globalHandlers) {
        try {
          handler(msg);
        } catch {
          // ignore handler errors
        }
      }
    }
  });

  return _connection;
}

async function ensureConnected(hubUrl: string): Promise<signalR.HubConnection> {
  const conn = getOrCreateConnection(hubUrl);

  if (conn.state === signalR.HubConnectionState.Connected) {
    return conn;
  }

  if (!_connectionPromise) {
    _connectionPromise = conn
      .start()
      .then(() => {
        _connected.value = true;
        _connectionPromise = null;
      })
      .catch((err) => {
        console.error('NotificationHub 连接失败:', err);
        _connected.value = false;
        _connectionPromise = null;
        throw err;
      });
  }

  await _connectionPromise;
  return conn;
}

// ===== Composable =====

export interface UseNotificationOptions {
  /** Hub URL，默认自动从 baseURL 推导 */
  hubUrl?: string;
}

export interface UseNotificationReturn {
  /** 连接状态 */
  connected: Ref<boolean>;
  /** 连接实例（调试用） */
  connection: Ref<signalR.HubConnection | null>;
  /** 加入频道 */
  joinChannel: (channel: string) => Promise<void>;
  /** 离开频道 */
  leaveChannel: (channel: string) => Promise<void>;
  /** 监听指定频道的通知 */
  onChannel: (channel: string, handler: NotifyHandler) => () => void;
  /** 监听指定事件类型（跨频道） */
  onEventType: (eventType: string, handler: NotifyHandler) => () => void;
  /** 启动会话流（SignalR Streaming） */
  streamSession: (
    sessionId: string,
    onEvent: SessionEventHandler,
    onComplete?: () => void,
    onError?: (err: Error) => void,
  ) => signalR.ISubscription<AgentApi.SessionEvent>;
  /** 停止连接 */
  stop: () => Promise<void>;
}

export function useNotification(
  options: UseNotificationOptions = {},
): UseNotificationReturn {
  const hubUrl = options.hubUrl ?? '/api/hubs/notification';
  const connection = shallowRef<signalR.HubConnection | null>(null);
  const localCleanups: Array<() => void> = [];

  // 自动连接
  ensureConnected(hubUrl)
    .then((conn) => {
      connection.value = conn;
    })
    .catch(() => {
      // 已在 ensureConnected 中 log
    });

  async function joinChannel(channel: string): Promise<void> {
    const conn = await ensureConnected(hubUrl);
    await conn.invoke('JoinChannel', channel);
  }

  async function leaveChannel(channel: string): Promise<void> {
    const conn = await ensureConnected(hubUrl);
    await conn.invoke('LeaveChannel', channel);
  }

  function onChannel(channel: string, handler: NotifyHandler): () => void {
    if (!_handlers.has(channel)) {
      _handlers.set(channel, new Set());
    }
    _handlers.get(channel)!.add(handler);

    const cleanup = () => {
      _handlers.get(channel)?.delete(handler);
      if (_handlers.get(channel)?.size === 0) {
        _handlers.delete(channel);
      }
    };
    localCleanups.push(cleanup);
    return cleanup;
  }

  function onEventType(eventType: string, handler: NotifyHandler): () => void {
    return onChannel(`@type:${eventType}`, handler);
  }

  function streamSession(
    sessionId: string,
    onEvent: SessionEventHandler,
    onComplete?: () => void,
    onError?: (err: Error) => void,
  ): signalR.ISubscription<AgentApi.SessionEvent> {
    const conn = getOrCreateConnection(hubUrl);

    const stream = conn.stream<AgentApi.SessionEvent>(
      'StreamSession',
      sessionId,
    );

    const subscription = stream.subscribe({
      next: (evt) => {
        try {
          onEvent(evt);
        } catch {
          // ignore
        }
      },
      complete: () => {
        onComplete?.();
      },
      error: (err) => {
        onError?.(err instanceof Error ? err : new Error(String(err)));
      },
    });

    return subscription;
  }

  async function stop(): Promise<void> {
    if (_connection) {
      await _connection.stop();
      _connected.value = false;
    }
  }

  // 组件卸载时清理本组件注册的 handlers
  onUnmounted(() => {
    for (const cleanup of localCleanups) {
      cleanup();
    }
    localCleanups.length = 0;
  });

  return {
    connected: _connected,
    connection,
    joinChannel,
    leaveChannel,
    onChannel,
    onEventType,
    streamSession,
    stop,
  };
}
