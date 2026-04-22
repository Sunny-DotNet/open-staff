import type { Ref } from 'vue';

import { computed, onUnmounted, ref, watch } from 'vue';

import {
  deleteApiPermissionRequestsListenersByListenerId,
  postApiPermissionRequestsByRequestIdResponses,
  postApiPermissionRequestsListeners,
  unwrapClientEnvelope,
} from '@openstaff/api';
import { message } from 'ant-design-vue';

import { useNotification } from '@/composables/useNotification';

export interface PermissionPrompt {
  commandText?: null | string;
  detailsJson?: null | string;
  fileName?: null | string;
  kind: string;
  message: string;
  projectName?: null | string;
  sessionId?: null | string;
  requestId: string;
  roleName?: null | string;
  scene?: null | string;
  timeoutMs?: null | number;
  toolName?: null | string;
  url?: null | string;
  warning?: null | string;
}

export interface UsePermissionRequestsOptions {
  sessionId?: Ref<null | string>;
}

let globalChannelLeaseCount = 0;
let globalChannelMutation = Promise.resolve();

function enqueueGlobalChannelMutation(work: () => Promise<void>) {
  globalChannelMutation = globalChannelMutation.then(work, work);
  return globalChannelMutation;
}

async function acquireGlobalChannel(joinChannel: (channel: string) => Promise<void>) {
  await enqueueGlobalChannelMutation(async () => {
    if (globalChannelLeaseCount === 0) {
      await joinChannel('global');
    }

    globalChannelLeaseCount += 1;
  });
}

async function releaseGlobalChannel(leaveChannel: (channel: string) => Promise<void>) {
  await enqueueGlobalChannelMutation(async () => {
    if (globalChannelLeaseCount === 0) {
      return;
    }

    globalChannelLeaseCount -= 1;
    if (globalChannelLeaseCount === 0) {
      await leaveChannel('global');
    }
  });
}

function asNumber(value: unknown) {
  return typeof value === 'number' && Number.isFinite(value) ? value : null;
}

function asString(value: unknown) {
  return typeof value === 'string' && value.length > 0 ? value : null;
}

function normalizePayload(payload: unknown): Record<string, unknown> {
  if (!payload) {
    return {};
  }

  if (typeof payload === 'string') {
    try {
      const parsed = JSON.parse(payload);
      return parsed && typeof parsed === 'object' ? (parsed as Record<string, unknown>) : {};
    } catch {
      return {};
    }
  }

  return typeof payload === 'object' ? (payload as Record<string, unknown>) : {};
}

function normalizeSessionId(value?: null | string) {
  const trimmed = value?.trim();
  return trimmed ? trimmed : null;
}

function toSessionChannel(sessionId: string) {
  return `session:${sessionId}`;
}

export function usePermissionRequests(options: UsePermissionRequestsOptions = {}) {
  const { connected, joinChannel, leaveChannel, onEventType } = useNotification();
  const receivedPermissionRequests = ref<PermissionPrompt[]>([]);
  const listenerId = ref<null | string>(null);
  const permissionRespondingRequestId = ref<null | string>(null);
  const permissionRequests = computed(() =>
    receivedPermissionRequests.value.filter((item) => matchesScope(item)),
  );
  const activePermissionRequest = computed(() => permissionRequests.value[0] ?? null);
  const isActive = ref(false);

  let heartbeatHandle: null | ReturnType<typeof setInterval> = null;
  let joinedGlobalChannel = false;
  let joinedSessionChannel: null | string = null;

  function appendPermissionRequest(payload: Record<string, unknown>) {
    const requestId = asString(payload.requestId);
    const promptMessage = asString(payload.message);
    const promptKind = asString(payload.kind);
    if (!requestId || !promptMessage || !promptKind) {
      return;
    }

    if (receivedPermissionRequests.value.some((item) => item.requestId === requestId)) {
      return;
    }

    receivedPermissionRequests.value.push({
      requestId,
      kind: promptKind,
      message: promptMessage,
      commandText: asString(payload.commandText),
      detailsJson: asString(payload.detailsJson),
      fileName: asString(payload.fileName),
      projectName: asString(payload.projectName),
      sessionId: asString(payload.sessionId),
      roleName: asString(payload.roleName),
      scene: asString(payload.scene),
      timeoutMs: asNumber(payload.timeoutMs),
      toolName: asString(payload.toolName),
      url: asString(payload.url),
      warning: asString(payload.warning),
    });
  }

  function clearPermissionRequests() {
    receivedPermissionRequests.value = [];
    permissionRespondingRequestId.value = null;
  }

  function removePermissionRequest(requestId: string) {
    receivedPermissionRequests.value = receivedPermissionRequests.value.filter(
      (item) => item.requestId !== requestId,
    );

    if (permissionRespondingRequestId.value === requestId) {
      permissionRespondingRequestId.value = null;
    }
  }

  function handlePermissionRequestedNotification(messageData: { payload: unknown }) {
    if (!isActive.value) {
      return;
    }

    appendPermissionRequest(normalizePayload(messageData.payload));
  }

  function handlePermissionResolvedNotification(messageData: { payload: unknown }) {
    if (!isActive.value) {
      return;
    }

    const payload = normalizePayload(messageData.payload);
    const requestId = asString(payload.requestId);
    if (requestId) {
      removePermissionRequest(requestId);
    }
  }

  const unsubscribeRequested = onEventType(
    'permission_requested',
    handlePermissionRequestedNotification,
  );
  const unsubscribeResolved = onEventType(
    'permission_resolved',
    handlePermissionResolvedNotification,
  );

  async function ensureListenerRegistered() {
    if (!isActive.value) {
      return;
    }

    if (!joinedGlobalChannel) {
      await acquireGlobalChannel(joinChannel);
      joinedGlobalChannel = true;
    }

    const lease = unwrapClientEnvelope(
      await postApiPermissionRequestsListeners({
        body: {
          listenerId: listenerId.value ?? undefined,
        },
      }),
    ) as {
      listenerId?: null | string;
      pendingRequests?: unknown[];
    };
    listenerId.value = asString(lease.listenerId) ?? null;
    for (const pendingRequest of lease.pendingRequests ?? []) {
      appendPermissionRequest(normalizePayload(pendingRequest));
    }
    await ensureScopedChannels();
  }

  async function unregisterListener() {
    const currentListenerId = listenerId.value;
    listenerId.value = null;

    if (currentListenerId) {
      try {
        await deleteApiPermissionRequestsListenersByListenerId({
          path: { listenerId: currentListenerId },
        });
      } catch (error) {
        console.error('Failed to unregister permission listener.', error);
      }
    }

    if (joinedSessionChannel) {
      const channel = joinedSessionChannel;
      joinedSessionChannel = null;
      try {
        await leaveChannel(channel);
      } catch (error) {
        console.error('Failed to leave scoped permission session channel.', error);
      }
    }

    if (joinedGlobalChannel) {
      joinedGlobalChannel = false;
      try {
        await releaseGlobalChannel(leaveChannel);
      } catch (error) {
        console.error('Failed to leave global notification channel.', error);
      }
    }
  }

  async function ensureScopedChannels() {
    if (!isActive.value) {
      return;
    }

    const nextSessionId = normalizeSessionId(options.sessionId?.value);
    const nextChannel = nextSessionId ? toSessionChannel(nextSessionId) : null;
    if (joinedSessionChannel === nextChannel) {
      return;
    }

    if (joinedSessionChannel) {
      const previousChannel = joinedSessionChannel;
      joinedSessionChannel = null;
      await leaveChannel(previousChannel);
    }

    if (nextChannel) {
      await joinChannel(nextChannel);
      joinedSessionChannel = nextChannel;
    }
  }

  function startHeartbeat() {
    if (heartbeatHandle) {
      return;
    }

    heartbeatHandle = setInterval(() => {
      if (isActive.value && connected.value) {
        void ensureListenerRegistered();
        void ensureScopedChannels();
      }
    }, 60_000);
  }

  function stopHeartbeat() {
    if (heartbeatHandle) {
      clearInterval(heartbeatHandle);
      heartbeatHandle = null;
    }
  }

  async function activate() {
    if (isActive.value) {
      return;
    }

    isActive.value = true;
    startHeartbeat();
    await ensureListenerRegistered();
  }

  async function deactivate() {
    if (!isActive.value) {
      return;
    }

    isActive.value = false;
    stopHeartbeat();
    clearPermissionRequests();
    await unregisterListener();
  }

  async function respondToPermissionRequest(kind: 'accept' | 'reject') {
    const current = activePermissionRequest.value;
    if (!current) {
      return;
    }

    if (!listenerId.value) {
      await ensureListenerRegistered();
    }

    if (!listenerId.value) {
      message.error('当前页面未成功注册授权监听器');
      return;
    }

    permissionRespondingRequestId.value = current.requestId;

    try {
      const result = unwrapClientEnvelope(
        await postApiPermissionRequestsByRequestIdResponses({
          body: {
            kind,
            listenerId: listenerId.value,
          },
          path: { requestId: current.requestId },
        }),
      ) as { accepted?: boolean; status?: string };

      if (result.accepted || result.status === 'already_completed' || result.status === 'not_found') {
        removePermissionRequest(current.requestId);
        return;
      }

      if (result.status === 'listener_not_registered') {
        await ensureListenerRegistered();
        message.warning('授权监听器已过期，请重新选择');
        return;
      }

      message.warning(`提交授权结果未生效：${result.status ?? 'unknown'}`);
    } catch (error) {
      const text = error instanceof Error ? error.message : '提交授权结果失败';
      message.error(text);
    } finally {
      permissionRespondingRequestId.value = null;
    }
  }

  watch(connected, (value) => {
    if (value && isActive.value) {
      void ensureListenerRegistered();
      void ensureScopedChannels();
    }
  });

  watch(
    () => normalizeSessionId(options.sessionId?.value),
    () => {
      if (isActive.value && connected.value) {
        void ensureScopedChannels();
      }
    },
  );

  onUnmounted(() => {
    unsubscribeRequested();
    unsubscribeResolved();
    void deactivate();
  });

  return {
    activate,
    activePermissionRequest,
    deactivate,
    permissionRespondingRequestId,
    respondToPermissionRequest,
  };

  function matchesScope(request: PermissionPrompt) {
    if (!options.sessionId) {
      return true;
    }

    const currentSessionId = options.sessionId.value?.trim();
    if (!currentSessionId) {
      return false;
    }

    return request.sessionId === currentSessionId;
  }
}
