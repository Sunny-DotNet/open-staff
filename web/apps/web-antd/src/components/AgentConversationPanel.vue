<script setup lang="ts">
import type {
  AgentConversationMessage,
  AgentConversationResponseStep,
  AgentConversationStep,
  AgentConversationThinkingStep,
  AgentConversationToolStep,
} from './agent-conversation';
import type {
  ConversationMentionOption as AgentConversationMentionOption,
} from './conversation-mentions';
import type { ComponentPublicInstance } from 'vue';

import { MdPreview } from 'md-editor-v3';
import { computed, nextTick, ref, watch } from 'vue';

import { Avatar, Button, Empty, Input, Spin, Tooltip } from 'ant-design-vue';
import 'md-editor-v3/lib/preview.css';

import { useThemeMode } from '@/composables/useThemeMode';

import {
  appendMentionShortcut,
  filterMentionOptions,
  insertMentionValue,
  normalizeMentionSearchKey,
  resolveMentionRange,
} from './conversation-mentions';

const TextArea = Input.TextArea;

type AgentConversationInputMode = 'input' | 'textarea';

interface AgentConversationPanelProps {
  title?: string;
  subtitle?: string;
  subtitleStatus?: 'connecting' | 'default' | 'online';
  agentName: string;
  agentAvatar?: null | string;
  hideHeaderIdentity?: boolean;
  forceAssistantIdentity?: boolean;
  messages: AgentConversationMessage[];
  loading?: boolean;
  sending?: boolean;
  sendingLabel?: string;
  modelValue?: string;
  inputPlaceholder?: string;
  inputDisabled?: boolean;
  inputMode?: AgentConversationInputMode;
  textareaMinRows?: number;
  textareaMaxRows?: number;
  sendDisabled?: boolean;
  clearable?: boolean;
  clearDisabled?: boolean;
  assistantIdentityMentions?: boolean;
  emptyDescription?: string;
  mentionOptions?: AgentConversationMentionOption[];
}

interface AgentConversationPanelEmits {
  (event: 'clear'): void;
  (event: 'send'): void;
  (event: 'update:modelValue', value: string): void;
}

const props = withDefaults(defineProps<AgentConversationPanelProps>(), {
  subtitle: undefined,
  subtitleStatus: 'default',
  agentAvatar: undefined,
  hideHeaderIdentity: false,
  forceAssistantIdentity: false,
  loading: false,
  sending: false,
  sendingLabel: '思考中',
  modelValue: '',
  inputPlaceholder: '输入消息…',
  inputDisabled: false,
  inputMode: 'textarea',
  textareaMinRows: 1,
  textareaMaxRows: 4,
  sendDisabled: false,
  clearable: false,
  clearDisabled: false,
  assistantIdentityMentions: false,
  emptyDescription: '暂无对话',
  mentionOptions: () => [],
});

const emit = defineEmits<AgentConversationPanelEmits>();
const { isDarkMode } = useThemeMode();

const scrollContainerRef = ref<HTMLElement | null>(null);
const inputControlRef = ref<ComponentPublicInstance | HTMLElement | null>(null);
const messageElementRefs = ref<Record<string, HTMLElement>>({});
const messageExpandedState = ref<Record<string, boolean>>({});
const stepExpandedState = ref<Record<string, Record<string, boolean>>>({});
const previousMessageRunningState = ref<Record<string, boolean>>({});
const previousLatestStepIdState = ref<Record<string, null | string>>({});
const activeMentionIndex = ref(0);
const activeMentionRange = ref<null | { end: number; query: string; start: number }>(null);

const headerTitle = computed(() => props.title || props.agentName);
const headerSubtitle = computed(() => props.subtitle);
const headerSubtitleStatus = computed(() => props.subtitleStatus);
const normalizedAgentAvatar = computed(() => props.agentAvatar ?? undefined);
const canSend = computed(
  () => !props.inputDisabled && !props.sendDisabled && !!props.modelValue.trim(),
);
const showSendingIndicator = computed(
  () =>
    props.sending
    && !props.messages.some(
      (message) =>
        message.role === 'assistant' && (message.streaming || message.thinkingStreaming),
    ),
);
const markdownTheme = computed(() => (isDarkMode.value ? 'dark' : 'light'));
const markdownPreviewTheme = computed(() => (isDarkMode.value ? 'dark' : 'github'));
const markdownCodeTheme = computed(() => (isDarkMode.value ? 'atom' : 'github'));
const tokenCountFormatter = new Intl.NumberFormat('en-US');
const mentionCandidates = computed(() => {
  if (!activeMentionRange.value) {
    return [];
  }

  const normalizedQuery = normalizeMentionSearchKey(activeMentionRange.value.query);
  if (!normalizedQuery) {
    return props.mentionOptions;
  }

  return filterMentionOptions(props.mentionOptions, activeMentionRange.value.query);
});
const showMentionSuggestions = computed(
  () => !!activeMentionRange.value && mentionCandidates.value.length > 0 && !props.inputDisabled,
);

watch(
  () => props.messages,
  (messages) => {
    const nextExpandedState: Record<string, boolean> = {};
    const nextStepExpandedState: Record<string, Record<string, boolean>> = {};
    const nextRunningState: Record<string, boolean> = {};
    const nextLatestStepIdState: Record<string, null | string> = {};
    let completedMessageKey: null | string = null;

    for (const [index, message] of messages.entries()) {
      if (!hasAssistantSteps(message)) {
        continue;
      }

      const messageKey = getMessageKey(message, index);
      const isRunning = isAssistantMessageRunning(message);
      const wasRunning = previousMessageRunningState.value[messageKey] ?? false;
      const steps = message.steps ?? [];
      const latestStepId = steps.at(-1)?.id ?? null;
      const previousStepExpanded = stepExpandedState.value[messageKey] ?? {};
      const previousLatestStepId = previousLatestStepIdState.value[messageKey] ?? null;
      const hasNewLatestStep = !!latestStepId && latestStepId !== previousLatestStepId;

      nextExpandedState[messageKey] = isRunning
        ? true
        : wasRunning
          ? false
          : messageExpandedState.value[messageKey] ?? false;

      nextStepExpandedState[messageKey] = Object.fromEntries(
        steps.map((step) => {
          if (isRunning) {
            if (hasNewLatestStep) {
              return [step.id, step.id === latestStepId];
            }

            return [step.id, previousStepExpanded[step.id] ?? step.id === latestStepId];
          }

          if (wasRunning) {
            return [step.id, false];
          }

          return [step.id, previousStepExpanded[step.id] ?? false];
        }),
      );
      if (wasRunning && !isRunning) {
        completedMessageKey = messageKey;
      }

      nextRunningState[messageKey] = isRunning;
      nextLatestStepIdState[messageKey] = latestStepId;
    }

    if (!completedMessageKey) {
      const lastMessageIndex = messages.length - 1;
      const lastMessage = messages[lastMessageIndex];
      if (
        lastMessage
        && hasAssistantSteps(lastMessage)
        && !isAssistantMessageRunning(lastMessage)
      ) {
        completedMessageKey = getMessageKey(lastMessage, lastMessageIndex);
      }
    }

    messageExpandedState.value = nextExpandedState;
    stepExpandedState.value = nextStepExpandedState;
    previousMessageRunningState.value = nextRunningState;
    previousLatestStepIdState.value = nextLatestStepIdState;
    if (completedMessageKey) {
      scrollMessageIntoView(completedMessageKey);
      return;
    }

    scrollToBottom();
  },
  { deep: true, immediate: true },
);

watch(() => props.loading, (loading) => {
  if (loading) {
    scrollToBottom();
  }
});
watch(() => props.sending, (sending) => {
  if (sending) {
    scrollToBottom();
  }
});
watch(
  () => props.modelValue,
  () => {
    scheduleMentionSync();
  },
);
watch(
  () => mentionCandidates.value,
  (candidates) => {
    if (!candidates.length) {
      activeMentionIndex.value = 0;
      return;
    }

    activeMentionIndex.value = Math.min(activeMentionIndex.value, candidates.length - 1);
  },
  { deep: true },
);

function updateInput(value: string) {
  emit('update:modelValue', value);
  syncMentionRangeForValue(value, getInputElement()?.selectionStart ?? value.length);
  scheduleMentionSync();
}

function emitSend() {
  if (!canSend.value) {
    return;
  }

  emit('send');
}

function onInputKeydown(event: KeyboardEvent) {
  if (showMentionSuggestions.value) {
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      activeMentionIndex.value = (activeMentionIndex.value + 1) % mentionCandidates.value.length;
      return;
    }

    if (event.key === 'ArrowUp') {
      event.preventDefault();
      activeMentionIndex.value =
        (activeMentionIndex.value - 1 + mentionCandidates.value.length) % mentionCandidates.value.length;
      return;
    }

    if (event.key === 'Enter' || event.key === 'Tab') {
      const candidate = mentionCandidates.value[activeMentionIndex.value];
      if (candidate) {
        event.preventDefault();
        applyMentionCandidate(candidate);
        return;
      }
    }

    if (event.key === 'Escape') {
      event.preventDefault();
      closeMentionSuggestions();
      return;
    }
  }

  if (event.key !== 'Enter') {
    return;
  }

  if (props.inputMode === 'textarea' && event.shiftKey) {
    return;
  }

  event.preventDefault();
  emitSend();
}

function onInputCursorChange() {
  scheduleMentionSync();
}

function onInputBlur() {
  window.setTimeout(() => {
    closeMentionSuggestions();
  }, 0);
}

function applyMentionCandidate(candidate: AgentConversationMentionOption) {
  const range = activeMentionRange.value;
  if (!range) {
    return;
  }

  const nextMention = insertMentionValue(props.modelValue, range, candidate);

  emit('update:modelValue', nextMention.value);
  closeMentionSuggestions();
  nextTick(() => {
    setInputSelectionRange(nextMention.cursorPosition);
  });
}

function scheduleMentionSync() {
  nextTick(() => {
    syncMentionRange();
  });
}

function syncMentionRange() {
  if (!props.mentionOptions.length) {
    activeMentionRange.value = null;
    return;
  }

  const inputElement = getInputElement();
  syncMentionRangeForValue(props.modelValue, inputElement?.selectionStart ?? props.modelValue.length);
}

function closeMentionSuggestions() {
  activeMentionRange.value = null;
  activeMentionIndex.value = 0;
}

function syncMentionRangeForValue(value: string, caret: number) {
  activeMentionRange.value = props.mentionOptions.length
    ? resolveMentionRange(value, caret)
    : null;
  if (!activeMentionRange.value) {
    activeMentionIndex.value = 0;
  }
}

function getInputElement(): HTMLInputElement | HTMLTextAreaElement | null {
  const target = inputControlRef.value;
  if (!target) {
    return null;
  }

  if (target instanceof HTMLInputElement || target instanceof HTMLTextAreaElement) {
    return target;
  }

  if (target instanceof HTMLElement) {
    const input = target.querySelector('input, textarea');
    return input instanceof HTMLInputElement || input instanceof HTMLTextAreaElement ? input : null;
  }

  if ('$el' in target && target.$el instanceof HTMLElement) {
    const input = target.$el.querySelector('input, textarea');
    return input instanceof HTMLInputElement || input instanceof HTMLTextAreaElement ? input : null;
  }

  return null;
}

function setInputSelectionRange(position: number) {
  const inputElement = getInputElement();
  if (!inputElement) {
    return;
  }

  inputElement.focus();
  inputElement.setSelectionRange(position, position);
}

function scrollToBottom() {
  nextTick(() => {
    scrollContainerRef.value?.scrollTo({
      top: scrollContainerRef.value.scrollHeight,
      behavior: 'smooth',
    });
  });
}

function scrollMessageIntoView(messageKey: string) {
  nextTick(() => {
    const container = scrollContainerRef.value;
    const messageElement = messageElementRefs.value[messageKey];
    if (!container || !messageElement) {
      scrollToBottom();
      return;
    }

    const containerRect = container.getBoundingClientRect();
    const messageRect = messageElement.getBoundingClientRect();
    const nextTop = container.scrollTop + (messageRect.top - containerRect.top) - 8;

    container.scrollTo({
      top: Math.max(nextTop, 0),
      behavior: 'smooth',
    });
  });
}

function resolveName(message: AgentConversationMessage) {
  if (message.role === 'user') {
    return message.displayName || '我';
  }

  if (props.forceAssistantIdentity) {
    return props.agentName;
  }

  return message.displayName || props.agentName;
}

function resolveAvatar(message: AgentConversationMessage) {
  if (message.role === 'assistant' && props.forceAssistantIdentity) {
    return normalizedAgentAvatar.value;
  }

  return (
    message.avatar
    ?? (message.role === 'assistant' ? normalizedAgentAvatar.value : undefined)
    ?? undefined
  );
}

function fallbackAvatarLabel(message: AgentConversationMessage) {
  const label = resolveName(message).trim();
  return label.slice(0, 1) || (message.role === 'assistant' ? '助' : '我');
}

function resolveMentionShortcutLabel(message: AgentConversationMessage) {
  if (message.role !== 'assistant') {
    return '';
  }

  return resolveName(message).trim();
}

function canInsertAssistantMention(message: AgentConversationMessage) {
  return props.assistantIdentityMentions
    && message.role === 'assistant'
    && !!resolveMentionShortcutLabel(message);
}

function insertAssistantMention(message: AgentConversationMessage) {
  const label = resolveMentionShortcutLabel(message);
  if (!label) {
    return;
  }

  const nextMention = appendMentionShortcut(props.modelValue, label);
  emit('update:modelValue', nextMention.value);
  nextTick(() => {
    setInputSelectionRange(nextMention.cursorPosition);
  });
}

function formatDuration(ms?: number) {
  if (ms == null) {
    return '';
  }

  return ms >= 1000 ? `${(ms / 1000).toFixed(1)}s` : `${ms}ms`;
}

function formatTokenCount(value?: number) {
  if (value == null) {
    return '';
  }

  return tokenCountFormatter.format(value);
}

function resolveUsageSummary(message: AgentConversationMessage) {
  const usage = message.usage;
  if (!usage) {
    return '--';
  }

  if (usage.totalTokens != null) {
    const segments = [`总 ${formatTokenCount(usage.totalTokens)} tokens`];
    if (usage.inputTokens != null) {
      segments.push(`输入 ${formatTokenCount(usage.inputTokens)}`);
    }
    if (usage.outputTokens != null) {
      segments.push(`输出 ${formatTokenCount(usage.outputTokens)}`);
    }
    return segments.join(' / ');
  }

  if (usage.inputTokens != null || usage.outputTokens != null) {
    return [
      usage.inputTokens != null ? `输入 ${formatTokenCount(usage.inputTokens)}` : null,
      usage.outputTokens != null ? `输出 ${formatTokenCount(usage.outputTokens)}` : null,
    ].filter(Boolean).join(' / ');
  }

  return '--';
}

function resolveTimingSummary(message: AgentConversationMessage) {
  const timing = message.timing;
  if (!timing) {
    return '--';
  }

  if (timing.totalMs != null || timing.firstTokenMs != null) {
    return [
      timing.totalMs != null ? `总 ${formatDuration(timing.totalMs)}` : null,
      timing.firstTokenMs != null ? `首 token ${formatDuration(timing.firstTokenMs)}` : null,
    ].filter(Boolean).join(' / ');
  }

  return '--';
}

function resolveUsageValue(message: AgentConversationMessage) {
  const usage = message.usage;
  if (usage?.totalTokens != null) {
    return formatTokenCount(usage.totalTokens);
  }

  if (usage?.inputTokens != null || usage?.outputTokens != null) {
    return [
      usage.inputTokens != null ? `入 ${formatTokenCount(usage.inputTokens)}` : null,
      usage.outputTokens != null ? `出 ${formatTokenCount(usage.outputTokens)}` : null,
    ].filter(Boolean).join(' / ');
  }

  return '--';
}

function resolveTimingValue(message: AgentConversationMessage) {
  const timing = message.timing;
  if (timing?.totalMs != null) {
    return formatDuration(timing.totalMs);
  }

  if (timing?.firstTokenMs != null) {
    return formatDuration(timing.firstTokenMs);
  }

  return '--';
}

function hasAssistantSteps(message: AgentConversationMessage) {
  return message.role === 'assistant' && !!message.steps?.length;
}

function shouldRenderBubble(message: AgentConversationMessage) {
  if (hasAssistantSteps(message)) {
    return true;
  }

  return !!message.content || message.thinkingStreaming || message.streaming;
}

function isThinkingStep(step: AgentConversationStep): step is AgentConversationThinkingStep {
  return step.kind === 'thinking';
}

function isToolStep(step: AgentConversationStep): step is AgentConversationToolStep {
  return step.kind === 'tool_call';
}

function isResponseStep(step: AgentConversationStep): step is AgentConversationResponseStep {
  return step.kind === 'response';
}

function resolveStepKindLabel(step?: AgentConversationStep) {
  if (!step) {
    return '思考';
  }

  if (isThinkingStep(step)) {
    return '思考';
  }

  if (isToolStep(step)) {
    return '使用工具';
  }

  return step.appearance === 'error' ? '错误' : '回复';
}

function formatStructuredValue(value: unknown) {
  if (value == null) {
    return '';
  }

  if (typeof value === 'string') {
    const trimmed = value.trim();
    if (!trimmed) {
      return '';
    }

    try {
      return JSON.stringify(JSON.parse(trimmed), null, 2);
    } catch {
      return value;
    }
  }

  return JSON.stringify(value, null, 2);
}

function getMessageKey(message: AgentConversationMessage, index: number) {
  return message.id ?? `${message.role}-${message.timestamp ?? index}`;
}

function setMessageElement(
  message: AgentConversationMessage,
  index: number,
  element: ComponentPublicInstance | Element | null,
) {
  const messageKey = getMessageKey(message, index);
  const resolvedElement =
    element instanceof HTMLElement
      ? element
      : element && '$el' in element && element.$el instanceof HTMLElement
        ? element.$el
        : null;

  if (!resolvedElement) {
    delete messageElementRefs.value[messageKey];
    return;
  }

  messageElementRefs.value[messageKey] = resolvedElement;
}

function getPreviewId(message: AgentConversationMessage, suffix: string) {
  return `conversation-preview-${message.id ?? 'message'}-${suffix}`;
}

function isAssistantMessageRunning(message: AgentConversationMessage) {
  if (!hasAssistantSteps(message)) {
    return false;
  }

  if (message.streaming || message.thinkingStreaming) {
    return true;
  }

  return !!message.steps?.some((step) => 'streaming' in step && step.streaming);
}

function hasAssistantStepError(message: AgentConversationMessage) {
  return (message.steps ?? []).some(
    (step) =>
      (isToolStep(step) && step.status === 'error')
      || (isResponseStep(step) && step.appearance === 'error'),
  );
}

function resolveMessageSummary(message: AgentConversationMessage) {
  const steps = message.steps ?? [];
  if (!steps.length) {
    return '思考中';
  }

  if (isAssistantMessageRunning(message)) {
    return `第${steps.length}步，正在${resolveStepKindLabel(steps.at(-1))}中`;
  }

  if (hasAssistantStepError(message)) {
    return `❌ 已失败，共${steps.length}步`;
  }

  return `✅ 已完成，共${steps.length}步`;
}

function resolveMessageSummaryHint(
  message: AgentConversationMessage,
  expanded: boolean,
) {
  if (expanded) {
    return '收起过程详情';
  }

  return isAssistantMessageRunning(message) ? '展开查看实时过程' : '展开过程详情';
}

function resolveLatestResponseStep(message: AgentConversationMessage) {
  const steps = message.steps ?? [];
  for (let index = steps.length - 1; index >= 0; index -= 1) {
    const step = steps[index];
    if (step && isResponseStep(step) && step.content.trim()) {
      return step;
    }
  }

  return undefined;
}

function shouldShowCollapsedResponse(message: AgentConversationMessage, index: number) {
  return !isAssistantMessageRunning(message)
    && !isMessageExpanded(message, index)
    && !!resolveLatestResponseStep(message);
}

function resolveStepSummary(step: AgentConversationStep) {
  if (isToolStep(step)) {
    return `${resolveStepKindLabel(step)} · ${step.name}`;
  }

  return resolveStepKindLabel(step);
}

function resolveStepStatusLabel(step: AgentConversationStep) {
  if (isToolStep(step)) {
    if (step.status === 'calling') {
      return '进行中';
    }

    return step.status === 'error' ? '失败' : '完成';
  }

  if (isResponseStep(step) && step.appearance === 'error') {
    return '失败';
  }

  return 'streaming' in step && step.streaming ? '进行中' : '完成';
}

function resolveStepStatusClass(step: AgentConversationStep) {
  if (isToolStep(step)) {
    if (step.status === 'calling') {
      return 'is-running';
    }

    return step.status === 'error' ? 'is-error' : 'is-done';
  }

  if (isResponseStep(step) && step.appearance === 'error') {
    return 'is-error';
  }

  return 'streaming' in step && step.streaming ? 'is-running' : 'is-done';
}

function isMessageExpanded(message: AgentConversationMessage, index: number) {
  return messageExpandedState.value[getMessageKey(message, index)] ?? false;
}

function toggleMessageExpanded(message: AgentConversationMessage, index: number) {
  const messageKey = getMessageKey(message, index);
  messageExpandedState.value[messageKey] = !isMessageExpanded(message, index);
}

function isStepExpanded(
  message: AgentConversationMessage,
  index: number,
  step: AgentConversationStep,
) {
  return stepExpandedState.value[getMessageKey(message, index)]?.[step.id] ?? false;
}

function toggleStepExpanded(
  message: AgentConversationMessage,
  index: number,
  step: AgentConversationStep,
) {
  const messageKey = getMessageKey(message, index);
  stepExpandedState.value[messageKey] = {
    ...(stepExpandedState.value[messageKey] ?? {}),
    [step.id]: !isStepExpanded(message, index, step),
  };
}
</script>

<template>
  <div class="conversation-panel">
    <div class="conversation-header">
      <div class="conversation-header-main">
        <template v-if="!hideHeaderIdentity">
          <Avatar :src="normalizedAgentAvatar" class="conversation-header-avatar">
            {{ agentName.slice(0, 1) || '助' }}
          </Avatar>
          <div class="conversation-header-text">
            <div class="conversation-title">
              {{ headerTitle }}
            </div>
            <div
              v-if="headerSubtitle"
              :class="['conversation-subtitle', `is-${headerSubtitleStatus}`]"
            >
              <span class="conversation-subtitle-dot" />
              {{ headerSubtitle }}
            </div>
            <div v-if="$slots['header-meta']" class="conversation-header-meta">
              <slot name="header-meta" />
            </div>
          </div>
        </template>
        <div
          v-else-if="$slots['header-meta']"
          class="conversation-header-meta conversation-header-meta-standalone"
        >
          <slot name="header-meta" />
        </div>
      </div>
      <slot name="header-extra" />
    </div>

    <div v-if="$slots.banner" class="conversation-banner">
      <slot name="banner" />
    </div>

    <div class="conversation-content">
      <Spin :spinning="loading" class="conversation-spin">
        <div ref="scrollContainerRef" class="conversation-messages">
          <slot v-if="messages.length === 0" name="empty">
            <div class="conversation-empty">
              <Empty :description="emptyDescription" />
            </div>
          </slot>

          <template v-else>
            <div
              v-for="(message, index) in messages"
              :key="message.id ?? `${message.role}-${index}`"
              :class="['message-row', message.role]"
              :ref="(element) => setMessageElement(message, index, element)"
            >
              <button
                v-if="canInsertAssistantMention(message)"
                class="message-identity-trigger"
                type="button"
                @click="insertAssistantMention(message)"
              >
                <Avatar
                  :src="resolveAvatar(message)"
                  class="message-avatar"
                >
                  {{ fallbackAvatarLabel(message) }}
                </Avatar>
              </button>
              <Avatar
                v-else-if="message.role === 'assistant'"
                :src="resolveAvatar(message)"
                class="message-avatar"
              >
                {{ fallbackAvatarLabel(message) }}
              </Avatar>

              <div :class="['message-column', message.role]">
                <button
                  v-if="canInsertAssistantMention(message)"
                  class="message-name message-name-trigger"
                  type="button"
                  @click="insertAssistantMention(message)"
                >
                  {{ resolveName(message) }}
                </button>
                <div
                  v-else-if="message.role === 'assistant'"
                  class="message-name"
                >
                  {{ resolveName(message) }}
                </div>

                <div
                  v-if="shouldRenderBubble(message)"
                  :class="[
                    'message-bubble',
                    message.role,
                    { 'assistant-steps-bubble': hasAssistantSteps(message) },
                  ]"
                >
                  <template v-if="hasAssistantSteps(message)">
                    <div
                      :class="[
                        'assistant-run-card',
                        {
                          'is-running': isAssistantMessageRunning(message),
                          'is-error': hasAssistantStepError(message),
                        },
                      ]"
                    >
                      <button
                        class="assistant-run-toggle"
                        type="button"
                        @click="toggleMessageExpanded(message, index)"
                      >
                        <div
                          :class="[
                            'assistant-run-status-dot',
                            {
                              'is-running': isAssistantMessageRunning(message),
                              'is-error': hasAssistantStepError(message),
                            },
                          ]"
                        />
                        <div
                          class="assistant-run-main"
                        >
                          <span
                            :class="[
                              'assistant-run-caret',
                              { 'is-expanded': isMessageExpanded(message, index) },
                            ]"
                          >
                            ▶
                          </span>
                          <span class="assistant-run-title">{{ resolveMessageSummary(message) }}</span>
                        </div>
                        <div class="assistant-run-hint">
                          {{ resolveMessageSummaryHint(message, isMessageExpanded(message, index)) }}
                        </div>
                      </button>

                      <div
                        v-if="shouldShowCollapsedResponse(message, index)"
                        class="assistant-run-preview"
                      >
                        <div class="assistant-response-block assistant-run-preview-content">
                          <MdPreview
                            :id="getPreviewId(message, 'collapsed-response')"
                            class="assistant-md-preview"
                            :code-theme="markdownCodeTheme"
                            language="zh-CN"
                            :code-foldable="false"
                            :model-value="resolveLatestResponseStep(message)?.content ?? ''"
                            :no-katex="true"
                            :no-mermaid="true"
                            :preview-theme="markdownPreviewTheme"
                            :theme="markdownTheme"
                          />
                        </div>
                      </div>

                      <div
                        v-if="isMessageExpanded(message, index)"
                        class="assistant-run-body"
                      >
                        <div class="assistant-step-list">
                          <section
                            v-for="(step, stepIndex) in message.steps"
                            :key="step.id"
                            :class="[
                              'assistant-step',
                              `assistant-step-${step.kind}`,
                              {
                                'is-error': isResponseStep(step) && step.appearance === 'error',
                              },
                            ]"
                          >
                            <button
                              class="assistant-step-toggle"
                              type="button"
                              @click="toggleStepExpanded(message, index, step)"
                            >
                              <div class="assistant-step-heading-main">
                                <span
                                  :class="[
                                    'assistant-step-caret',
                                    {
                                      'is-expanded': isStepExpanded(message, index, step),
                                    },
                                  ]"
                                >
                                  ▶
                                </span>
                                <span class="assistant-step-index">第{{ stepIndex + 1 }}步</span>
                                <span class="assistant-step-label">{{ resolveStepSummary(step) }}</span>
                              </div>
                              <span
                                :class="[
                                  'assistant-step-status-pill',
                                  resolveStepStatusClass(step),
                                ]"
                              >
                                {{ resolveStepStatusLabel(step) }}
                              </span>
                            </button>

                            <div
                              v-if="isStepExpanded(message, index, step)"
                              class="assistant-step-body"
                            >
                              <template v-if="isToolStep(step)">
                                <div class="assistant-step-tool-name">
                                  {{ step.name }}
                                </div>
                                <div
                                  v-if="step.arguments"
                                  class="assistant-step-section"
                                >
                                  <div class="assistant-step-section-label">
                                    参数
                                  </div>
                                  <pre class="assistant-step-code"><code>{{ formatStructuredValue(step.arguments) }}</code></pre>
                                </div>
                                <div
                                  v-if="step.result"
                                  class="assistant-step-section"
                                >
                                  <div class="assistant-step-section-label">
                                    结果
                                  </div>
                                  <pre class="assistant-step-code assistant-step-result"><code>{{ formatStructuredValue(step.result) }}</code></pre>
                                </div>
                                <div
                                  v-if="step.error"
                                  class="assistant-step-section"
                                >
                                  <div class="assistant-step-section-label">
                                    错误
                                  </div>
                                  <pre class="assistant-step-code assistant-step-error"><code>{{ formatStructuredValue(step.error) }}</code></pre>
                                </div>
                              </template>

                              <div
                                v-else-if="isResponseStep(step)"
                                class="assistant-response-block"
                              >
                                <MdPreview
                                  :id="getPreviewId(message, step.id)"
                                  class="assistant-md-preview"
                                  :code-theme="markdownCodeTheme"
                                  language="zh-CN"
                                  :code-foldable="false"
                                  :model-value="step.content"
                                  :no-katex="true"
                                  :no-mermaid="true"
                                  :preview-theme="markdownPreviewTheme"
                                  :theme="markdownTheme"
                                />
                              </div>

                              <div
                                v-else
                                class="assistant-step-content"
                              >
                                {{ step.content }}
                              </div>

                              <span
                                v-if="'streaming' in step && step.streaming"
                                class="streaming-cursor"
                              >
                                ▌
                              </span>
                            </div>
                          </section>
                        </div>
                      </div>
                    </div>
                  </template>

                  <template v-else-if="message.role === 'assistant' && message.content">
                    <MdPreview
                      :id="getPreviewId(message, 'message')"
                      class="assistant-md-preview"
                      :code-theme="markdownCodeTheme"
                      language="zh-CN"
                      :code-foldable="false"
                      :model-value="message.content"
                      :no-katex="true"
                      :no-mermaid="true"
                      :preview-theme="markdownPreviewTheme"
                      :theme="markdownTheme"
                    />
                    <span
                      v-if="message.streaming"
                      class="streaming-cursor"
                    >
                      ▌
                    </span>
                  </template>

                  <template v-else-if="message.content">
                    {{ message.content }}
                    <span
                      v-if="message.streaming"
                      class="streaming-cursor"
                    >
                      ▌
                    </span>
                  </template>

                  <template v-else-if="message.thinkingStreaming || message.streaming">
                    <span class="message-placeholder">思考中</span>
                  </template>
                </div>

                <div
                  v-if="message.role === 'assistant'"
                  class="message-meta"
                >
                  <span
                    v-if="message.model"
                    class="message-meta-chip message-meta-chip--model"
                  >
                    <span class="message-meta-icon" aria-hidden="true">🤖</span>
                    <span class="message-meta-text">{{ message.model }}</span>
                  </span>
                  <Tooltip :title="resolveUsageSummary(message)">
                    <span class="message-meta-chip message-meta-chip--usage">
                      <span class="message-meta-icon" aria-hidden="true">📊</span>
                      <span class="message-meta-text">{{ resolveUsageValue(message) }}</span>
                    </span>
                  </Tooltip>
                  <Tooltip :title="resolveTimingSummary(message)">
                    <span class="message-meta-chip message-meta-chip--timing">
                      <span class="message-meta-icon" aria-hidden="true">⏱️</span>
                      <span class="message-meta-text">{{ resolveTimingValue(message) }}</span>
                    </span>
                  </Tooltip>
                </div>
              </div>
            </div>
          </template>

          <div
            v-if="showSendingIndicator"
            class="message-row assistant"
          >
            <Avatar :src="normalizedAgentAvatar" class="message-avatar">
              {{ agentName.slice(0, 1) || '助' }}
            </Avatar>
            <div class="message-column assistant">
              <div class="message-name">
                {{ agentName }}
              </div>
              <div class="message-bubble assistant">
                <span class="message-placeholder">{{ sendingLabel }}</span>
              </div>
            </div>
          </div>
        </div>
      </Spin>
    </div>

    <div class="conversation-input">
      <div class="conversation-input-main">
        <TextArea
          v-if="inputMode === 'textarea'"
          ref="inputControlRef"
          :value="modelValue"
          :auto-size="{ minRows: textareaMinRows, maxRows: textareaMaxRows }"
          :disabled="inputDisabled"
          :placeholder="inputPlaceholder"
          class="conversation-input-control"
          @blur="onInputBlur"
          @click="onInputCursorChange"
          @focus="onInputCursorChange"
          @keydown="onInputKeydown"
          @keyup="onInputCursorChange"
          @update:value="updateInput"
        />
        <Input
          v-else
          ref="inputControlRef"
          :value="modelValue"
          :disabled="inputDisabled"
          :placeholder="inputPlaceholder"
          class="conversation-input-control"
          @blur="onInputBlur"
          @click="onInputCursorChange"
          @focus="onInputCursorChange"
          @keydown="onInputKeydown"
          @keyup="onInputCursorChange"
          @update:value="updateInput"
        />

        <div
          v-if="showMentionSuggestions"
          class="conversation-mention-menu"
          role="listbox"
        >
          <button
            v-for="(candidate, index) in mentionCandidates"
            :key="candidate.key"
            :class="[
              'conversation-mention-item',
              { 'is-active': index === activeMentionIndex },
            ]"
            type="button"
            @mousedown.prevent="applyMentionCandidate(candidate)"
            @mouseenter="activeMentionIndex = index"
          >
            <Avatar :src="candidate.avatar" class="conversation-mention-avatar">
              {{ candidate.label.slice(0, 1) || '@' }}
            </Avatar>
            <span class="conversation-mention-text">
              <span class="conversation-mention-label">{{ candidate.label }}</span>
              <span v-if="candidate.description" class="conversation-mention-description">
                {{ candidate.description }}
              </span>
            </span>
          </button>
        </div>
      </div>

      <Button
        :disabled="!canSend"
        :loading="loading || sending"
        type="primary"
        @click="emitSend"
      >
        发送
      </Button>

      <Tooltip
        v-if="clearable"
        title="清空对话"
      >
        <Button
          :disabled="clearDisabled"
          @click="emit('clear')"
        >
          🗑️
        </Button>
      </Tooltip>
    </div>
  </div>
</template>

<style scoped>
.conversation-panel {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.conversation-header {
  padding: 16px 20px;
  border-bottom: 1px solid hsl(var(--border));
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
}

.conversation-header-main {
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 16px;
}

.conversation-header-avatar {
  flex-shrink: 0;
  width: 48px;
  height: 48px;
  line-height: 48px;
  font-size: 18px;
}

.conversation-header-text {
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.conversation-title {
  font-size: 17px;
  font-weight: 600;
  line-height: 1.35;
}

.conversation-subtitle {
  display: flex;
  align-items: center;
  gap: 6px;
  color: hsl(var(--muted-foreground));
  font-size: 12px;
  line-height: 1.5;
}

.conversation-subtitle-dot {
  width: 8px;
  height: 8px;
  flex-shrink: 0;
  border-radius: 999px;
  background: hsl(var(--muted-foreground));
}

.conversation-subtitle.is-online {
  color: hsl(var(--success));
}

.conversation-subtitle.is-online .conversation-subtitle-dot {
  background: hsl(var(--success));
}

.conversation-subtitle.is-connecting {
  color: hsl(var(--warning));
}

.conversation-subtitle.is-connecting .conversation-subtitle-dot {
  background: hsl(var(--warning));
}

.conversation-header-meta {
  min-width: 0;
}

.conversation-header-meta-standalone {
  display: flex;
  align-items: center;
}

.conversation-banner {
  padding: 12px 12px 0;
}

.conversation-content {
  min-height: 0;
  flex: 1;
  overflow: hidden;
}

.conversation-messages {
  height: 100%;
  overflow-y: auto;
  padding: 12px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.conversation-empty {
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
}

.message-row {
  display: flex;
  gap: 8px;
  align-items: flex-start;
}

.message-row.user {
  justify-content: flex-end;
}

.message-avatar {
  flex-shrink: 0;
}

.message-identity-trigger {
  padding: 0;
  border: none;
  background: transparent;
  display: inline-flex;
  flex-shrink: 0;
  cursor: pointer;
}

.message-column {
  max-width: 82%;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.message-column.assistant {
  width: min(82%, 860px);
  max-width: none;
}

.message-column.user {
  align-items: flex-end;
}

.message-name {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
  padding: 0 4px;
}

.message-name-trigger {
  width: fit-content;
  border: none;
  background: transparent;
  text-align: left;
  cursor: pointer;
}

.message-bubble {
  padding: 10px 14px;
  border-radius: 12px;
  line-height: 1.6;
  white-space: pre-wrap;
  word-break: break-word;
}

.message-bubble.user {
  background: hsl(var(--primary));
  color: hsl(var(--primary-foreground));
}

.message-bubble.assistant {
  background: hsl(var(--accent));
  color: hsl(var(--foreground));
  width: 100%;
  box-sizing: border-box;
}

.assistant-steps-bubble {
  padding: 0;
  overflow: hidden;
}

.assistant-run-card {
  background: linear-gradient(
    180deg,
    hsl(var(--background) / 0.92) 0%,
    hsl(var(--accent) / 0.8) 100%
  );
}

.assistant-run-card.is-running {
  box-shadow: inset 0 0 0 1px hsl(var(--primary) / 0.16);
}

.assistant-run-card.is-error {
  box-shadow: inset 0 0 0 1px hsl(var(--destructive) / 0.16);
}

.assistant-run-toggle {
  width: 100%;
  border: none;
  background: transparent;
  color: inherit;
  padding: 12px 14px;
  display: flex;
  align-items: center;
   justify-content: space-between;
  gap: 12px;
  text-align: left;
  cursor: pointer;
}

.assistant-run-status-dot {
  width: 10px;
  height: 10px;
  flex-shrink: 0;
  border-radius: 999px;
  background: hsl(var(--success));
  box-shadow: 0 0 0 4px hsl(var(--success) / 0.12);
}

.assistant-run-status-dot.is-running {
  background: hsl(var(--primary));
  box-shadow: 0 0 0 4px hsl(var(--primary) / 0.12);
}

.assistant-run-status-dot.is-error {
  background: hsl(var(--destructive));
  box-shadow: 0 0 0 4px hsl(var(--destructive) / 0.12);
}

.assistant-run-main {
  min-width: 0;
  display: flex;
  align-items: center;
  gap: 10px;
  flex: 1;
}

.assistant-run-caret {
  font-size: 11px;
  color: hsl(var(--muted-foreground));
  transition: transform 0.2s ease;
}

.assistant-run-caret.is-expanded {
  transform: rotate(90deg);
}

.assistant-run-title {
  min-width: 0;
  font-size: 13px;
  font-weight: 600;
}

.assistant-run-hint {
  flex-shrink: 0;
  color: hsl(var(--muted-foreground));
  font-size: 12px;
}

.assistant-run-preview {
  padding: 0 14px 14px;
}

.assistant-run-preview-content {
  box-shadow: 0 10px 24px -24px rgb(15 23 42 / 0.45);
}

.assistant-run-body {
  padding: 0 12px 12px;
  border-top: 1px solid hsl(var(--border) / 0.7);
  background: hsl(var(--background) / 0.7);
}

.assistant-step-list {
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding-top: 12px;
}

.assistant-step {
  border: 1px solid hsl(var(--border) / 0.7);
  border-radius: 12px;
  background: hsl(var(--background));
  box-shadow: 0 8px 24px -22px rgb(15 23 42 / 0.35);
}

.assistant-step-thinking {
  border-color: hsl(var(--primary) / 0.2);
}

.assistant-step-response {
  background: hsl(var(--accent) / 0.45);
}

.assistant-step-response.is-error {
  border-color: hsl(var(--destructive) / 0.35);
  background: hsl(var(--destructive) / 0.08);
}

.assistant-step-toggle {
   width: 100%;
   border: none;
   background: transparent;
   color: inherit;
   display: flex;
   align-items: center;
   justify-content: space-between;
   gap: 12px;
   padding: 12px 14px;
   text-align: left;
   cursor: pointer;
}

.assistant-step-heading-main {
   min-width: 0;
   display: flex;
   align-items: center;
   gap: 8px;
}

.assistant-step-caret {
   font-size: 11px;
   color: hsl(var(--muted-foreground));
   transition: transform 0.2s ease;
}

.assistant-step-caret.is-expanded {
   transform: rotate(90deg);
}

.assistant-step-index {
   padding: 2px 8px;
   border-radius: 999px;
   background: hsl(var(--muted));
  color: hsl(var(--muted-foreground));
  font-size: 12px;
  font-weight: 600;
}

.assistant-step-label {
  font-size: 13px;
  font-weight: 600;
}

.assistant-step-status-pill {
  flex-shrink: 0;
  padding: 2px 8px;
  border-radius: 999px;
  font-size: 12px;
  font-weight: 600;
}

.assistant-step-status-pill.is-running {
  background: hsl(var(--primary) / 0.12);
  color: hsl(var(--primary));
}

.assistant-step-status-pill.is-done {
  background: hsl(var(--success) / 0.12);
  color: hsl(var(--success));
}

.assistant-step-status-pill.is-error {
  background: hsl(var(--destructive) / 0.12);
  color: hsl(var(--destructive));
}

.assistant-step-body {
   padding: 12px 14px 14px;
   border-top: 1px solid hsl(var(--border) / 0.7);
}

.assistant-step-tool-name {
  font-weight: 600;
  margin-bottom: 10px;
  word-break: break-word;
}

.assistant-step-section + .assistant-step-section {
  margin-top: 10px;
}

.assistant-step-section-label {
  margin-bottom: 6px;
  color: hsl(var(--muted-foreground));
  font-size: 12px;
  font-weight: 600;
}

.assistant-step-content {
  white-space: pre-wrap;
  word-break: break-word;
  line-height: 1.6;
  color: hsl(var(--foreground));
}

.assistant-step-code {
  margin: 0;
  padding: 10px 12px;
  border-radius: 10px;
  background: hsl(var(--muted) / 0.5);
  font-size: 12px;
  line-height: 1.5;
  overflow: auto;
  border: 1px solid hsl(var(--border) / 0.6);
}

.assistant-step-code code {
  font-family: ui-monospace, SFMono-Regular, SFMono-RegularFallback, Consolas, monospace;
  white-space: pre-wrap;
  word-break: break-word;
}

.assistant-step-result {
  color: hsl(var(--muted-foreground));
}

.assistant-response-block {
  padding: 12px 14px;
  border-radius: 12px;
  border: 1px solid hsl(var(--border) / 0.6);
  background: hsl(var(--background));
}

.assistant-md-preview {
  background: transparent;
  color: inherit;
}

.assistant-md-preview:deep(.md-editor) {
  background: transparent;
}

.assistant-md-preview:deep(.md-editor-preview-wrapper) {
  padding: 0;
  background: transparent;
}

.assistant-md-preview:deep(.md-editor-preview) {
  color: inherit;
  font-family: inherit;
  font-size: 13px;
  line-height: 1.7;
}

.assistant-md-preview:deep(.md-editor-preview pre) {
  padding: 10px 12px;
  border-radius: 10px;
  border: 1px solid hsl(var(--border) / 0.65);
  background: hsl(var(--muted) / 0.65);
}

.assistant-md-preview:deep(.md-editor-preview pre code) {
  color: inherit;
  background: transparent;
}

.assistant-md-preview:deep(.md-editor-preview code:not(pre code)) {
  padding: 0.15rem 0.4rem;
  border-radius: 6px;
  background: hsl(var(--muted) / 0.65);
  color: hsl(var(--foreground));
}

.assistant-md-preview:deep(.md-editor-preview table) {
  background: transparent;
  border-color: hsl(var(--border));
}

.assistant-md-preview:deep(.md-editor-preview tr) {
  background: transparent;
}

.assistant-md-preview:deep(.md-editor-preview th) {
  background: hsl(var(--accent) / 0.5);
  color: hsl(var(--foreground));
  border-color: hsl(var(--border));
}

.assistant-md-preview:deep(.md-editor-preview td) {
  color: hsl(var(--foreground));
  border-color: hsl(var(--border));
}

.assistant-md-preview:deep(.md-editor-preview p:first-child),
.assistant-md-preview:deep(.md-editor-preview ul:first-child),
.assistant-md-preview:deep(.md-editor-preview ol:first-child),
.assistant-md-preview:deep(.md-editor-preview pre:first-child),
.assistant-md-preview:deep(.md-editor-preview blockquote:first-child),
.assistant-md-preview:deep(.md-editor-preview h1:first-child),
.assistant-md-preview:deep(.md-editor-preview h2:first-child),
.assistant-md-preview:deep(.md-editor-preview h3:first-child) {
  margin-top: 0;
}

.assistant-md-preview:deep(.md-editor-preview p:last-child),
.assistant-md-preview:deep(.md-editor-preview ul:last-child),
.assistant-md-preview:deep(.md-editor-preview ol:last-child),
.assistant-md-preview:deep(.md-editor-preview pre:last-child),
.assistant-md-preview:deep(.md-editor-preview blockquote:last-child) {
  margin-bottom: 0;
}

.tool-call-error,
.assistant-step-error {
  color: hsl(var(--destructive));
}

.message-placeholder {
  color: hsl(var(--muted-foreground));
  font-size: 13px;
}

.message-meta {
  padding: 2px 4px 0;
  color: hsl(var(--muted-foreground));
  font-size: 12px;
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.message-meta-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 10px;
  border-radius: 999px;
  border: 1px solid hsl(var(--border) / 0.7);
  background: hsl(var(--muted) / 0.45);
  color: hsl(var(--muted-foreground));
  line-height: 1.2;
  white-space: nowrap;
}

.message-meta-icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 16px;
  height: 16px;
  flex-shrink: 0;
  font-size: 14px;
  line-height: 1;
}

.message-meta-text {
  color: hsl(var(--foreground) / 0.92);
  font-size: 12px;
  font-weight: 600;
  line-height: 1.2;
}

.conversation-input {
  padding: 10px;
  border-top: 1px solid hsl(var(--border));
  display: flex;
  gap: 8px;
  flex-shrink: 0;
  align-items: flex-end;
}

.conversation-input-main {
  position: relative;
  flex: 1;
}

.conversation-input-control {
  flex: 1;
}

.conversation-mention-menu {
  position: absolute;
  inset-inline: 0;
  bottom: calc(100% + 8px);
  display: flex;
  max-height: 260px;
  flex-direction: column;
  overflow-y: auto;
  border-radius: 16px;
  border: 1px solid hsl(var(--border) / 0.75);
  background: hsl(var(--background));
  box-shadow: 0 22px 48px -28px rgb(15 23 42 / 0.42);
  z-index: 10;
}

.conversation-mention-item {
  padding: 10px 12px;
  border: none;
  background: transparent;
  display: flex;
  align-items: center;
  gap: 10px;
  text-align: left;
  cursor: pointer;
  transition: background-color 0.18s ease;
}

.conversation-mention-item + .conversation-mention-item {
  border-top: 1px solid hsl(var(--border) / 0.55);
}

.conversation-mention-item:hover,
.conversation-mention-item.is-active {
  background: hsl(var(--accent) / 0.7);
}

.conversation-mention-avatar {
  flex-shrink: 0;
}

.conversation-mention-text {
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.conversation-mention-label {
  color: hsl(var(--foreground));
  font-size: 14px;
  font-weight: 600;
  line-height: 1.25;
}

.conversation-mention-description {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
  line-height: 1.25;
}

.streaming-cursor {
  animation: blink 1s step-end infinite;
  font-weight: bold;
}

:deep(.ant-spin-nested-loading),
:deep(.ant-spin-container) {
  height: 100%;
}

@keyframes blink {
  50% {
    opacity: 0;
  }
}
</style>
