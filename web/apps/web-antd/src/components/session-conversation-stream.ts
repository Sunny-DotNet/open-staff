import type { SessionEventDto } from '@openstaff/api';

import type {
  AgentConversationMessage,
  AgentConversationResponseStep,
  AgentConversationStep,
  AgentConversationThinkingStep,
  AgentConversationTiming,
  AgentConversationToolCall,
  AgentConversationToolStep,
  AgentConversationUsage,
} from './agent-conversation';

export interface SessionConversationStreamTarget {
  isActive?: boolean;
  status?: null | string;
}

export interface SessionConversationMessage extends AgentConversationMessage {
  id: string;
  agentKey?: null | string;
  parentMessageId?: null | string;
}

export interface SessionConversationState {
  pendingAssistantId: null | string;
}

export interface SessionConversationApplyResult {
  clearSending?: boolean;
  handled: boolean;
}

export function createSessionConversationState(): SessionConversationState {
  return {
    pendingAssistantId: null,
  };
}

export function createLocalConversationMessageId(prefix: string): string {
  return `local-${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
}

export function shouldStreamSessionConversation(
  session: null | SessionConversationStreamTarget | undefined,
): boolean {
  const normalizedStatus = session?.status?.trim().toLowerCase();
  if (normalizedStatus) {
    return normalizedStatus === 'active';
  }

  return session?.isActive === true;
}

export function startAssistantPlaceholder(
  messages: SessionConversationMessage[],
  state: SessionConversationState,
  timestamp = new Date().toISOString(),
): string {
  const existingIndex = findPendingAssistantIndex(messages, state);
  if (existingIndex >= 0) {
    const existing = messages[existingIndex]!;
    messages.splice(existingIndex, 1, {
      ...existing,
      streaming: true,
      thinkingStreaming: existing.thinkingStreaming ?? false,
      timestamp: existing.timestamp ?? timestamp,
    });
    state.pendingAssistantId = existing.id;
    return existing.id;
  }

  const placeholderId = createLocalConversationMessageId('assistant');
  messages.push({
    id: placeholderId,
    role: 'assistant',
    content: '',
    parentMessageId: null,
    streaming: true,
    thinkingStreaming: false,
    timestamp,
  });
  state.pendingAssistantId = placeholderId;
  return placeholderId;
}

export function removePendingAssistantPlaceholder(
  messages: SessionConversationMessage[],
  state: SessionConversationState,
) {
  const pendingIndex = findPendingAssistantIndex(messages, state);
  if (pendingIndex >= 0) {
    messages.splice(pendingIndex, 1);
  }

  state.pendingAssistantId = null;
}

export function applySessionConversationEvent(
  messages: SessionConversationMessage[],
  state: SessionConversationState,
  event: SessionEventDto,
): SessionConversationApplyResult {
  const payload = parseEventPayload(event);
  const eventMessageId = asString(payload.messageId) ?? event.messageId ?? undefined;
  const parentMessageId = asString(payload.parentMessageId) ?? null;

  switch (event.eventType) {
    case 'user_input': {
      const content = asString(payload.content) ?? asString(payload.input) ?? '';
      if (!content) {
        return { handled: true };
      }

      reconcileUserMessage(
        messages,
        eventMessageId ?? createLocalConversationMessageId('user'),
        content,
        event.createdAt ?? new Date().toISOString(),
        parentMessageId,
      );
      return { handled: true };
    }
    case 'streaming_thinking': {
      const token = asString(payload.token) ?? '';
      if (!token) {
        return { handled: true };
      }

      const messageIndex = ensurePendingAssistantIndex(
        messages,
        state,
        event.createdAt ?? new Date().toISOString(),
        parentMessageId,
        eventMessageId,
      );
      const current = messages[messageIndex]!;
      messages.splice(messageIndex, 1, appendThinkingTokenToMessage(current, token));
      return { handled: true };
    }
    case 'streaming_token': {
      const token = asString(payload.token) ?? '';
      if (!token) {
        return { handled: true };
      }

      const messageIndex = ensurePendingAssistantIndex(
        messages,
        state,
        event.createdAt ?? new Date().toISOString(),
        parentMessageId,
        eventMessageId,
      );
      const current = messages[messageIndex]!;
      messages.splice(messageIndex, 1, appendResponseTokenToMessage(current, token));
      return { handled: true };
    }
    case 'tool_call': {
      const messageIndex = ensurePendingAssistantIndex(
        messages,
        state,
        event.createdAt ?? new Date().toISOString(),
        parentMessageId,
        eventMessageId,
      );
      const current = messages[messageIndex]!;
      messages.splice(
        messageIndex,
        1,
        appendToolCallToMessage(current, {
          arguments: normalizeToolArguments(payload.arguments),
          name: asString(payload.name) ?? 'unknown',
          status: 'calling',
          toolCallId: asString(payload.toolCallId) ?? undefined,
        }),
      );
      return { handled: true };
    }
    case 'tool_result': {
      const pendingIndex = findPendingAssistantIndex(messages, state);
      if (pendingIndex < 0) {
        return { handled: true };
      }

      const current = messages[pendingIndex]!;
      messages.splice(
        pendingIndex,
        1,
        updateToolCallInMessage(
          current,
          asString(payload.toolCallId),
          asString(payload.name),
          asString(payload.result),
          undefined,
          'done',
        ),
      );
      return { handled: true };
    }
    case 'tool_error': {
      const pendingIndex = findPendingAssistantIndex(messages, state);
      if (pendingIndex < 0) {
        return { handled: true };
      }

      const current = messages[pendingIndex]!;
      messages.splice(
        pendingIndex,
        1,
        updateToolCallInMessage(
          current,
          asString(payload.toolCallId),
          asString(payload.name),
          undefined,
          asString(payload.error),
          'error',
        ),
      );
      return { handled: true };
    }
    case 'streaming_done': {
      const pendingIndex = findPendingAssistantIndex(messages, state);
      if (pendingIndex < 0) {
        return { handled: true };
      }

      const current = messages[pendingIndex]!;
      let nextSteps = finalizeMessageSteps(current.steps);
      const finalizedToolCalls = normalizeToolCalls(payload.toolCalls);
      if (finalizedToolCalls?.length) {
        nextSteps = mergeToolCallSteps(nextSteps, finalizedToolCalls);
      }

      messages.splice(
        pendingIndex,
        1,
        withAssistantStepState(current, nextSteps, {
          content: asString(payload.content) ?? current.content,
          thinking: asString(payload.thinking) ?? current.thinking,
          model: asString(payload.model) ?? current.model,
          streaming: false,
          thinkingStreaming: false,
          timing: normalizeTiming(payload.timing) ?? current.timing,
          usage: normalizeUsage(payload.usage) ?? current.usage,
        }),
      );
      return { handled: true };
    }
    case 'message': {
      const finalContent = asString(payload.content) ?? '';
      const finalMessageId =
        eventMessageId ?? createLocalConversationMessageId('assistant-final');
      const pendingIndex = findPendingAssistantIndex(messages, state);

      if (pendingIndex >= 0) {
        const current = messages[pendingIndex]!;
        const resolvedContent = finalContent || current.content || '（无响应）';
        const nextSteps = reconcileFinalResponseSteps(
          current.steps,
          resolvedContent,
          'default',
        );

        messages.splice(pendingIndex, 1);
        upsertSessionConversationMessage(
          messages,
          withAssistantStepState(
            {
              ...current,
              id: finalMessageId,
            },
            nextSteps,
            {
              content: resolvedContent,
              model: asString(payload.model) ?? current.model,
              parentMessageId,
              streaming: false,
              thinkingStreaming: false,
              timestamp: event.createdAt ?? new Date().toISOString(),
              timing: normalizeTiming(payload.timing) ?? current.timing,
              usage: normalizeUsage(payload.usage) ?? current.usage,
            },
          ),
        );
        state.pendingAssistantId = null;
        return { handled: true, clearSending: true };
      }

      upsertSessionConversationMessage(
        messages,
        createAssistantConversationMessage({
          content: finalContent,
          id: finalMessageId,
          model: asString(payload.model) ?? undefined,
          parentMessageId,
          timestamp: event.createdAt ?? new Date().toISOString(),
          timing: normalizeTiming(payload.timing),
          usage: normalizeUsage(payload.usage),
        }),
      );
      return { handled: true, clearSending: true };
    }
    case 'error': {
      const errorText = asString(payload.error) ?? asString(payload.message) ?? '未知错误';
      const finalContent = `❌ ${errorText}`;
      const pendingIndex = findPendingAssistantIndex(messages, state);

      if (pendingIndex >= 0) {
        const current = messages[pendingIndex]!;
        messages.splice(
          pendingIndex,
          1,
          withAssistantStepState(
            current,
            reconcileFinalResponseSteps(current.steps, finalContent, 'error'),
            {
              content: finalContent,
              streaming: false,
              thinkingStreaming: false,
              timestamp: event.createdAt ?? new Date().toISOString(),
            },
          ),
        );
        state.pendingAssistantId = null;
        return { handled: true, clearSending: true };
      }

      upsertSessionConversationMessage(
        messages,
        createAssistantConversationMessage({
          appearance: 'error',
          content: finalContent,
          id: eventMessageId ?? createLocalConversationMessageId('assistant-error'),
          parentMessageId,
          timestamp: event.createdAt ?? new Date().toISOString(),
        }),
      );
      return { handled: true, clearSending: true };
    }
    default:
      return { handled: false };
  }
}

export function upsertSessionConversationMessage(
  messages: SessionConversationMessage[],
  nextMessage: SessionConversationMessage,
) {
  const existingIndex = messages.findIndex((message) => message.id === nextMessage.id);
  if (existingIndex >= 0) {
    messages.splice(existingIndex, 1, {
      ...messages[existingIndex]!,
      ...nextMessage,
    });
    return;
  }

  messages.push(nextMessage);
}

export function reconcileUserMessage(
  messages: SessionConversationMessage[],
  messageId: string,
  content: string,
  timestamp: string,
  parentMessageId: null | string,
) {
  const existingIndex = messages.findIndex((message) => message.id === messageId);
  if (existingIndex >= 0) {
    messages.splice(existingIndex, 1, {
      ...messages[existingIndex]!,
      content,
      parentMessageId,
      timestamp,
    });
    return;
  }

  const localIndex = [...messages].findLastIndex(
    (message) =>
      message.role === 'user' &&
      message.id.startsWith('local-') &&
      message.content === content,
  );

  if (localIndex >= 0) {
    messages.splice(localIndex, 1, {
      ...messages[localIndex]!,
      id: messageId,
      parentMessageId,
      timestamp,
    });
    return;
  }

  upsertSessionConversationMessage(messages, {
    id: messageId,
    role: 'user',
    content,
    timestamp,
    parentMessageId,
  });
}

export function normalizeUsage(value: unknown): AgentConversationUsage | undefined {
  if (!isRecord(value)) {
    return undefined;
  }

  const usage: AgentConversationUsage = {
    inputTokens: asNumber(value.inputTokens ?? value.InputTokens),
    outputTokens: asNumber(value.outputTokens ?? value.OutputTokens),
    totalTokens: asNumber(value.totalTokens ?? value.TotalTokens),
  };

  return usage.inputTokens == null &&
    usage.outputTokens == null &&
    usage.totalTokens == null
    ? undefined
    : usage;
}

export function normalizeTiming(value: unknown): AgentConversationTiming | undefined {
  if (!isRecord(value)) {
    return undefined;
  }

  const timing: AgentConversationTiming = {
    totalMs: asNumber(value.totalMs ?? value.TotalMs ?? value.durationMs ?? value.DurationMs),
    firstTokenMs: asNumber(value.firstTokenMs ?? value.FirstTokenMs),
  };

  return timing.totalMs == null && timing.firstTokenMs == null
    ? undefined
    : timing;
}

function ensurePendingAssistantIndex(
  messages: SessionConversationMessage[],
  state: SessionConversationState,
  timestamp: string,
  parentMessageId: null | string,
  eventMessageId?: string,
) {
  const existingIndex = findPendingAssistantIndex(messages, state);
  if (existingIndex >= 0) {
    const current = messages[existingIndex]!;
    messages.splice(existingIndex, 1, {
      ...current,
      timestamp: current.timestamp ?? timestamp,
      parentMessageId: current.parentMessageId ?? parentMessageId,
    });
    state.pendingAssistantId = current.id;
    return existingIndex;
  }

  const placeholderId = eventMessageId ?? createLocalConversationMessageId('assistant');
  messages.push({
    id: placeholderId,
    role: 'assistant',
    content: '',
    parentMessageId,
    timestamp,
    streaming: true,
    thinkingStreaming: false,
  });
  state.pendingAssistantId = placeholderId;
  return messages.length - 1;
}

function findPendingAssistantIndex(
  messages: SessionConversationMessage[],
  state: SessionConversationState,
) {
  if (state.pendingAssistantId) {
    const exactIndex = messages.findIndex(
      (message) => message.id === state.pendingAssistantId,
    );
    if (exactIndex >= 0) {
      return exactIndex;
    }
  }

  const fallbackIndex = [...messages].findLastIndex(
    (message) =>
      message.role === 'assistant' && (message.streaming || message.thinkingStreaming),
  );

  if (fallbackIndex < 0) {
    state.pendingAssistantId = null;
    return -1;
  }

  state.pendingAssistantId = messages[fallbackIndex]!.id;
  return fallbackIndex;
}

function parseEventPayload(event: SessionEventDto): Record<string, unknown> {
  if (!event.payload) {
    return {};
  }

  try {
    return JSON.parse(event.payload) as Record<string, unknown>;
  } catch {
    return {};
  }
}

function appendThinkingTokenToMessage(
  message: SessionConversationMessage,
  token: string,
) {
  const preparedSteps = closeStreamingTextSteps(message.steps, 'thinking');
  const nextSteps = [...preparedSteps];
  const lastStep = nextSteps[nextSteps.length - 1];
  const appendToExisting =
    lastStep != null && isThinkingStep(lastStep) && !!lastStep.streaming;

  if (appendToExisting) {
    nextSteps.splice(nextSteps.length - 1, 1, {
      ...lastStep,
      content: `${lastStep.content}${token}`,
      streaming: true,
    });
  } else {
    nextSteps.push(createThinkingStep(token, true));
  }

  const nextThinking = appendToExisting || !message.thinking
    ? `${message.thinking ?? ''}${token}`
    : `${message.thinking}\n\n${token}`;

  return withAssistantStepState(message, nextSteps, {
    streaming: true,
    thinking: nextThinking,
    thinkingStreaming: true,
  });
}

function appendResponseTokenToMessage(
  message: SessionConversationMessage,
  token: string,
) {
  const preparedSteps = closeStreamingTextSteps(message.steps, 'response');
  const nextSteps = [...preparedSteps];
  const lastStep = nextSteps[nextSteps.length - 1];
  const appendToExisting =
    lastStep != null && isResponseStep(lastStep) && !!lastStep.streaming;

  if (appendToExisting) {
    nextSteps.splice(nextSteps.length - 1, 1, {
      ...lastStep,
      content: `${lastStep.content}${token}`,
      streaming: true,
    });
  } else {
    nextSteps.push(createResponseStep(token, true));
  }

  return withAssistantStepState(message, nextSteps, {
    content: `${message.content ?? ''}${token}`,
    streaming: true,
    thinkingStreaming: false,
  });
}

function appendToolCallToMessage(
  message: SessionConversationMessage,
  toolCall: AgentConversationToolCall,
) {
  const nextSteps = [
    ...closeStreamingTextSteps(message.steps),
    createToolStep(toolCall),
  ];

  return withAssistantStepState(message, nextSteps, {
    streaming: true,
    thinkingStreaming: false,
  });
}

function updateToolCallInMessage(
  message: SessionConversationMessage,
  toolCallId: null | string,
  toolName: null | string,
  result: null | string | undefined,
  error: null | string | undefined,
  status: AgentConversationToolCall['status'],
) {
  return withAssistantStepState(
    message,
    updateToolStepStatus(message.steps, toolCallId, toolName, result, error, status),
    {
      streaming: true,
      thinkingStreaming: false,
    },
  );
}

function withAssistantStepState(
  message: SessionConversationMessage,
  nextSteps: AgentConversationStep[],
  overrides: Partial<SessionConversationMessage>,
): SessionConversationMessage {
  const toolCalls = nextSteps
    .filter((step): step is AgentConversationToolStep => isToolStep(step))
    .map<AgentConversationToolCall>((step) => ({
      toolCallId: step.toolCallId,
      name: step.name,
      arguments: step.arguments,
      result: step.result,
      error: step.error,
      status: step.status,
    }));

  return {
    ...message,
    ...overrides,
    steps: nextSteps.length ? nextSteps : undefined,
    toolCalls: toolCalls.length ? toolCalls : undefined,
  };
}

function closeStreamingTextSteps(
  steps: AgentConversationStep[] | undefined,
  activeKind?: 'response' | 'thinking',
) {
  return (steps ?? []).map((step) => {
    if ((isThinkingStep(step) || isResponseStep(step))
      && step.streaming
      && step.kind !== activeKind) {
      return {
        ...step,
        streaming: false,
      };
    }

    return step;
  });
}

function finalizeMessageSteps(steps: AgentConversationStep[] | undefined) {
  return closeStreamingTextSteps(steps);
}

function reconcileFinalResponseSteps(
  steps: AgentConversationStep[] | undefined,
  finalContent: string,
  appearance: AgentConversationResponseStep['appearance'],
) {
  const nextSteps = finalizeMessageSteps(steps);
  const responseIndexes = nextSteps.reduce<number[]>((indexes, step, index) => {
    if (isResponseStep(step)) {
      indexes.push(index);
    }
    return indexes;
  }, []);

  if (responseIndexes.length === 0) {
    return [...nextSteps, createResponseStep(finalContent, false, appearance)];
  }

  if (responseIndexes.length === 1) {
    const responseIndex = responseIndexes[0]!;
    const responseStep = nextSteps[responseIndex] as AgentConversationResponseStep;
    nextSteps.splice(responseIndex, 1, {
      ...responseStep,
      appearance,
      content: finalContent,
      streaming: false,
    });
    return nextSteps;
  }

  const firstIndex = responseIndexes[0]!;
  const lastIndex = responseIndexes[responseIndexes.length - 1]!;
  const prefix = responseIndexes
    .slice(0, -1)
    .map((index) => (nextSteps[index] as AgentConversationResponseStep).content)
    .join('');

  if (finalContent.startsWith(prefix)) {
    const responseStep = nextSteps[lastIndex] as AgentConversationResponseStep;
    nextSteps.splice(lastIndex, 1, {
      ...responseStep,
      appearance,
      content: finalContent.slice(prefix.length),
      streaming: false,
    });
    return nextSteps;
  }

  nextSteps.splice(
    firstIndex,
    responseIndexes.length,
    createResponseStep(finalContent, false, appearance),
  );
  return nextSteps;
}

function createAssistantConversationMessage(input: {
  appearance?: AgentConversationResponseStep['appearance'];
  content: string;
  id: string;
  model?: string;
  parentMessageId: null | string;
  timestamp: string;
  timing?: AgentConversationTiming;
  usage?: AgentConversationUsage;
}): SessionConversationMessage {
  const steps = input.content
    ? [createResponseStep(input.content, false, input.appearance)]
    : undefined;

  return {
    id: input.id,
    role: 'assistant',
    content: input.content,
    model: input.model,
    parentMessageId: input.parentMessageId,
    steps,
    streaming: false,
    thinkingStreaming: false,
    timestamp: input.timestamp,
    timing: input.timing,
    usage: input.usage,
  };
}

function createThinkingStep(
  content: string,
  streaming: boolean,
): AgentConversationThinkingStep {
  return {
    id: createLocalConversationMessageId('thinking-step'),
    kind: 'thinking',
    content,
    streaming,
  };
}

function createResponseStep(
  content: string,
  streaming: boolean,
  appearance: AgentConversationResponseStep['appearance'] = 'default',
): AgentConversationResponseStep {
  return {
    id: createLocalConversationMessageId('response-step'),
    kind: 'response',
    appearance,
    content,
    streaming,
  };
}

function createToolStep(toolCall: AgentConversationToolCall): AgentConversationToolStep {
  return {
    id: createLocalConversationMessageId('tool-step'),
    kind: 'tool_call',
    arguments: toolCall.arguments,
    error: toolCall.error,
    name: toolCall.name,
    result: toolCall.result,
    status: toolCall.status,
    toolCallId: toolCall.toolCallId,
  };
}

function updateToolStepStatus(
  steps: AgentConversationStep[] | undefined,
  toolCallId: null | string,
  toolName: null | string,
  result: null | string | undefined,
  error: null | string | undefined,
  status: AgentConversationToolCall['status'],
) {
  const nextSteps = [...(steps ?? [])];
  const exactIndex = !toolCallId
    ? -1
    : nextSteps.findLastIndex(
      (step) => isToolStep(step) && step.toolCallId === toolCallId,
    );
  const fallbackIndex =
    exactIndex >= 0
      ? exactIndex
      : nextSteps.findLastIndex(
        (step) => isToolStep(step) && step.name === (toolName ?? 'unknown'),
      );

  if (fallbackIndex >= 0) {
    const current = nextSteps[fallbackIndex] as AgentConversationToolStep;
    nextSteps.splice(fallbackIndex, 1, {
      ...current,
      error: error ?? undefined,
      result: result ?? undefined,
      status,
    });
    return nextSteps;
  }

  nextSteps.push(
    createToolStep({
      toolCallId: toolCallId ?? undefined,
      name: toolName ?? 'unknown',
      result: result ?? undefined,
      error: error ?? undefined,
      status,
    }),
  );
  return nextSteps;
}

function mergeToolCallSteps(
  steps: AgentConversationStep[] | undefined,
  toolCalls: AgentConversationToolCall[],
) {
  return toolCalls.reduce(
    (currentSteps, toolCall) =>
      updateToolStepStatus(
        currentSteps,
        toolCall.toolCallId ?? null,
        toolCall.name,
        toolCall.result,
        toolCall.error,
        toolCall.status,
      ),
    [...(steps ?? [])],
  );
}

function normalizeToolArguments(value: unknown): Record<string, unknown> | undefined {
  if (isRecord(value)) {
    return value;
  }

  if (typeof value !== 'string' || !value.trim()) {
    return undefined;
  }

  try {
    const parsed = JSON.parse(value);
    return isRecord(parsed) ? parsed : { value: parsed };
  } catch {
    return { raw: value };
  }
}

function normalizeToolCalls(value: unknown): AgentConversationToolCall[] | undefined {
  if (!Array.isArray(value)) {
    return undefined;
  }

  const toolCalls = value
    .map((toolCall) => normalizeToolCall(toolCall))
    .filter((toolCall): toolCall is AgentConversationToolCall => !!toolCall);

  return toolCalls.length ? toolCalls : undefined;
}

function normalizeToolCall(value: unknown): AgentConversationToolCall | undefined {
  if (!isRecord(value)) {
    return undefined;
  }

  const name = asString(value.name);
  if (!name) {
    return undefined;
  }

  return {
    toolCallId: asString(value.toolCallId) ?? undefined,
    name,
    arguments: normalizeToolArguments(value.arguments),
    result: asString(value.result) ?? undefined,
    error: asString(value.error) ?? undefined,
    status: normalizeToolCallStatus(value.status) ?? 'done',
  };
}

function normalizeToolCallStatus(
  value: unknown,
): AgentConversationToolCall['status'] | undefined {
  if (value === 'calling' || value === 'done' || value === 'error') {
    return value;
  }

  return undefined;
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

function isRecord(value: unknown): value is Record<string, unknown> {
  return !!value && typeof value === 'object' && !Array.isArray(value);
}

function asNumber(value: unknown) {
  if (typeof value === 'number') {
    return value;
  }

  if (typeof value === 'string' && value.trim()) {
    const parsed = Number(value);
    return Number.isNaN(parsed) ? undefined : parsed;
  }

  return undefined;
}

function asString(value: unknown) {
  return typeof value === 'string' ? value : null;
}
