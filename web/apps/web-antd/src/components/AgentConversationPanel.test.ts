import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';

import AgentConversationPanel from './AgentConversationPanel.vue';
import type { AgentConversationMessage } from './agent-conversation';

describe('AgentConversationPanel', () => {
  it('shows a collapsed completed summary and expands step details on click', async () => {
    const messages: AgentConversationMessage[] = [
      {
        id: 'assistant-1',
        role: 'assistant',
        content: '# 标题\n\n- 条目一',
        steps: [
          {
            id: 'thinking-1',
            kind: 'thinking',
            content: '先分析一下',
            streaming: false,
          },
          {
            id: 'tool-1',
            kind: 'tool_call',
            name: 'search_code',
            arguments: { query: 'OpenStaff' },
            status: 'done',
            toolCallId: 'call-1',
          },
          {
            id: 'response-1',
            kind: 'response',
            content: '# 标题\n\n- 条目一',
            streaming: false,
          },
        ],
      },
    ];

    const wrapper = mount(AgentConversationPanel, {
      props: {
        agentName: '助手',
        messages,
        title: '对话',
      },
    });

    expect(wrapper.text()).toContain('✅ 已完成，共3步');
    expect(wrapper.find('.assistant-run-body').exists()).toBe(false);
    expect(wrapper.find('.assistant-run-preview').exists()).toBe(true);
    expect(wrapper.text()).not.toContain('最终回复');
    expect(wrapper.text()).toContain('标题');
    expect(wrapper.text()).not.toContain('先分析一下');
    expect(wrapper.text()).not.toContain('"query": "OpenStaff"');

    await wrapper.find('.assistant-run-toggle').trigger('click');

    expect(wrapper.find('.assistant-run-body').exists()).toBe(true);
    expect(wrapper.find('.assistant-run-preview').exists()).toBe(false);
    expect(wrapper.findAll('.assistant-step-body')).toHaveLength(0);
    expect(wrapper.text()).toContain('第1步');
    expect(wrapper.text()).toContain('使用工具 · search_code');
    expect(wrapper.text()).not.toContain('先分析一下');
    expect(wrapper.text()).not.toContain('"query": "OpenStaff"');

    await wrapper.findAll('.assistant-step-toggle')[0]!.trigger('click');

    expect(wrapper.findAll('.assistant-step-body')).toHaveLength(1);
    expect(wrapper.text()).toContain('先分析一下');

    await wrapper.findAll('.assistant-step-toggle')[1]!.trigger('click');

    expect(wrapper.findAll('.assistant-step-body')).toHaveLength(2);
    expect(wrapper.text()).toContain('"query": "OpenStaff"');
  });

  it('uses the running summary when steps are still streaming', () => {
    const messages: AgentConversationMessage[] = [
      {
        id: 'assistant-2',
        role: 'assistant',
        content: '处理中',
        streaming: true,
        steps: [
          {
            id: 'thinking-1',
            kind: 'thinking',
            content: '先分析',
            streaming: false,
          },
          {
            id: 'tool-1',
            kind: 'tool_call',
            name: 'list_directory',
            status: 'done',
          },
          {
            id: 'response-1',
            kind: 'response',
            content: '处理中',
            streaming: true,
          },
        ],
      },
    ];

    const wrapper = mount(AgentConversationPanel, {
      props: {
        agentName: '助手',
        messages,
        title: '对话',
      },
    });

    expect(wrapper.text()).toContain('第3步，正在回复中');
    expect(wrapper.find('.assistant-run-body').exists()).toBe(true);
    expect(wrapper.findAll('.assistant-step-body')).toHaveLength(1);
    expect(wrapper.text()).not.toContain('先分析');
    expect(wrapper.text()).toContain('处理中');
  });

  it('collapses previous streaming step when a new step arrives', async () => {
    const wrapper = mount(AgentConversationPanel, {
      props: {
        agentName: '助手',
        title: '对话',
        messages: [
          {
            id: 'assistant-3',
            role: 'assistant',
            content: '',
            streaming: true,
            steps: [
              {
                id: 'thinking-1',
                kind: 'thinking',
                content: '先分析目录结构',
                streaming: true,
              },
            ],
          },
        ] satisfies AgentConversationMessage[],
      },
    });

    expect(wrapper.findAll('.assistant-step-body')).toHaveLength(1);
    expect(wrapper.text()).toContain('先分析目录结构');

    await wrapper.setProps({
      messages: [
        {
          id: 'assistant-3',
          role: 'assistant',
          content: '',
          streaming: true,
          steps: [
            {
              id: 'thinking-1',
              kind: 'thinking',
              content: '先分析目录结构',
              streaming: false,
            },
            {
              id: 'tool-1',
              kind: 'tool_call',
              name: 'list_directory',
              status: 'calling',
            },
          ],
        },
      ] satisfies AgentConversationMessage[],
    });

    expect(wrapper.findAll('.assistant-step-body')).toHaveLength(1);
    expect(wrapper.text()).not.toContain('先分析目录结构');
    expect(wrapper.text()).toContain('使用工具 · list_directory');
  });
});
