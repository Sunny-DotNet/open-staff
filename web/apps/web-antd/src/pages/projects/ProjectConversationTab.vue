<script setup lang="ts">
import type {
  AgentRoleDto,
  AgentRuntimePreviewDto,
  ChatMessageTimingDto,
  ChatMessageUsageDto,
  ConversationTaskOutput,
  ConversationMentionDto,
  ProjectDto,
  ProviderAccountDto,
  SessionEventDto,
} from '@openstaff/api';
import type { ISubscription } from '@microsoft/signalr';

import {
  getApiProjectsByProjectIdAgentsByAgentIdRuntimePreview,
  getApiSessionsByProjectByProjectIdActive,
  getApiSessionsBySessionId,
  getApiSessionsBySessionIdChatMessages,
  getApiSessionsBySessionIdEvents,
  postApiProjectsByProjectIdAgentsByAgentIdMessage,
  postApiSessions,
  postApiSessionsBySessionIdCancel,
  postApiSessionsBySessionIdMessages,
  unwrapClientEnvelope,
} from '@openstaff/api';
import { message } from 'ant-design-vue';
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';

import AgentConversationPanel from '@/components/AgentConversationPanel.vue';
import PermissionRequestModal from '@/components/PermissionRequestModal.vue';
import {
  applySessionConversationEvent,
  createLocalConversationMessageId,
  createSessionConversationState,
  removePendingAssistantPlaceholder,
  sanitizeSessionConversationAssistantContent,
  startAssistantPlaceholder,
  shouldStreamSessionConversation,
  type SessionConversationMessage,
} from '@/components/session-conversation-stream';
import { useNotification } from '@/composables/useNotification';
import { usePermissionRequests } from '@/composables/usePermissionRequests';
import { t } from '@/i18n';
import { localizeJobTitle } from '@/utils/job-title';

import RoleWorkspace from '../agent-roles/workspace.vue';

type HeaderAgent = {
  avatar?: string;
  jobTitle?: string;
  key: string;
  label: string;
  projectAgentRoleId?: string;
  role?: AgentRoleDto | null;
};

const props = defineProps<{
  brainstormAgent?: HeaderAgent | null;
  headerAgents?: HeaderAgent[];
  emptyDescription: string;
  inputDisabledHint?: string;
  inputPlaceholder: string;
  project: ProjectDto | null;
  projectId: string;
  providers?: ProviderAccountDto[];
  readOnlyDescription?: string;
  readOnlyMessage?: string;
  scene: string;
  startFailedMessage: string;
  unavailableDescription?: string;
  unavailableMessage?: string;
  canAccess?: boolean;
  canInput?: boolean;
}>();
const emit = defineEmits<{
  (event: 'project-state-changed'): void;
}>();

const { connected, streamSession, streamTask } = useNotification();
const currentSessionId = ref<null | string>(null);
const {
  activate: activatePermissionRequests,
  activePermissionRequest,
  deactivate: deactivatePermissionRequests,
  permissionRespondingRequestId,
  respondToPermissionRequest,
} = usePermissionRequests({
  sessionId: currentSessionId,
});

const conversationState = createSessionConversationState();
const chatInput = ref('');
const chatLoading = ref(false);
const chatMessages = ref<SessionConversationMessage[]>([]);
const currentSubscription = ref<ISubscription<SessionEventDto> | null>(null);
const currentSessionSequenceNo = ref(0);
const privateConversationState = createSessionConversationState();
const privateChatInput = ref('');
const privateChatLoading = ref(false);
const privateChatMessages = ref<SessionConversationMessage[]>([]);
const privateChatOpen = ref(false);
const privateChatSubscription = ref<ISubscription<SessionEventDto> | null>(null);
const roleWorkspaceOpen = ref(false);
const runtimePreviewOpen = ref(false);
const runtimePreviewLoading = ref(false);
const runtimePreview = ref<AgentRuntimePreviewDto | null>(null);
const selectedHeaderAgent = ref<HeaderAgent | null>(null);
const restoring = ref(false);
const runtimePreviewEnabled = import.meta.env.DEV;

// 头脑风暴是固定“秘书”场景，这里给一个稳定头像，避免回放历史或流式阶段退回通用首字母占位。
const SECRETARY_AVATAR_DATA_URI = `data:image/svg+xml;charset=UTF-8,${encodeURIComponent(`
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 96 96" fill="none">
  <defs>
    <linearGradient id="secretary-avatar-bg" x1="16" y1="12" x2="80" y2="84" gradientUnits="userSpaceOnUse">
      <stop stop-color="#8B5CF6" />
      <stop offset="1" stop-color="#2563EB" />
    </linearGradient>
  </defs>
  <rect width="96" height="96" rx="48" fill="url(#secretary-avatar-bg)" />
  <circle cx="48" cy="32" r="16" fill="#F7D7C4" />
  <path d="M30 31C30 20.5066 38.5066 12 49 12C56.5795 12 63.1228 16.4412 66.1558 22.8633C62.9956 21.3088 59.5134 20.5 55.9766 20.5C43.2861 20.5 33 30.7861 33 43.4766V45H30V31Z" fill="#1E1B4B" fill-opacity="0.88" />
  <path d="M26 78C29.7161 61.2788 38.2253 53 48 53C57.7747 53 66.2839 61.2788 70 78H26Z" fill="white" fill-opacity="0.95" />
  <path d="M35 78L39.5 56H56.5L61 78H35Z" fill="#312E81" fill-opacity="0.9" />
  <path d="M48 53L53.5 61H42.5L48 53Z" fill="#C4B5FD" />
  <rect x="57" y="46" width="18" height="23" rx="4" fill="white" fill-opacity="0.96" />
  <path d="M61 53H70.5M61 58.5H70.5M61 64H67.5" stroke="#8B5CF6" stroke-width="3" stroke-linecap="round" />
</svg>
`)}`;

const canAccessScene = computed(() => props.canAccess ?? true);
const canInputScene = computed(
  () => canAccessScene.value && (props.canInput ?? canAccessScene.value),
);
const conversationAgentName = computed(() =>
  props.scene === 'ProjectBrainstorm'
    ? (props.brainstormAgent?.label || t('project.brainstormAgentName'))
    : (props.project?.name || 'OpenStaff'),
);
const conversationAgentAvatar = computed(() =>
  props.scene === 'ProjectBrainstorm'
    ? (props.brainstormAgent?.avatar || SECRETARY_AVATAR_DATA_URI)
    : undefined,
);
const projectRoleAvatars = computed(() => {
  if (props.scene !== 'ProjectGroup') {
    return [];
  }

  return props.headerAgents ?? [];
});
const projectGroupHeaderTitle = computed(() => props.project?.name || t('project.chat'));
const projectGroupHeaderSubtitle = computed(
  () => `${t('project.assignedAgents')} · ${projectRoleAvatars.value.length}`,
);
const conversationSending = computed(() =>
  props.scene === 'ProjectGroup'
    ? false
    : (chatLoading.value || restoring.value),
);
const projectGroupMentionOptions = computed(() => {
  const options = new Map<string, {
    avatar?: string;
    description?: string;
    key: string;
    keywords?: string[];
    label: string;
    value?: string;
  }>();

  for (const agent of projectRoleAvatars.value) {
    const optionKey = agent.projectAgentRoleId || agent.key || agent.label;
    if (!optionKey || options.has(optionKey)) {
      continue;
    }

    options.set(optionKey, {
      avatar: agent.avatar,
      description: localizeJobTitle(agent.jobTitle || agent.role?.jobTitle, agent.jobTitle || agent.role?.jobTitle),
      key: optionKey,
      keywords: [
        agent.key,
        agent.label,
        agent.jobTitle,
        agent.projectAgentRoleId,
        agent.role?.name,
        agent.role?.jobTitle,
        agent.role?.roleType,
      ].filter((value): value is string => typeof value === 'string' && value.trim().length > 0),
      label: agent.label,
      value: agent.label,
    });
  }

  return [...options.values()];
});
const privateChatTitle = computed(() => selectedHeaderAgent.value?.label || t('project.privateChat'));
const runtimePreviewTitle = computed(() => {
  const label = selectedHeaderAgent.value?.label;
  return label
    ? `${t('project.agentCardRuntimePreview')} · ${label}`
    : t('project.agentCardRuntimePreview');
});
const runtimePreviewBuiltinTools = computed(() =>
  (runtimePreview.value?.tools ?? []).filter(item => item.source === 'builtin'),
);
const runtimePreviewMcpTools = computed(() =>
  (runtimePreview.value?.tools ?? []).filter(item => item.source === 'mcp'),
);
const runtimePreviewSkills = computed(() => runtimePreview.value?.skills ?? []);
const runtimePreviewMissingSkills = computed(() => runtimePreview.value?.missingSkills ?? []);
const projectGroupIdentityLookup = computed(() => {
  const lookup = new Map<string, { avatar?: string; displayName: string }>();

  for (const agent of projectRoleAvatars.value) {
    const identity = {
      avatar: agent.avatar,
      displayName: agent.label,
    };
    const keys = [
      agent.key,
      agent.projectAgentRoleId,
      agent.label,
      agent.jobTitle,
      agent.role?.id,
      agent.role?.name,
      agent.role?.jobTitle,
      agent.role?.roleType,
    ];

    for (const key of keys) {
      const normalized = normalizeAgentIdentityKey(key);
      if (normalized && !lookup.has(normalized)) {
        lookup.set(normalized, identity);
      }
    }
  }

  return lookup;
});
const projectGroupMentionLookup = computed(() => {
  const lookup = new Map<string, HeaderAgent>();

  for (const agent of projectRoleAvatars.value) {
    const keys = [
      agent.key,
      agent.projectAgentRoleId,
      agent.label,
      agent.jobTitle,
      agent.role?.id,
      agent.role?.name,
      agent.role?.jobTitle,
      agent.role?.roleType,
    ];

    for (const key of keys) {
      const normalized = normalizeAgentIdentityKey(key);
      if (normalized && !lookup.has(normalized)) {
        lookup.set(normalized, agent);
      }
    }
  }

  return lookup;
});

watch(
  () => [props.projectId, props.scene, canAccessScene.value, canInputScene.value] as const,
  async ([projectId, _scene, canAccess]) => {
    disposeStream();
    resetConversation(false);

    if (!projectId || !canAccess) {
      return;
    }

    await restoreSession();
  },
  { immediate: true },
);

watch(
  () => [props.scene, projectGroupIdentityLookup.value, chatMessages.value.length] as const,
  () => {
    refreshProjectGroupMessageIdentities(chatMessages.value);
  },
  { deep: true },
);

onMounted(() => {
  void activatePermissionRequests();
});

onUnmounted(() => {
  disposeStream();
  disposePrivateChatStream();
  void deactivatePermissionRequests();
});

async function restoreSession() {
  restoring.value = true;

  try {
    const session = unwrapClientEnvelope(
      await getApiSessionsByProjectByProjectIdActive({
        path: { projectId: props.projectId },
        query: { scene: props.scene },
      }),
    );

    if (!session?.id) {
      return;
    }

    currentSessionId.value = session.id;
    await hydrateSessionConversation(session.id);

    if (canInputScene.value && shouldStreamSessionConversation(session)) {
      await connectStream(session.id, currentSessionSequenceNo.value);
    }
  } catch (error) {
    if (!isNotFoundError(error)) {
      message.error(getErrorMessage(error, props.startFailedMessage));
    }
  } finally {
    restoring.value = false;
  }
}

async function loadHistory(sessionId: string) {
  const history = unwrapClientEnvelope(
    await getApiSessionsBySessionIdChatMessages({
      path: { sessionId },
      query: { skip: 0, take: 200 },
    }),
  );

  const nextMessages: SessionConversationMessage[] = [];
  for (const item of history.messages ?? []) {
    const role = item.role === 'user' ? 'user' : 'assistant';
    const content = role === 'assistant'
      ? sanitizeSessionConversationAssistantContent(item.content ?? '')
      : (item.content ?? '');
    if (role === 'assistant' && !content) {
      continue;
    }

    nextMessages.push({
      agentKey: item.projectAgentRoleId ?? item.agentRoleId ?? null,
      content,
      id: item.id ?? createLocalConversationMessageId('history'),
      parentMessageId: item.parentMessageId ?? null,
      role,
      timestamp: item.createdAt ?? new Date().toISOString(),
      timing: normalizeTiming(item.timing),
      usage: normalizeUsage(item.usage),
      ...resolveProjectGroupIdentity(item.role, item.projectAgentRoleId ?? item.agentRoleId),
    });
  }

  chatMessages.value = nextMessages;
}

async function reconstructFromEvents(sessionId: string) {
  const events = unwrapClientEnvelope(
    await getApiSessionsBySessionIdEvents({
      path: { sessionId },
    }),
  );

  currentSessionSequenceNo.value = 0;
  conversationState.pendingAssistantId = null;
  for (const event of events) {
    updateCurrentSessionSequenceNo(event);
    applySessionConversationEvent(chatMessages.value, conversationState, event);
    applyProjectGroupIdentityFromEvent(chatMessages.value, event);
  }
}

async function hydrateSessionConversation(sessionId: string) {
  await loadHistory(sessionId);
  await reconstructFromEvents(sessionId);
}

async function loadSessionSnapshot(sessionId: string) {
  return unwrapClientEnvelope(
    await getApiSessionsBySessionId({
      path: { sessionId },
    }),
  );
}

async function ensureCurrentSessionRenderingMode(sessionId: string) {
  const session = await loadSessionSnapshot(sessionId);
  if (shouldStreamSessionConversation(session)) {
    await connectStream(sessionId, currentSessionSequenceNo.value);
    return;
  }

  disposeStream();
  await hydrateSessionConversation(sessionId);
  chatLoading.value = false;
}

async function connectStream(sessionId: string, afterSequenceNo = 0) {
  if (!canInputScene.value) {
    return;
  }

  disposeStream();

  currentSubscription.value = await streamSession(
    sessionId,
    (event) => {
      if (event.eventType === 'project_state_changed') {
        emit('project-state-changed');
      }

      updateCurrentSessionSequenceNo(event);
      const reduced = applySessionConversationEvent(
        chatMessages.value,
        conversationState,
        event,
      );
      applyProjectGroupIdentityFromEvent(chatMessages.value, event);
      if (reduced.clearSending) {
        chatLoading.value = false;
      }
    },
    () => {
      chatLoading.value = false;
      currentSubscription.value = null;
      conversationState.pendingAssistantId = null;
    },
    (error) => {
      chatLoading.value = false;
      currentSubscription.value = null;
      removePendingAssistantPlaceholder(chatMessages.value, conversationState);
      chatMessages.value.push({
        content: `❌ ${error.message}`,
        id: createLocalConversationMessageId('assistant-error'),
        role: 'assistant',
        timestamp: new Date().toISOString(),
        });
      },
    afterSequenceNo,
  );
}

async function connectPrivateConversationStream(result: ConversationTaskOutput) {
  disposePrivateChatStream();

  if (result.sessionId) {
    privateChatSubscription.value = await streamSession(
      result.sessionId,
      (event) => {
        const reduced = applySessionConversationEvent(
          privateChatMessages.value,
          privateConversationState,
          event,
        );
        if (reduced.clearSending) {
          privateChatLoading.value = false;
        }
      },
      () => {
        privateChatLoading.value = false;
        privateChatSubscription.value = null;
        privateConversationState.pendingAssistantId = null;
      },
      (error) => {
        privateChatLoading.value = false;
        privateChatSubscription.value = null;
        removePendingAssistantPlaceholder(privateChatMessages.value, privateConversationState);
        privateChatMessages.value.push({
          content: `❌ ${error.message}`,
          id: createLocalConversationMessageId('assistant-error'),
          role: 'assistant',
          timestamp: new Date().toISOString(),
        });
      },
    );
    return;
  }

  if (!result.taskId) {
    throw new Error(t('project.privateChatStartFailed'));
  }

  privateChatSubscription.value = await streamTask(
    result.taskId,
    (event) => {
      const reduced = applySessionConversationEvent(
        privateChatMessages.value,
        privateConversationState,
        event,
      );
      if (reduced.clearSending) {
        privateChatLoading.value = false;
      }
    },
    () => {
      privateChatLoading.value = false;
      privateChatSubscription.value = null;
      privateConversationState.pendingAssistantId = null;
    },
    (error) => {
      privateChatLoading.value = false;
      privateChatSubscription.value = null;
      removePendingAssistantPlaceholder(privateChatMessages.value, privateConversationState);
      privateChatMessages.value.push({
        content: `❌ ${error.message}`,
        id: createLocalConversationMessageId('assistant-error'),
        role: 'assistant',
        timestamp: new Date().toISOString(),
      });
    },
  );
}

async function sendMessage() {
  if (
    !props.projectId
    || !canInputScene.value
    || !chatInput.value.trim()
    || chatLoading.value
  ) {
    return;
  }

  const userMessage = chatInput.value.trim();
  const groupPayload = buildProjectGroupMessagePayload(userMessage);
  chatMessages.value.push({
    content: userMessage,
    id: createLocalConversationMessageId('user'),
    role: 'user',
    timestamp: new Date().toISOString(),
  });
  chatInput.value = '';
  chatLoading.value = true;

  try {
    if (!currentSessionId.value) {
      const created = unwrapClientEnvelope(
        await postApiSessions({
          body: {
            input: groupPayload.executionInput,
            rawInput: groupPayload.rawInput,
            mentions: groupPayload.mentions.length > 0 ? groupPayload.mentions : undefined,
            projectId: props.projectId,
            scene: props.scene,
          },
        }),
      );

      if (!created.taskId) {
        throw new Error(props.startFailedMessage);
      }

      currentSessionId.value = created.sessionId ?? null;
      if (props.scene !== 'ProjectGroup') {
        startAssistantPlaceholder(chatMessages.value, conversationState);
      }
      if (!currentSessionId.value) {
        throw new Error(props.startFailedMessage);
      }

      await ensureCurrentSessionRenderingMode(currentSessionId.value);
      return;
    }

    if (props.scene !== 'ProjectGroup') {
      startAssistantPlaceholder(chatMessages.value, conversationState);
    }
    const result = unwrapClientEnvelope(await postApiSessionsBySessionIdMessages({
      body: {
        input: groupPayload.executionInput,
        rawInput: groupPayload.rawInput,
        mentions: groupPayload.mentions.length > 0 ? groupPayload.mentions : undefined,
      },
      path: { sessionId: currentSessionId.value },
    }));

    if (!result.taskId) {
      throw new Error(props.startFailedMessage);
    }

    currentSessionId.value = result.sessionId ?? currentSessionId.value;
    if (!currentSessionId.value) {
      throw new Error(props.startFailedMessage);
    }

    if (!currentSubscription.value) {
      await ensureCurrentSessionRenderingMode(currentSessionId.value);
    }
  } catch (error) {
    removePendingAssistantPlaceholder(chatMessages.value, conversationState);
    chatLoading.value = false;
    message.error(getErrorMessage(error, props.startFailedMessage));
  }
}

async function cancelSession() {
  if (!currentSessionId.value) {
    resetConversation(false);
    return;
  }

  try {
    await postApiSessionsBySessionIdCancel({
      path: { sessionId: currentSessionId.value },
    });
    message.success(t('project.cancelSessionSuccess'));
  } catch (error) {
    message.error(getErrorMessage(error, props.startFailedMessage));
  } finally {
    disposeStream();
    resetConversation(false);
  }
}

function openRoleTestChat(agent: HeaderAgent) {
  if (!agent.role?.id) {
    message.error(t('project.agentCardRoleUnavailable'));
    return;
  }

  selectedHeaderAgent.value = agent;
  roleWorkspaceOpen.value = true;
}

async function openRuntimePreview(agent: HeaderAgent) {
  if (!runtimePreviewEnabled) {
    return;
  }

  if (!agent.projectAgentRoleId) {
    message.error(t('project.validationProjectAgent'));
    return;
  }

  selectedHeaderAgent.value = agent;
  runtimePreviewOpen.value = true;
  runtimePreviewLoading.value = true;
  runtimePreview.value = null;

  try {
    runtimePreview.value = unwrapClientEnvelope(
      await getApiProjectsByProjectIdAgentsByAgentIdRuntimePreview({
        path: {
          agentId: agent.projectAgentRoleId,
          projectId: props.projectId,
        },
      }),
    );
  } catch (error) {
    runtimePreviewOpen.value = false;
    message.error(getErrorMessage(error, t('project.agentCardRuntimePreviewLoadFailed')));
  } finally {
    runtimePreviewLoading.value = false;
  }
}

function closeRuntimePreview() {
  runtimePreviewOpen.value = false;
  runtimePreviewLoading.value = false;
  runtimePreview.value = null;
}

function openPrivateChat(agent: HeaderAgent) {
  if (!agent.projectAgentRoleId) {
    message.error(t('project.validationProjectAgent'));
    return;
  }

  selectedHeaderAgent.value = agent;
  resetPrivateChat();
  privateChatOpen.value = true;
}

function closePrivateChat() {
  disposePrivateChatStream();
  privateChatOpen.value = false;
  resetPrivateChat();
}

function disposePrivateChatStream() {
  privateChatSubscription.value?.dispose();
  privateChatSubscription.value = null;
}

function resetPrivateChat(clearInput = true) {
  if (clearInput) {
    privateChatInput.value = '';
  }
  privateChatLoading.value = false;
  privateChatMessages.value = [];
  privateConversationState.pendingAssistantId = null;
}

async function sendPrivateMessage() {
  if (
    !props.projectId
    || !selectedHeaderAgent.value?.projectAgentRoleId
    || !privateChatInput.value.trim()
    || privateChatLoading.value
  ) {
    return;
  }

  const userMessage = privateChatInput.value.trim();
  privateChatMessages.value.push({
    content: userMessage,
    id: createLocalConversationMessageId('private-user'),
    role: 'user',
    timestamp: new Date().toISOString(),
  });
  privateChatInput.value = '';
  privateChatLoading.value = true;

  try {
    startAssistantPlaceholder(privateChatMessages.value, privateConversationState);
    const result = unwrapClientEnvelope(
      await postApiProjectsByProjectIdAgentsByAgentIdMessage({
        body: { message: userMessage },
        path: {
          agentId: selectedHeaderAgent.value.projectAgentRoleId,
          projectId: props.projectId,
        },
      }),
    );

    await connectPrivateConversationStream(result);
  } catch (error) {
    removePendingAssistantPlaceholder(privateChatMessages.value, privateConversationState);
    privateChatLoading.value = false;
    message.error(getErrorMessage(error, t('project.privateChatStartFailed')));
  }
}

function disposeStream() {
  currentSubscription.value?.dispose();
  currentSubscription.value = null;
}

function resetConversation(clearInput = true) {
  if (clearInput) {
    chatInput.value = '';
  }
  chatLoading.value = false;
  chatMessages.value = [];
  currentSessionId.value = null;
  currentSessionSequenceNo.value = 0;
  conversationState.pendingAssistantId = null;
}

function isNotFoundError(error: unknown) {
  return error instanceof Error && /\b404\b/.test(error.message);
}

function getErrorMessage(error: unknown, fallback: string) {
  if (error instanceof Error && error.message) {
    return error.message;
  }

  return fallback;
}

function updateCurrentSessionSequenceNo(event: SessionEventDto) {
  const sequenceNo = normalizeMetric(event.sequenceNo);
  if (sequenceNo != null && sequenceNo > currentSessionSequenceNo.value) {
    currentSessionSequenceNo.value = sequenceNo;
  }
}

function normalizeMetric(value: null | number | string | undefined) {
  if (typeof value === 'number') {
    return value;
  }

  if (typeof value === 'string') {
    const parsed = Number(value);
    return Number.isNaN(parsed) ? undefined : parsed;
  }

  return undefined;
}

function normalizeAgentIdentityKey(value: null | string | undefined) {
  if (typeof value !== 'string') {
    return '';
  }

  return value.trim().toLowerCase();
}

function parseEventPayload(event: SessionEventDto) {
  if (!event.payload) {
    return {};
  }

  try {
    return JSON.parse(event.payload) as Record<string, unknown>;
  } catch {
    return {};
  }
}

function resolveProjectGroupIdentity(role: null | string | undefined, agentKey: null | string | undefined) {
  if (props.scene !== 'ProjectGroup' || role !== 'assistant') {
    return {};
  }

  const normalizedKey = normalizeAgentIdentityKey(agentKey);
  if (!normalizedKey) {
    return {};
  }

  const identity = projectGroupIdentityLookup.value.get(normalizedKey);
  if (!identity) {
    return {};
  }

  return {
    avatar: identity.avatar,
    displayName: identity.displayName,
  };
}

function applyProjectGroupIdentityToMessage(
  messages: SessionConversationMessage[],
  messageId: null | string | undefined,
  agentKey: null | string | undefined,
) {
  const identity = resolveProjectGroupIdentity('assistant', agentKey);
  if (!('displayName' in identity) || !identity.displayName) {
    return;
  }

  let messageIndex = typeof messageId === 'string'
    ? messages.findIndex((message) => message.id === messageId)
    : -1;

  if (messageIndex < 0) {
    messageIndex = [...messages].findLastIndex(
      (message) => message.role === 'assistant' && (message.streaming || message.thinkingStreaming),
    );
  }

  if (messageIndex < 0) {
    return;
  }

  const current = messages[messageIndex];
  if (!current || current.role !== 'assistant') {
    return;
  }

  messages.splice(messageIndex, 1, {
    ...current,
    agentKey,
    avatar: identity.avatar,
    displayName: identity.displayName,
  });
}

function applyProjectGroupIdentityFromEvent(
  messages: SessionConversationMessage[],
  event: SessionEventDto,
) {
  if (props.scene !== 'ProjectGroup') {
    return;
  }

  const payload = parseEventPayload(event);
  const projectAgentRoleId = typeof payload.projectAgentRoleId === 'string'
    ? payload.projectAgentRoleId
    : undefined;
  const agentRoleId = typeof payload.agentRoleId === 'string'
    ? payload.agentRoleId
    : undefined;
  const legacyAgentKey = typeof payload.agent === 'string' ? payload.agent : undefined;
  const agentKey = projectAgentRoleId || agentRoleId || legacyAgentKey;
  if (!agentKey) {
    return;
  }

  applyProjectGroupIdentityToMessage(messages, event.messageId ?? undefined, agentKey);
}

function refreshProjectGroupMessageIdentities(messages: SessionConversationMessage[]) {
  if (props.scene !== 'ProjectGroup' || projectGroupIdentityLookup.value.size === 0) {
    return;
  }

  messages.forEach((message, index) => {
    if (message.role !== 'assistant' || !message.agentKey) {
      return;
    }

    const identity = resolveProjectGroupIdentity(message.role, message.agentKey);
    if (!('displayName' in identity) || !identity.displayName) {
      return;
    }

    messages.splice(index, 1, {
      ...message,
      avatar: identity.avatar,
      displayName: identity.displayName,
    });
  });
}

function buildProjectGroupMentions(input: string): ConversationMentionDto[] {
  if (props.scene !== 'ProjectGroup') {
    return [];
  }

  const matches = input.matchAll(/@([^\s,，:：]+)/g);
  const mentions: ConversationMentionDto[] = [];

  for (const match of matches) {
    const rawText = match[0]?.trim();
    const target = match[1]?.trim();
    if (!rawText || !target) {
      continue;
    }

    const normalizedTarget = normalizeAgentIdentityKey(target);
    if (!normalizedTarget) {
      mentions.push({ rawText });
      continue;
    }

    if (normalizedTarget === '所有人' || normalizedTarget === 'all' || normalizedTarget === '@all') {
      mentions.push({
        rawText,
        builtinRole: '所有人',
        resolvedKind: 'builtin_role',
      });
      continue;
    }

    if (normalizedTarget === 'secretary' || normalizedTarget === '秘书') {
      mentions.push({
        rawText,
        builtinRole: 'secretary',
        resolvedKind: 'builtin_role',
      });
      continue;
    }

    const matchedAgent = projectGroupMentionLookup.value.get(normalizedTarget);
    if (!matchedAgent) {
      mentions.push({ rawText });
      continue;
    }

    const isSecretary = normalizeAgentIdentityKey(matchedAgent.jobTitle) === 'secretary'
      || normalizeAgentIdentityKey(matchedAgent.role?.jobTitle) === 'secretary';

    mentions.push({
      rawText,
      builtinRole: isSecretary ? 'secretary' : undefined,
      projectAgentRoleId: matchedAgent.projectAgentRoleId,
      resolvedKind: matchedAgent.projectAgentRoleId ? 'project_agent_role' : (isSecretary ? 'builtin_role' : undefined),
    });
  }

  return mentions;
}

function buildProjectGroupMessagePayload(input: string) {
  const mentions = buildProjectGroupMentions(input);
  if (props.scene !== 'ProjectGroup') {
    return {
      executionInput: input,
      mentions,
      rawInput: undefined as string | undefined,
    };
  }

  const firstMention = mentions.find((mention) => typeof mention.rawText === 'string' && mention.rawText.trim().length > 0);
  if (!firstMention || !shouldStripProjectGroupMention(firstMention)) {
    return {
      executionInput: input,
      mentions,
      rawInput: input,
    };
  }

  return {
    executionInput: input,
    mentions,
    rawInput: input,
  };
}

function shouldStripProjectGroupMention(mention: ConversationMentionDto) {
  return mention.resolvedKind === 'project_agent_role'
    || normalizeAgentIdentityKey(mention.builtinRole) === 'secretary';
}

function normalizeTiming(
  timing: ChatMessageTimingDto | SessionConversationMessage['timing'] | null | undefined,
) {
  if (!timing) {
    return undefined;
  }

  const source = timing as {
    durationMs?: null | number | string;
    firstTokenMs?: null | number | string;
    totalMs?: null | number | string;
  };

  return {
    totalMs: normalizeMetric(source.totalMs ?? source.durationMs),
    firstTokenMs: normalizeMetric(source.firstTokenMs),
  };
}

function normalizeUsage(
  usage: ChatMessageUsageDto | SessionConversationMessage['usage'] | null | undefined,
) {
  if (!usage) {
    return undefined;
  }

  return {
    inputTokens: normalizeMetric(usage.inputTokens),
    outputTokens: normalizeMetric(usage.outputTokens),
    totalTokens: normalizeMetric(usage.totalTokens),
  };
}
</script>

<template>
  <div class="conversation-tab-shell">
    <a-alert
      v-if="!canAccessScene"
      show-icon
      type="info"
      :message="unavailableMessage"
      :description="unavailableDescription"
    />

    <template v-else>
      <a-alert
        v-if="!canInputScene && readOnlyMessage"
        show-icon
        type="info"
        :message="readOnlyMessage"
        :description="readOnlyDescription"
      />

      <section class="conversation-panel-shell">
        <AgentConversationPanel
          v-model="chatInput"
          :agent-name="conversationAgentName"
          :agent-avatar="conversationAgentAvatar"
          :empty-description="emptyDescription"
          :force-assistant-identity="props.scene === 'ProjectBrainstorm'"
          :hide-header-identity="props.scene === 'ProjectGroup'"
          :assistant-identity-mentions="props.scene === 'ProjectGroup'"
          :input-disabled="!canInputScene"
          :input-placeholder="canInputScene ? inputPlaceholder : (inputDisabledHint || inputPlaceholder)"
          :mention-options="props.scene === 'ProjectGroup' ? projectGroupMentionOptions : []"
          :messages="chatMessages"
          :sending="conversationSending"
          :send-disabled="!canInputScene"
          :subtitle="connected ? t('role.signalrConnected') : t('role.signalrConnecting')"
          :subtitle-status="connected ? 'online' : 'connecting'"
          @send="sendMessage"
        >
          <template #header-meta>
            <div v-if="projectRoleAvatars.length > 0" class="conversation-group-header">
              <div class="conversation-group-header-text">
                <div class="conversation-group-header-title">{{ projectGroupHeaderTitle }}</div>
                <div class="conversation-group-header-subtitle">{{ projectGroupHeaderSubtitle }}</div>
              </div>

              <div class="conversation-role-avatar-list">
                <a-popover
                  v-for="agent in projectRoleAvatars"
                  :key="agent.key"
                  placement="bottom"
                  trigger="hover"
                >
                  <template #content>
                    <div class="conversation-role-card">
                      <div class="conversation-role-card-header">
                        <a-avatar
                          :src="agent.avatar"
                          class="conversation-role-card-avatar"
                          :size="44"
                        >
                          {{ agent.label.slice(0, 1) || '角' }}
                        </a-avatar>
                        <div class="conversation-role-card-text">
                          <div class="conversation-role-card-name">{{ agent.label }}</div>
                          <div class="conversation-role-card-title">
                            {{ localizeJobTitle(agent.jobTitle, agent.jobTitle) || t('project.agentCardNoJobTitle') }}
                          </div>
                        </div>
                      </div>
                      <div class="conversation-role-card-actions">
                        <a-button
                          v-if="runtimePreviewEnabled"
                          size="small"
                          :disabled="!agent.projectAgentRoleId"
                          @click="openRuntimePreview(agent)"
                        >
                          {{ t('project.agentCardRuntimePreviewAction') }}
                        </a-button>
                        <a-button
                          size="small"
                          type="primary"
                          :disabled="!agent.role?.id"
                          @click="openRoleTestChat(agent)"
                        >
                          {{ t('role.testChat') }}
                        </a-button>
                        <a-button
                          size="small"
                          :disabled="!agent.projectAgentRoleId"
                          @click="openPrivateChat(agent)"
                        >
                          {{ t('project.privateChat') }}
                        </a-button>
                      </div>
                    </div>
                  </template>

                  <a-avatar
                    :src="agent.avatar"
                    class="conversation-role-avatar"
                    :size="40"
                  >
                    {{ agent.label.slice(0, 1) || '角' }}
                  </a-avatar>
                </a-popover>
              </div>
            </div>
          </template>
          <template #header-extra>
            <!-- 这里把取消动作收进头部右上角，避免单独占一整行压缩脑暴正文区域。 -->
            <a-button
              v-if="props.scene !== 'ProjectGroup'"
              danger
              size="small"
              type="text"
              :disabled="!currentSessionId || !canInputScene"
              @click="cancelSession"
            >
              {{ t('project.cancelSession') }}
            </a-button>
          </template>
        </AgentConversationPanel>
      </section>
    </template>

    <PermissionRequestModal
      :request="activePermissionRequest"
      :responding-request-id="permissionRespondingRequestId"
      @respond="respondToPermissionRequest"
    />

    <RoleWorkspace
      v-model:open="roleWorkspaceOpen"
      :providers="props.providers ?? []"
      :role="selectedHeaderAgent?.role ?? null"
    />

    <a-modal
      :open="runtimePreviewOpen"
      :title="runtimePreviewTitle"
      :footer="null"
      :width="960"
      destroy-on-close
      @cancel="closeRuntimePreview"
    >
      <a-spin :spinning="runtimePreviewLoading">
        <div v-if="runtimePreview" class="agent-runtime-preview-modal">
          <section class="agent-runtime-preview-section">
            <div class="agent-runtime-preview-section-title">
              {{ t('project.agentCardPromptPreview') }}
            </div>
            <pre class="agent-runtime-preview-block">{{ runtimePreview.prompt }}</pre>
          </section>

          <section class="agent-runtime-preview-section">
            <div class="agent-runtime-preview-section-title">
              {{ t('project.agentCardToolPreview') }}
            </div>

            <div v-if="runtimePreviewBuiltinTools.length > 0" class="agent-runtime-preview-group">
              <div class="agent-runtime-preview-group-title">
                {{ t('project.agentCardBuiltinTools') }}
              </div>
              <div class="agent-runtime-preview-grid">
                <div
                  v-for="tool in runtimePreviewBuiltinTools"
                  :key="`builtin-${tool.name}`"
                  class="agent-runtime-preview-item"
                >
                  <div class="agent-runtime-preview-item-title">{{ tool.name }}</div>
                  <div v-if="tool.description" class="agent-runtime-preview-item-description">
                    {{ tool.description }}
                  </div>
                </div>
              </div>
            </div>

            <div v-if="runtimePreviewMcpTools.length > 0" class="agent-runtime-preview-group">
              <div class="agent-runtime-preview-group-title">
                {{ t('project.agentCardMcpTools') }}
              </div>
              <div class="agent-runtime-preview-grid">
                <div
                  v-for="tool in runtimePreviewMcpTools"
                  :key="`mcp-${tool.name}`"
                  class="agent-runtime-preview-item"
                >
                  <div class="agent-runtime-preview-item-title">{{ tool.name }}</div>
                  <div v-if="tool.description" class="agent-runtime-preview-item-description">
                    {{ tool.description }}
                  </div>
                </div>
              </div>
            </div>

            <a-empty
              v-if="runtimePreviewBuiltinTools.length === 0 && runtimePreviewMcpTools.length === 0"
              :description="t('project.agentCardNoTools')"
            />
          </section>

          <section class="agent-runtime-preview-section">
            <div class="agent-runtime-preview-section-title">
              {{ t('project.agentCardSkillRuntime') }}
            </div>

            <div v-if="runtimePreviewSkills.length > 0" class="agent-runtime-preview-group">
              <div class="agent-runtime-preview-group-title">
                {{ t('project.agentCardResolvedSkills') }}
              </div>
              <div class="agent-runtime-preview-grid">
                <div
                  v-for="skill in runtimePreviewSkills"
                  :key="`${skill.installKey}-${skill.skillId}`"
                  class="agent-runtime-preview-item"
                >
                  <div class="agent-runtime-preview-item-title">{{ skill.displayName || skill.skillId }}</div>
                  <div class="agent-runtime-preview-item-meta">{{ skill.skillId }}</div>
                  <div class="agent-runtime-preview-item-description">{{ skill.directoryPath }}</div>
                </div>
              </div>
            </div>

            <div v-if="runtimePreviewMissingSkills.length > 0" class="agent-runtime-preview-group">
              <div class="agent-runtime-preview-group-title">
                {{ t('project.agentCardMissingSkillBindings') }}
              </div>
              <div class="agent-runtime-preview-grid">
                <div
                  v-for="skill in runtimePreviewMissingSkills"
                  :key="`${skill.skillInstallKey}-${skill.skillId}`"
                  class="agent-runtime-preview-item"
                >
                  <div class="agent-runtime-preview-item-title">{{ skill.displayName || skill.skillId }}</div>
                  <div class="agent-runtime-preview-item-meta">{{ skill.bindingScope }}</div>
                  <div class="agent-runtime-preview-item-description">{{ skill.message }}</div>
                </div>
              </div>
            </div>

            <a-empty
              v-if="runtimePreviewSkills.length === 0 && runtimePreviewMissingSkills.length === 0"
              :description="t('project.agentCardNoSkillRuntime')"
            />
          </section>
        </div>
      </a-spin>
    </a-modal>

    <a-modal
      :open="privateChatOpen"
      :title="privateChatTitle"
      :footer="null"
      :width="960"
      destroy-on-close
      @cancel="closePrivateChat"
    >
      <div class="private-chat-modal-body">
        <AgentConversationPanel
          v-model="privateChatInput"
          :agent-avatar="selectedHeaderAgent?.avatar"
          :agent-name="selectedHeaderAgent?.label || t('project.privateChat')"
          :clearable="true"
          :clear-disabled="!privateChatMessages.length && !privateChatLoading"
          :empty-description="t('project.privateChatEmpty')"
          :input-placeholder="t('project.privateChatPlaceholder')"
          :messages="privateChatMessages"
          :send-disabled="!selectedHeaderAgent?.projectAgentRoleId"
          :sending="privateChatLoading"
          :subtitle="localizeJobTitle(selectedHeaderAgent?.jobTitle, selectedHeaderAgent?.jobTitle) || undefined"
          @clear="resetPrivateChat()"
          @send="sendPrivateMessage"
        />
      </div>
    </a-modal>
  </div>
</template>

<style scoped>
.conversation-tab-shell {
  height: 100%;
  display: flex;
  min-height: 0;
  flex: 1;
  flex-direction: column;
  gap: 1rem;
}

.conversation-panel-shell {
  min-height: 0;
  flex: 1;
  overflow: hidden;
  border-radius: 1rem;
  border: 1px solid hsl(var(--border) / 0.7);
  background: hsl(var(--background) / 0.75);
}

.conversation-role-avatar-list {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: flex-end;
  gap: 10px;
}

.conversation-group-header {
  width: 100%;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 20px;
}

.conversation-group-header-text {
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.conversation-group-header-title {
  font-size: 21px;
  font-weight: 700;
  line-height: 1.25;
  color: hsl(var(--foreground));
}

.conversation-group-header-subtitle {
  font-size: 14px;
  color: hsl(var(--muted-foreground));
}

.conversation-role-avatar {
  border: 2px solid hsl(var(--background));
  box-shadow: 0 0 0 1px hsl(var(--border) / 0.45), 0 8px 18px -16px rgb(15 23 42 / 0.55);
  cursor: pointer;
}

.conversation-role-card {
  min-width: 260px;
}

.conversation-role-card-header {
  display: flex;
  align-items: center;
  gap: 12px;
}

.conversation-role-card-avatar {
  flex-shrink: 0;
  box-shadow: 0 8px 20px -16px rgb(15 23 42 / 0.55);
}

.conversation-role-card-text {
  min-width: 0;
}

.conversation-role-card-name {
  font-size: 15px;
  font-weight: 600;
  color: hsl(var(--foreground));
}

.conversation-role-card-title {
  margin-top: 4px;
  font-size: 13px;
  color: hsl(var(--muted-foreground));
}

.conversation-role-card-actions {
  margin-top: 12px;
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.agent-runtime-preview-modal {
  display: flex;
  flex-direction: column;
  gap: 18px;
}

.agent-runtime-preview-section {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.agent-runtime-preview-section-title {
  font-size: 15px;
  font-weight: 600;
  color: hsl(var(--foreground));
}

.agent-runtime-preview-group {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.agent-runtime-preview-group-title {
  font-size: 13px;
  font-weight: 500;
  color: hsl(var(--muted-foreground));
}

.agent-runtime-preview-grid {
  display: grid;
  gap: 12px;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
}

.agent-runtime-preview-item {
  padding: 12px;
  border: 1px solid hsl(var(--border));
  border-radius: 12px;
  background: hsl(var(--card));
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.agent-runtime-preview-item-title {
  font-size: 14px;
  font-weight: 600;
  color: hsl(var(--foreground));
  word-break: break-word;
}

.agent-runtime-preview-item-meta {
  font-size: 12px;
  color: hsl(var(--muted-foreground));
  word-break: break-word;
}

.agent-runtime-preview-item-description {
  font-size: 12px;
  color: hsl(var(--muted-foreground));
  white-space: pre-wrap;
  word-break: break-word;
}

.agent-runtime-preview-block {
  margin: 0;
  padding: 14px;
  min-height: 240px;
  max-height: 420px;
  overflow: auto;
  border: 1px solid hsl(var(--border));
  border-radius: 12px;
  background: hsl(var(--muted) / 0.35);
  color: hsl(var(--foreground));
  font-size: 12px;
  line-height: 1.65;
  white-space: pre-wrap;
  word-break: break-word;
}

.private-chat-modal-body {
  height: 70vh;
  min-height: 520px;
}

.private-chat-modal-body :deep(.conversation-panel) {
  min-height: 0;
  height: 100%;
}
</style>
