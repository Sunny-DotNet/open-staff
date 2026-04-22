import type { SessionEventDto } from '@openstaff/api';

import { describe, expect, it } from 'vitest';

import {
  applySessionConversationEvent,
  createLocalConversationMessageId,
  createSessionConversationState,
  shouldStreamSessionConversation,
  type SessionConversationMessage,
} from './session-conversation-stream';

function createEvent(
  eventType: string,
  payload: Record<string, unknown>,
  messageId = 'assistant-1',
): SessionEventDto {
  return {
    createdAt: '2026-04-17T13:30:00.000Z',
    eventType,
    messageId,
    payload: JSON.stringify(payload),
    sessionId: 'session-1',
  };
}

describe('session conversation stream', () => {
  it('only treats active sessions as streamable when restoring a conversation', () => {
    expect(shouldStreamSessionConversation({ isActive: true, status: 'active' })).toBe(true);
    expect(shouldStreamSessionConversation({ isActive: true, status: 'awaiting_input' })).toBe(false);
    expect(shouldStreamSessionConversation({ isActive: false, status: 'completed' })).toBe(false);
  });

  it('splits interleaved thinking and tool events into ordered assistant steps', () => {
    const messages: SessionConversationMessage[] = [];
    const state = createSessionConversationState();

    applySessionConversationEvent(
      messages,
      state,
      createEvent('streaming_thinking', { token: '先分析需求' }),
    );
    applySessionConversationEvent(
      messages,
      state,
      createEvent('tool_call', {
        arguments: JSON.stringify({ query: 'OpenStaff' }),
        name: 'search_code',
        toolCallId: 'call-1',
      }),
    );
    applySessionConversationEvent(
      messages,
      state,
      createEvent('tool_result', {
        name: 'search_code',
        result: '找到了 3 个匹配项',
        toolCallId: 'call-1',
      }),
    );
    applySessionConversationEvent(
      messages,
      state,
      createEvent('streaming_thinking', { token: '继续整理上下文' }),
    );
    applySessionConversationEvent(
      messages,
      state,
      createEvent('tool_call', {
        arguments: JSON.stringify({ path: 'src/app.ts' }),
        name: 'read_file',
        toolCallId: 'call-2',
      }),
    );
    applySessionConversationEvent(
      messages,
      state,
      createEvent('tool_result', {
        name: 'read_file',
        result: '文件读取完成',
        toolCallId: 'call-2',
      }),
    );
    applySessionConversationEvent(
      messages,
      state,
      createEvent('streaming_token', { token: '先给你结论。' }),
    );
    const reduced = applySessionConversationEvent(
      messages,
      state,
      createEvent('message', { content: '先给你结论。' }),
    );

    expect(reduced.clearSending).toBe(true);
    expect(state.pendingAssistantId).toBeNull();
    expect(messages).toHaveLength(1);

    const assistant = messages[0]!;
    expect(assistant.steps?.map((step) => step.kind)).toEqual([
      'thinking',
      'tool_call',
      'thinking',
      'tool_call',
      'response',
    ]);

    expect(assistant.steps?.[0]).toMatchObject({
      content: '先分析需求',
      kind: 'thinking',
    });
    expect(assistant.steps?.[1]).toMatchObject({
      kind: 'tool_call',
      name: 'search_code',
      status: 'done',
    });
    expect(assistant.steps?.[2]).toMatchObject({
      content: '继续整理上下文',
      kind: 'thinking',
    });
    expect(assistant.steps?.[3]).toMatchObject({
      kind: 'tool_call',
      name: 'read_file',
      status: 'done',
    });
    expect(assistant.steps?.[4]).toMatchObject({
      content: '先给你结论。',
      kind: 'response',
    });
  });

  it('creates a final response step when the terminal message arrives without prior response tokens', () => {
    const messages: SessionConversationMessage[] = [];
    const state = createSessionConversationState();

    applySessionConversationEvent(
      messages,
      state,
      createEvent('streaming_thinking', { token: '先想一下' }, 'assistant-2'),
    );
    const reduced = applySessionConversationEvent(
      messages,
      state,
      createEvent('message', { content: '直接给最终答复' }, 'assistant-2'),
    );

    expect(reduced.clearSending).toBe(true);
    expect(messages).toHaveLength(1);
    expect(messages[0]!.steps?.map((step) => step.kind)).toEqual([
      'thinking',
      'response',
    ]);
    expect(messages[0]!.steps?.[1]).toMatchObject({
      content: '直接给最终答复',
      kind: 'response',
    });
  });

  it('reconciles local user messages when the server echoes user_input', () => {
    const messages: SessionConversationMessage[] = [
      {
        id: createLocalConversationMessageId('user'),
        role: 'user',
        content: '帮我看一下项目状态',
        timestamp: '2026-04-17T13:29:59.000Z',
      },
    ];
    const state = createSessionConversationState();

    applySessionConversationEvent(
      messages,
      state,
      createEvent(
        'user_input',
        { content: '帮我看一下项目状态' },
        'server-user-1',
      ),
    );

    expect(messages).toHaveLength(1);
    expect(messages[0]).toMatchObject({
      id: 'server-user-1',
      role: 'user',
      content: '帮我看一下项目状态',
    });
  });
});
