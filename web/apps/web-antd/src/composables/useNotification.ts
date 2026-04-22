import type { SessionEventDto } from '@openstaff/api';
import type { Ref } from 'vue';

import { onUnmounted, ref, shallowRef } from 'vue';
import * as signalR from '@microsoft/signalr';

export interface NotificationMessage {
  channel: string;
  eventType: string;
  payload: unknown;
  sequenceNo?: number;
  timestamp: string;
}

export type NotifyHandler = (message: NotificationMessage) => void;
export type SessionEventHandler = (event: SessionEventDto) => void;

let sharedConnection: null | signalR.HubConnection = null;
let sharedConnectionPromise: null | Promise<void> = null;
const sharedConnected = ref(false);
const sharedHandlers = new Map<string, Set<NotifyHandler>>();

function getOrCreateConnection(hubUrl: string) {
  if (sharedConnection) {
    return sharedConnection;
  }

  sharedConnection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl)
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Information)
    .build();

  sharedConnection.onclose(() => {
    sharedConnected.value = false;
  });

  sharedConnection.onreconnected(() => {
    sharedConnected.value = true;
  });

  sharedConnection.onreconnecting(() => {
    sharedConnected.value = false;
  });

  sharedConnection.on('Notify', (message: NotificationMessage) => {
    const channelHandlers = sharedHandlers.get(message.channel);
    if (channelHandlers) {
      for (const handler of channelHandlers) {
        try {
          handler(message);
        } catch {
          // Ignore individual handler failures.
        }
      }
    }

    const typeHandlers = sharedHandlers.get(`@type:${message.eventType}`);
    if (typeHandlers) {
      for (const handler of typeHandlers) {
        try {
          handler(message);
        } catch {
          // Ignore individual handler failures.
        }
      }
    }

    const globalHandlers = sharedHandlers.get('*');
    if (globalHandlers) {
      for (const handler of globalHandlers) {
        try {
          handler(message);
        } catch {
          // Ignore individual handler failures.
        }
      }
    }
  });

  return sharedConnection;
}

async function ensureConnected(hubUrl: string) {
  const connection = getOrCreateConnection(hubUrl);

  if (connection.state === signalR.HubConnectionState.Connected) {
    return connection;
  }

  if (!sharedConnectionPromise) {
    sharedConnectionPromise = connection
      .start()
      .then(() => {
        sharedConnected.value = true;
        sharedConnectionPromise = null;
      })
      .catch((error) => {
        sharedConnected.value = false;
        sharedConnectionPromise = null;
        throw error;
      });
  }

  await sharedConnectionPromise;
  return connection;
}

export interface UseNotificationOptions {
  hubUrl?: string;
}

export interface UseNotificationReturn {
  connected: Ref<boolean>;
  connection: Ref<null | signalR.HubConnection>;
  joinChannel: (channel: string) => Promise<void>;
  leaveChannel: (channel: string) => Promise<void>;
  onChannel: (channel: string, handler: NotifyHandler) => () => void;
  onEventType: (eventType: string, handler: NotifyHandler) => () => void;
  streamSession: (
    sessionId: string,
    onEvent: SessionEventHandler,
    onComplete?: () => void,
    onError?: (error: Error) => void,
    afterSequenceNo?: number,
  ) => Promise<signalR.ISubscription<SessionEventDto>>;
  streamTask: (
    taskId: string,
    onEvent: SessionEventHandler,
    onComplete?: () => void,
    onError?: (error: Error) => void,
  ) => Promise<signalR.ISubscription<SessionEventDto>>;
  stop: () => Promise<void>;
}

export function useNotification(
  options: UseNotificationOptions = {},
): UseNotificationReturn {
  const hubUrl = options.hubUrl ?? '/hubs/notification';
  const connection = shallowRef<null | signalR.HubConnection>(null);
  const localCleanups: Array<() => void> = [];

  ensureConnected(hubUrl)
    .then((currentConnection) => {
      connection.value = currentConnection;
    })
    .catch(() => {
      // Error is surfaced when a caller actually tries to use the connection.
    });

  async function joinChannel(channel: string) {
    const currentConnection = await ensureConnected(hubUrl);
    await currentConnection.invoke('JoinChannel', channel);
  }

  async function leaveChannel(channel: string) {
    const currentConnection = await ensureConnected(hubUrl);
    await currentConnection.invoke('LeaveChannel', channel);
  }

  function onChannel(channel: string, handler: NotifyHandler) {
    if (!sharedHandlers.has(channel)) {
      sharedHandlers.set(channel, new Set());
    }
    sharedHandlers.get(channel)!.add(handler);

    const cleanup = () => {
      sharedHandlers.get(channel)?.delete(handler);
      if (sharedHandlers.get(channel)?.size === 0) {
        sharedHandlers.delete(channel);
      }
    };

    localCleanups.push(cleanup);
    return cleanup;
  }

  function onEventType(eventType: string, handler: NotifyHandler) {
    return onChannel(`@type:${eventType}`, handler);
  }

  async function streamSession(
    sessionId: string,
    onEvent: SessionEventHandler,
    onComplete?: () => void,
    onError?: (error: Error) => void,
    afterSequenceNo = 0,
  ) {
    const currentConnection = await ensureConnected(hubUrl);
    const stream = currentConnection.stream<SessionEventDto>(
      'StreamSession',
      sessionId,
      afterSequenceNo,
    );

    return stream.subscribe({
      next: (event) => {
        try {
          onEvent(event);
        } catch {
          // Ignore downstream reducer failures to keep the stream alive.
        }
      },
      complete: () => {
        onComplete?.();
      },
      error: (error) => {
        onError?.(error instanceof Error ? error : new Error(String(error)));
      },
    });
  }

  async function streamTask(
    taskId: string,
    onEvent: SessionEventHandler,
    onComplete?: () => void,
    onError?: (error: Error) => void,
  ) {
    const currentConnection = await ensureConnected(hubUrl);
    const stream = currentConnection.stream<SessionEventDto>('StreamTask', taskId);

    return stream.subscribe({
      next: (event) => {
        try {
          onEvent(event);
        } catch {
          // Ignore downstream reducer failures to keep the stream alive.
        }
      },
      complete: () => {
        onComplete?.();
      },
      error: (error) => {
        onError?.(error instanceof Error ? error : new Error(String(error)));
      },
    });
  }

  async function stop() {
    if (!sharedConnection) {
      return;
    }

    await sharedConnection.stop();
    sharedConnected.value = false;
  }

  onUnmounted(() => {
    for (const cleanup of localCleanups) {
      cleanup();
    }
    localCleanups.length = 0;
  });

  return {
    connected: sharedConnected,
    connection,
    joinChannel,
    leaveChannel,
    onChannel,
    onEventType,
    streamSession,
    streamTask,
    stop,
  };
}
