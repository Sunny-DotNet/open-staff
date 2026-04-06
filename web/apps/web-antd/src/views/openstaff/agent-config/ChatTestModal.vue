<script lang="ts" setup>
import type { AgentApi } from '#/api/openstaff/agent';

import { nextTick, onUnmounted, ref, watch } from 'vue';

import {
  Badge,
  Button,
  Empty,
  Input,
  Modal,
  Tooltip,
} from 'ant-design-vue';

import { testAgentChatApi } from '#/api/openstaff/agent';
import { useNotification } from '#/composables/useNotification';

interface ChatMessage {
  content: string;
  role: 'assistant' | 'user';
  streaming?: boolean;
}

const props = defineProps<{
  open: boolean;
  roleId: string;
  roleName: string;
}>();

const emit = defineEmits<{
  (e: 'update:open', value: boolean): void;
}>();

const { streamSession } = useNotification();

const chatMessages = ref<ChatMessage[]>([]);
const chatInput = ref('');
const chatLoading = ref(false);
const chatContainerRef = ref<HTMLElement | null>(null);
let currentSubscription: { dispose: () => void } | null = null;

function scrollToBottom() {
  if (chatContainerRef.value) {
    chatContainerRef.value.scrollTop = chatContainerRef.value.scrollHeight;
  }
}

function resetState() {
  chatMessages.value = [];
  chatInput.value = '';
  chatLoading.value = false;
  if (currentSubscription) {
    currentSubscription.dispose();
    currentSubscription = null;
  }
}

watch(
  () => props.open,
  (val) => {
    if (val) resetState();
  },
);

async function sendTestMessage() {
  if (!props.roleId || !chatInput.value.trim()) return;

  const userMsg = chatInput.value.trim();
  chatMessages.value.push({ role: 'user', content: userMsg });
  chatInput.value = '';
  chatLoading.value = true;

  await nextTick();
  scrollToBottom();

  try {
    const { sessionId } = await testAgentChatApi(props.roleId, userMsg);
    const assistantIdx = chatMessages.value.length;
    chatMessages.value.push({
      role: 'assistant',
      content: '思考中...',
      streaming: true,
    });

    currentSubscription = await streamSession(
      sessionId,
      (evt: AgentApi.SessionEvent) => {
        if (!evt.payload) return;
        try {
          const data = JSON.parse(evt.payload);
          if (evt.eventType === 'message') {
            chatMessages.value[assistantIdx] = {
              role: 'assistant',
              content: data.content || '（无响应）',
              streaming: false,
            };
          } else if (evt.eventType === 'error') {
            chatMessages.value[assistantIdx] = {
              role: 'assistant',
              content: `❌ ${data.error || '未知错误'}`,
              streaming: false,
            };
          }
        } catch {
          // ignore parse errors
        }
        nextTick(() => scrollToBottom());
      },
      () => {
        chatLoading.value = false;
        currentSubscription = null;
        if (chatMessages.value[assistantIdx]?.streaming) {
          chatMessages.value[assistantIdx]!.streaming = false;
        }
        nextTick(() => scrollToBottom());
      },
      (err: Error) => {
        chatLoading.value = false;
        currentSubscription = null;
        if (chatMessages.value[assistantIdx]?.streaming) {
          chatMessages.value[assistantIdx] = {
            role: 'assistant',
            content: `❌ 流式连接错误: ${err.message}`,
            streaming: false,
          };
        }
        nextTick(() => scrollToBottom());
      },
    );
  } catch (err: unknown) {
    const message =
      err instanceof Error ? err.message : '网络错误';
    chatMessages.value.push({
      role: 'assistant',
      content: `❌ 请求失败: ${message}`,
    });
    chatLoading.value = false;
    await nextTick();
    scrollToBottom();
  }
}

function handleKeydown(e: KeyboardEvent) {
  if (e.key === 'Enter' && !e.shiftKey) {
    e.preventDefault();
    sendTestMessage();
  }
}

function clearChat() {
  if (currentSubscription) {
    currentSubscription.dispose();
    currentSubscription = null;
  }
  chatLoading.value = false;
  chatMessages.value = [];
}

onUnmounted(() => {
  if (currentSubscription) {
    currentSubscription.dispose();
    currentSubscription = null;
  }
});
</script>

<template>
  <Modal
    :open="open"
    :title="`对话测试 - ${roleName}`"
    :footer="null"
    :width="'80%'"
    :body-style="{ padding: 0, display: 'flex', flexDirection: 'column', height: '70vh' }"
    :destroy-on-close="true"
    @update:open="emit('update:open', $event)"
    @cancel="emit('update:open', false)"
  >
    <!-- 消息列表 -->
    <div
      ref="chatContainerRef"
      style="flex: 1; overflow-y: auto; padding: 16px 20px"
    >
      <div
        v-if="chatMessages.length === 0"
        style="display: flex; justify-content: center; align-items: center; height: 100%"
      >
        <Empty description="发送消息开始测试对话" />
      </div>

      <div
        v-for="(msg, idx) in chatMessages"
        :key="idx"
        :style="{
          display: 'flex',
          justifyContent: msg.role === 'user' ? 'flex-end' : 'flex-start',
          marginBottom: '12px',
        }"
      >
        <div
          :style="{
            maxWidth: '75%',
            padding: '10px 16px',
            borderRadius: '12px',
            whiteSpace: 'pre-wrap',
            wordBreak: 'break-word',
            lineHeight: '1.6',
            ...(msg.role === 'user'
              ? { background: 'hsl(var(--primary))', color: 'hsl(var(--primary-foreground))' }
              : { background: 'hsl(var(--accent))', color: 'hsl(var(--foreground))' }),
          }"
        >
          {{ msg.content }}
        </div>
      </div>

      <div
        v-if="chatLoading"
        style="display: flex; justify-content: flex-start; margin-bottom: 12px"
      >
        <div
          style="
            padding: 10px 16px;
            border-radius: 12px;
            background: hsl(var(--accent));
          "
        >
          <Badge color="blue" status="processing" text="思考中..." />
        </div>
      </div>
    </div>

    <!-- 输入区域 -->
    <div
      style="
        border-top: 1px solid hsl(var(--border));
        padding: 12px 20px;
        display: flex;
        gap: 8px;
      "
    >
      <Input
        v-model:value="chatInput"
        :disabled="chatLoading"
        placeholder="输入消息测试员工..."
        style="flex: 1"
        @keydown="handleKeydown"
      />
      <Button
        :disabled="!chatInput.trim() || chatLoading"
        :loading="chatLoading"
        type="primary"
        @click="sendTestMessage"
      >
        发送
      </Button>
      <Tooltip title="清空对话">
        <Button @click="clearChat">
          🗑️
        </Button>
      </Tooltip>
    </div>
  </Modal>
</template>
