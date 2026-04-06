<script lang="ts" setup>
import type { AgentApi } from '#/api/openstaff/agent';

import { computed, nextTick, onUnmounted, ref, watch } from 'vue';

import {
  Badge,
  Button,
  Card,
  Empty,
  Form,
  FormItem,
  Input,
  InputNumber,
  Modal,
  Select,
  SelectOption,
  Slider,
  Textarea,
  Tooltip,
} from 'ant-design-vue';

import {
  getAgentRoleApi,
  testAgentChatApi,
} from '#/api/openstaff/agent';
import { getMcpConfigsApi } from '#/api/openstaff/mcp';
import {
  getProviderAccountsApi,
  getProviderModelsApi,
} from '#/api/openstaff/settings';
import { useNotification } from '#/composables/useNotification';

interface ChatMessage {
  content: string;
  role: 'assistant' | 'user';
  streaming?: boolean;
  thinking?: string;
  thinkingStreaming?: boolean;
  usage?: {
    inputTokens?: number;
    outputTokens?: number;
    totalTokens?: number;
  };
  timing?: { totalMs?: number; firstTokenMs?: number };
  model?: string;
  toolCalls?: Array<{
    name: string;
    arguments?: Record<string, unknown>;
    result?: string;
    error?: string;
    status: 'calling' | 'done' | 'error';
  }>;
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

// Chat state
const chatMessages = ref<ChatMessage[]>([]);
const chatInput = ref('');
const chatLoading = ref(false);
const chatContainerRef = ref<HTMLElement | null>(null);
let currentSubscription: { dispose: () => void } | null = null;

// Config panel state
const configCollapsed = ref(false);
const configForm = ref({
  systemPrompt: '',
  modelProviderId: undefined as string | undefined,
  modelName: undefined as string | undefined,
  temperature: 0.7,
});

// Provider/model options
const providerAccounts = ref<
  Array<{ id: string; name: string; protocolType: string }>
>([]);
const modelOptions = ref<Array<{ id: string; name: string }>>([]);
const modelsLoading = ref(false);

// MCP config options
const mcpConfigs = ref<Array<{ id: string; name: string; serverName: string }>>([]);
const selectedMcpConfigIds = ref<string[]>([]);

const hasOverride = computed(() => {
  return (
    configForm.value.systemPrompt ||
    configForm.value.modelProviderId ||
    configForm.value.modelName ||
    configForm.value.temperature !== 0.7
  );
});

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

async function loadRoleConfig() {
  if (!props.roleId) return;
  try {
    const role = await getAgentRoleApi(props.roleId);
    configForm.value.systemPrompt = role.systemPrompt || '';
    configForm.value.modelProviderId = role.modelProviderId || undefined;
    configForm.value.modelName = role.modelName || undefined;

    // Parse temperature from config JSON
    if (role.config) {
      try {
        const cfg = JSON.parse(role.config) as AgentApi.AgentRoleConfig;
        configForm.value.temperature =
          cfg.modelParameters?.temperature ?? 0.7;
      } catch {
        configForm.value.temperature = 0.7;
      }
    }

    // Load models for selected provider
    if (configForm.value.modelProviderId) {
      await loadModels(configForm.value.modelProviderId);
    }
  } catch {
    // ignore - use defaults
  }
}

async function loadProviderAccounts() {
  try {
    const accounts = await getProviderAccountsApi();
    providerAccounts.value = accounts.map((a: Record<string, any>) => ({
      id: a.id,
      name: a.name || a.providerName || a.id,
      protocolType: a.protocolType || '',
    }));
  } catch {
    providerAccounts.value = [];
  }
}

async function loadModels(providerId: string) {
  if (!providerId) {
    modelOptions.value = [];
    return;
  }
  modelsLoading.value = true;
  try {
    const models = await getProviderModelsApi(providerId);
    modelOptions.value = models.map((m: Record<string, any>) => ({
      id: m.id || m.modelId,
      name: m.name || m.displayName || m.id || m.modelId,
    }));
  } catch {
    modelOptions.value = [];
  } finally {
    modelsLoading.value = false;
  }
}

async function loadMcpConfigs() {
  try {
    const configs = await getMcpConfigsApi();
    mcpConfigs.value = configs.map((c: Record<string, any>) => ({
      id: c.id,
      name: c.name,
      serverName: c.mcpServerName || c.serverName || '',
    }));
  } catch {
    mcpConfigs.value = [];
  }
}

function onProviderChange(value: string) {
  configForm.value.modelProviderId = value;
  configForm.value.modelName = undefined;
  modelOptions.value = [];
  if (value) loadModels(value);
}

watch(
  () => props.open,
  async (val) => {
    if (val) {
      resetState();
      await Promise.all([
        loadProviderAccounts(),
        loadMcpConfigs(),
        loadRoleConfig(),
      ]);
    }
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
    const override: AgentApi.AgentRoleOverride | undefined = hasOverride.value
      ? {
          systemPrompt: configForm.value.systemPrompt || undefined,
          modelProviderId: configForm.value.modelProviderId || undefined,
          modelName: configForm.value.modelName || undefined,
          temperature: configForm.value.temperature,
        }
      : undefined;

    const { sessionId } = await testAgentChatApi(
      props.roleId,
      userMsg,
      override,
    );
    const assistantIdx = chatMessages.value.length;
    chatMessages.value.push({
      role: 'assistant',
      content: '',
      streaming: true,
    });

    currentSubscription = await streamSession(
      sessionId,
      (evt: AgentApi.SessionEvent) => {
        if (!evt.payload) return;
        try {
          const data = JSON.parse(evt.payload);
          const msg = chatMessages.value[assistantIdx]!;

          if (evt.eventType === 'streaming_thinking') {
            chatMessages.value[assistantIdx] = {
              ...msg,
              thinking: (msg.thinking || '') + (data.token || ''),
              thinkingStreaming: true,
            };
          } else if (evt.eventType === 'streaming_token') {
            chatMessages.value[assistantIdx] = {
              ...msg,
              content: (msg.content || '') + (data.token || ''),
              streaming: true,
              thinkingStreaming: false,
            };
          } else if (
            evt.eventType === 'streaming_done' ||
            evt.eventType === 'message'
          ) {
            chatMessages.value[assistantIdx] = {
              ...msg,
              content: msg.content || data.content || '（无响应）',
              streaming: false,
              thinkingStreaming: false,
              thinking: msg.thinking || data.thinking || undefined,
              usage: data.usage,
              timing: data.timing,
              model: data.model,
            };
          } else if (evt.eventType === 'error') {
            chatMessages.value[assistantIdx] = {
              role: 'assistant',
              content: `❌ ${data.error || '未知错误'}`,
              streaming: false,
            };
          } else if (evt.eventType === 'tool_call') {
            const calls = [...(msg.toolCalls || [])];
            calls.push({
              name: data.name,
              arguments: data.arguments,
              status: 'calling',
            });
            chatMessages.value[assistantIdx] = { ...msg, toolCalls: calls };
          } else if (evt.eventType === 'tool_result') {
            const calls = [...(msg.toolCalls || [])];
            const idx = calls.findLastIndex(
              (c) => c.name === data.name && c.status === 'calling',
            );
            if (idx >= 0)
              calls[idx] = {
                ...calls[idx]!,
                result: data.result,
                status: 'done',
              };
            chatMessages.value[assistantIdx] = { ...msg, toolCalls: calls };
          } else if (evt.eventType === 'tool_error') {
            const calls = [...(msg.toolCalls || [])];
            const idx = calls.findLastIndex(
              (c) => c.name === data.name && c.status === 'calling',
            );
            if (idx >= 0)
              calls[idx] = {
                ...calls[idx]!,
                error: data.error,
                status: 'error',
              };
            chatMessages.value[assistantIdx] = { ...msg, toolCalls: calls };
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
    const message = err instanceof Error ? err.message : '网络错误';
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
    :width="configCollapsed ? '70%' : '90%'"
    :body-style="{
      padding: 0,
      display: 'flex',
      flexDirection: 'row',
      height: '75vh',
    }"
    :destroy-on-close="true"
    @update:open="emit('update:open', $event)"
    @cancel="emit('update:open', false)"
  >
    <!-- 左侧：配置面板 -->
    <div
      v-show="!configCollapsed"
      class="config-panel"
    >
      <div class="config-header">
        <span style="font-weight: 600">⚙️ 配置</span>
        <Button size="small" type="text" @click="configCollapsed = true">
          ◀
        </Button>
      </div>
      <div class="config-body">
        <Form layout="vertical" size="small">
          <FormItem label="系统提示词">
            <Textarea
              v-model:value="configForm.systemPrompt"
              :auto-size="{ minRows: 4, maxRows: 10 }"
              placeholder="输入系统提示词..."
            />
          </FormItem>

          <FormItem label="供应商">
            <Select
              :value="configForm.modelProviderId"
              allow-clear
              placeholder="选择供应商"
              show-search
              option-filter-prop="label"
              @change="onProviderChange"
            >
              <SelectOption
                v-for="p in providerAccounts"
                :key="p.id"
                :value="p.id"
                :label="p.name"
              >
                {{ p.name }}
                <span style="color: hsl(var(--muted-foreground)); font-size: 12px; margin-left: 4px">
                  {{ p.protocolType }}
                </span>
              </SelectOption>
            </Select>
          </FormItem>

          <FormItem label="模型">
            <Select
              v-model:value="configForm.modelName"
              :loading="modelsLoading"
              :disabled="!configForm.modelProviderId"
              allow-clear
              placeholder="选择模型"
              show-search
              option-filter-prop="label"
            >
              <SelectOption
                v-for="m in modelOptions"
                :key="m.id"
                :value="m.id"
                :label="m.name"
              >
                {{ m.name }}
              </SelectOption>
            </Select>
          </FormItem>

          <FormItem label="Temperature">
            <div style="display: flex; align-items: center; gap: 8px">
              <Slider
                v-model:value="configForm.temperature"
                :min="0"
                :max="2"
                :step="0.1"
                style="flex: 1"
              />
              <InputNumber
                v-model:value="configForm.temperature"
                :min="0"
                :max="2"
                :step="0.1"
                :precision="1"
                style="width: 70px"
                size="small"
              />
            </div>
          </FormItem>

          <FormItem label="MCP 工具">
            <Select
              v-model:value="selectedMcpConfigIds"
              mode="multiple"
              placeholder="选择 MCP 配置"
              allow-clear
              option-filter-prop="label"
            >
              <SelectOption
                v-for="c in mcpConfigs"
                :key="c.id"
                :value="c.id"
                :label="c.name"
              >
                {{ c.name }}
                <span
                  v-if="c.serverName"
                  style="color: hsl(var(--muted-foreground)); font-size: 12px; margin-left: 4px"
                >
                  ({{ c.serverName }})
                </span>
              </SelectOption>
            </Select>
          </FormItem>
        </Form>
      </div>
    </div>

    <!-- 折叠按钮 -->
    <div
      v-if="configCollapsed"
      class="config-toggle"
      @click="configCollapsed = false"
    >
      <Tooltip title="展开配置" placement="right">
        <Button size="small" type="text" style="writing-mode: vertical-lr">
          ⚙️ 配置 ▶
        </Button>
      </Tooltip>
    </div>

    <!-- 右侧：对话区域 -->
    <div class="chat-area">
      <!-- 消息列表 -->
      <div ref="chatContainerRef" class="chat-messages">
        <div
          v-if="chatMessages.length === 0"
          style="
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100%;
          "
        >
          <Empty description="发送消息开始测试对话" />
        </div>

        <div
          v-for="(msg, idx) in chatMessages"
          :key="idx"
          :style="{
            display: 'flex',
            justifyContent:
              msg.role === 'user' ? 'flex-end' : 'flex-start',
            marginBottom: '12px',
          }"
        >
          <div
            :style="{
              maxWidth: '80%',
              minWidth: msg.role === 'assistant' ? '200px' : undefined,
            }"
          >
            <!-- 思考过程 -->
            <Card
              v-if="msg.thinking || msg.thinkingStreaming"
              size="small"
              class="thinking-card"
            >
              <template #title>
                <span style="font-size: 12px">💭 思考过程</span>
              </template>
              <div class="thinking-content">
                {{ msg.thinking
                }}<span v-if="msg.thinkingStreaming" class="streaming-cursor"
                  >▌</span
                >
              </div>
            </Card>

            <!-- 工具调用 -->
            <Card
              v-if="msg.toolCalls?.length"
              size="small"
              class="tool-card"
            >
              <template #title>
                <span style="font-size: 12px">🔧 工具调用</span>
              </template>
              <div
                v-for="(tc, tIdx) in msg.toolCalls"
                :key="tIdx"
                style="
                  margin-bottom: 4px;
                  padding: 4px 0;
                  border-bottom: 1px solid hsl(var(--border) / 0.3);
                "
              >
                <div>
                  <Badge
                    v-if="tc.status === 'calling'"
                    color="blue"
                    status="processing"
                  />
                  <span
                    v-else-if="tc.status === 'done'"
                    style="color: hsl(var(--success))"
                    >✅</span
                  >
                  <span v-else style="color: hsl(var(--destructive))"
                    >❌</span
                  >
                  <strong>{{ tc.name }}</strong>
                </div>
                <div
                  v-if="tc.result"
                  style="
                    margin-top: 2px;
                    font-size: 12px;
                    color: hsl(var(--muted-foreground));
                    max-height: 100px;
                    overflow-y: auto;
                    white-space: pre-wrap;
                    word-break: break-all;
                  "
                >
                  {{ tc.result }}
                </div>
                <div
                  v-if="tc.error"
                  style="
                    margin-top: 2px;
                    font-size: 12px;
                    color: hsl(var(--destructive));
                  "
                >
                  {{ tc.error }}
                </div>
              </div>
            </Card>

            <!-- 消息正文 -->
            <div
              :style="{
                padding: '10px 16px',
                borderRadius: '12px',
                whiteSpace: 'pre-wrap',
                wordBreak: 'break-word',
                lineHeight: '1.6',
                ...(msg.role === 'user'
                  ? {
                      background: 'hsl(var(--primary))',
                      color: 'hsl(var(--primary-foreground))',
                    }
                  : {
                      background: 'hsl(var(--accent))',
                      color: 'hsl(var(--foreground))',
                    }),
              }"
            >
              <template v-if="msg.content"
                >{{ msg.content
                }}<span v-if="msg.streaming" class="streaming-cursor"
                  >▌</span
                ></template
              >
              <template v-else-if="msg.thinkingStreaming">
                <span
                  style="
                    color: hsl(var(--muted-foreground));
                    font-style: italic;
                  "
                  >思考中...</span
                >
              </template>
            </div>

            <!-- 用量和用时 -->
            <div v-if="msg.usage || msg.timing" class="msg-meta">
              <span v-if="msg.model">🤖 {{ msg.model }}</span>
              <span v-if="msg.timing?.totalMs"
                >⏱ {{ (msg.timing.totalMs / 1000).toFixed(1) }}s</span
              >
              <span v-if="msg.timing?.firstTokenMs"
                >⚡ 首 token
                {{ (msg.timing.firstTokenMs / 1000).toFixed(1) }}s</span
              >
              <span v-if="msg.usage?.inputTokens != null"
                >📥 {{ msg.usage.inputTokens }}</span
              >
              <span v-if="msg.usage?.outputTokens != null"
                >📤 {{ msg.usage.outputTokens }}</span
              >
              <span v-if="msg.usage?.totalTokens"
                >📊 {{ msg.usage.totalTokens }} tokens</span
              >
            </div>
          </div>
        </div>

        <div
          v-if="
            chatLoading &&
            (!chatMessages.length ||
              (!chatMessages[chatMessages.length - 1]?.content &&
                !chatMessages[chatMessages.length - 1]?.thinking))
          "
          style="
            display: flex;
            justify-content: flex-start;
            margin-bottom: 12px;
          "
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
      <div class="chat-input-area">
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
          <Button @click="clearChat"> 🗑️ </Button>
        </Tooltip>
      </div>
    </div>
  </Modal>
</template>

<style scoped>
.config-panel {
  width: 320px;
  min-width: 320px;
  border-right: 1px solid hsl(var(--border));
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.config-header {
  padding: 12px 16px;
  border-bottom: 1px solid hsl(var(--border));
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.config-body {
  flex: 1;
  overflow-y: auto;
  padding: 12px 16px;
}

.config-toggle {
  display: flex;
  align-items: center;
  border-right: 1px solid hsl(var(--border));
  cursor: pointer;
}

.config-toggle:hover {
  background: hsl(var(--accent) / 0.5);
}

.chat-area {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.chat-messages {
  flex: 1;
  overflow-y: auto;
  padding: 16px 20px;
}

.chat-input-area {
  border-top: 1px solid hsl(var(--border));
  padding: 12px 20px;
  display: flex;
  gap: 8px;
}

.thinking-card {
  margin-bottom: 6px;
}

.thinking-content {
  white-space: pre-wrap;
  word-break: break-word;
  line-height: 1.5;
  max-height: 200px;
  overflow-y: auto;
  font-size: 13px;
}

.tool-card {
  margin-bottom: 6px;
}

.msg-meta {
  margin-top: 4px;
  padding: 0 4px;
  font-size: 12px;
  color: hsl(var(--muted-foreground));
  display: flex;
  gap: 12px;
  flex-wrap: wrap;
}

.streaming-cursor {
  animation: blink 1s step-end infinite;
  font-weight: bold;
}

@keyframes blink {
  50% {
    opacity: 0;
  }
}
</style>
