<script lang="ts" setup>
import type { AgentApi } from '#/api/openstaff/agent';
import type { McpApi } from '#/api/openstaff/mcp';
import type { SettingsApi } from '#/api/openstaff/settings';

import { computed, nextTick, onUnmounted, ref, watch } from 'vue';

import {
  Badge,
  Button,
  Card,
  Col,
  Collapse,
  CollapsePanel,
  Divider,
  Empty,
  Form,
  FormItem,
  Input,
  InputNumber,
  message,
  Modal,
  Row,
  Select,
  SelectOption,
  Slider,
  Space,
  Spin,
  Tag,
  Tooltip,
  Typography,
} from 'ant-design-vue';

import {
  getAgentRoleApi,
  testAgentChatApi,
  updateAgentRoleApi,
} from '#/api/openstaff/agent';
import {
  createAgentMcpBindingApi,
  deleteAgentMcpBindingApi,
  getAgentMcpBindingsApi,
  getMcpConfigsApi,
} from '#/api/openstaff/mcp';
import { useProviderModels } from '#/composables/useProviderModels';
import { useNotification } from '#/composables/useNotification';

import SoulConfigSection from './SoulConfigSection.vue';

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
  providers: SettingsApi.ProviderAccount[];
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
  name: '',
  description: '',
  systemPrompt: '',
  avatar: '' as string,
  modelProviderId: '' as string,
  modelName: '' as string,
  temperature: 0.7,
  maxTokens: 4096,
  tools: [] as string[],
  soul: { traits: [], style: '', attitudes: [], custom: '' } as AgentApi.AgentSoul,
});

const selectedProviderId = computed(() => configForm.value.modelProviderId);
const {
  models: providerModels,
  loading: loadingModels,
  error: modelsError,
  ensureLoaded: ensureModelsLoaded,
} = useProviderModels(selectedProviderId);

const enabledProviders = computed(() =>
  props.providers.filter((p) => p.isEnabled),
);

// MCP bindings (same pattern as AgentConfigDrawer)
const mcpBindings = ref<McpApi.AgentMcpBinding[]>([]);
const allMcpConfigs = ref<McpApi.McpServerConfig[]>([]);
const loadingMcpBindings = ref(false);
const selectedMcpConfigId = ref<string | undefined>(undefined);

const availableMcpConfigs = computed(() => {
  const boundIds = new Set(mcpBindings.value.map((b) => b.mcpServerConfigId));
  return allMcpConfigs.value.filter((c) => c.isEnabled && !boundIds.has(c.id));
});

function filterModelOption(input: string, option: any) {
  const search = input.toLowerCase();
  const val = (option?.value || '').toString().toLowerCase();
  return val.includes(search);
}

function filterMcpOption(input: string, option: any) {
  const search = input.toLowerCase();
  const val = (option?.children?.[0]?.children || '').toString().toLowerCase();
  return val.includes(search);
}

const hasOverride = computed(() => {
  return (
    configForm.value.name ||
    configForm.value.description ||
    configForm.value.modelProviderId ||
    configForm.value.modelName ||
    configForm.value.temperature !== 0.7 ||
    configForm.value.soul?.traits?.length ||
    configForm.value.soul?.style ||
    configForm.value.soul?.attitudes?.length ||
    configForm.value.soul?.custom
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
    const config = role.config ? (() => { try { return JSON.parse(role.config!) as AgentApi.AgentRoleConfig; } catch { return {} as AgentApi.AgentRoleConfig; } })() : {} as AgentApi.AgentRoleConfig;

    configForm.value = {
      name: role.name || '',
      description: role.description || '',
      systemPrompt: role.systemPrompt || '',
      avatar: role.avatar || '',
      modelProviderId: role.modelProviderId || '',
      modelName: role.modelName || '',
      temperature: config.modelParameters?.temperature ?? 0.7,
      maxTokens: config.modelParameters?.maxTokens ?? 4096,
      tools: config.tools ?? [],
      soul: {
        traits: role.soul?.traits ?? [],
        style: role.soul?.style ?? '',
        attitudes: role.soul?.attitudes ?? [],
        custom: role.soul?.custom ?? '',
      },
    };
  } catch {
    // ignore - use defaults
  }
}

async function loadMcpData() {
  if (!props.roleId) return;
  loadingMcpBindings.value = true;
  try {
    const [bindings, configs] = await Promise.all([
      getAgentMcpBindingsApi(props.roleId),
      getMcpConfigsApi(),
    ]);
    mcpBindings.value = bindings;
    allMcpConfigs.value = configs;
  } catch {
    mcpBindings.value = [];
    allMcpConfigs.value = [];
  } finally {
    loadingMcpBindings.value = false;
  }
}

async function addMcpBinding() {
  if (!selectedMcpConfigId.value || !props.roleId) return;
  try {
    await createAgentMcpBindingApi({
      agentRoleId: props.roleId,
      mcpServerConfigId: selectedMcpConfigId.value,
    });
    selectedMcpConfigId.value = undefined;
    await loadMcpData();
    message.success('MCP 工具已绑定');
  } catch (error: unknown) {
    const msg = error instanceof Error ? error.message : String(error);
    message.error('绑定失败: ' + msg);
  }
}

async function removeMcpBinding(configId: string) {
  if (!props.roleId) return;
  try {
    await deleteAgentMcpBindingApi(props.roleId, configId);
    await loadMcpData();
    message.success('已移除');
  } catch (error: unknown) {
    const msg = error instanceof Error ? error.message : String(error);
    message.error('移除失败: ' + msg);
  }
}

watch(
  () => props.open,
  async (val) => {
    if (val) {
      resetState();
      await Promise.all([
        loadRoleConfig(),
        loadMcpData(),
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
          name: configForm.value.name || undefined,
          description: configForm.value.description || undefined,
          modelProviderId: configForm.value.modelProviderId || undefined,
          modelName: configForm.value.modelName || undefined,
          temperature: configForm.value.temperature,
          soul: configForm.value.soul,
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

// Save state
const saving = ref(false);

async function saveConfig() {
  if (!props.roleId) return;
  saving.value = true;
  try {
    const configJson: AgentApi.AgentRoleConfig = {
      modelParameters: {
        temperature: configForm.value.temperature,
        maxTokens: configForm.value.maxTokens,
      },
      tools: configForm.value.tools,
    };
    await updateAgentRoleApi(props.roleId, {
      name: configForm.value.name || undefined,
      description: configForm.value.description || undefined,
      systemPrompt: configForm.value.systemPrompt || undefined,
      avatar: configForm.value.avatar || undefined,
      modelProviderId: configForm.value.modelProviderId || undefined,
      modelName: configForm.value.modelName || undefined,
      soul: configForm.value.soul,
      config: JSON.stringify(configJson),
    });
    message.success('配置已保存');
  } catch (err: unknown) {
    const msg = err instanceof Error ? err.message : String(err);
    message.error('保存失败: ' + msg);
  } finally {
    saving.value = false;
  }
}

function handleAvatarUpload(e: Event) {
  const file = (e.target as HTMLInputElement).files?.[0];
  if (!file) return;
  const reader = new FileReader();
  reader.onload = () => {
    const img = new Image();
    img.onload = () => {
      const canvas = document.createElement('canvas');
      canvas.width = 128;
      canvas.height = 128;
      const ctx = canvas.getContext('2d')!;
      ctx.drawImage(img, 0, 0, 128, 128);
      configForm.value.avatar = canvas.toDataURL('image/png');
    };
    img.src = reader.result as string;
  };
  reader.readAsDataURL(file);
  (e.target as HTMLInputElement).value = '';
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
        <Space size="small">
          <Button size="small" type="primary" :loading="saving" @click="saveConfig">
            💾 保存
          </Button>
          <Button size="small" type="text" @click="configCollapsed = true">
            ◀
          </Button>
        </Space>
      </div>
      <div class="config-body">
        <!-- 基本信息区 -->
        <Divider orientation="left" style="margin: 0 0 12px; font-size: 13px">基本信息</Divider>

        <!-- 头像上传 -->
        <div style="display: flex; align-items: center; gap: 12px; margin-bottom: 12px">
          <div
            class="avatar-upload"
            style="width: 64px; height: 64px; border-radius: 12px; overflow: hidden; border: 2px dashed var(--ant-color-border); display: flex; align-items: center; justify-content: center; cursor: pointer; flex-shrink: 0; background: var(--ant-color-bg-container-disabled)"
            @click="($refs.avatarInput as HTMLInputElement)?.click()"
          >
            <img
              v-if="configForm.avatar"
              :src="configForm.avatar"
              alt="头像"
              style="width: 100%; height: 100%; object-fit: cover"
            />
            <span v-else style="font-size: 24px; color: var(--ant-color-text-quaternary)">📷</span>
          </div>
          <div style="flex: 1">
            <Button size="small" @click="($refs.avatarInput as HTMLInputElement)?.click()">上传头像</Button>
            <Button
              v-if="configForm.avatar"
              size="small"
              type="text"
              danger
              style="margin-left: 4px"
              @click="configForm.avatar = ''"
            >
              移除
            </Button>
            <div style="font-size: 11px; color: var(--ant-color-text-tertiary); margin-top: 2px">
              128×128，支持 PNG/JPG
            </div>
          </div>
          <input
            ref="avatarInput"
            type="file"
            accept="image/png,image/jpeg,image/svg+xml"
            style="display: none"
            @change="handleAvatarUpload"
          />
        </div>

        <Form :label-col="{ span: 6 }" size="small">
          <FormItem label="名称">
            <Input v-model:value="configForm.name" placeholder="员工名称" />
          </FormItem>
          <FormItem label="描述">
            <Input.TextArea
              v-model:value="configForm.description"
              :rows="2"
              placeholder="描述"
            />
          </FormItem>
          <FormItem label="提示词">
            <Input.TextArea
              v-model:value="configForm.systemPrompt"
              :rows="4"
              placeholder="系统提示词"
            />
          </FormItem>
        </Form>

        <!-- 灵魂配置区 -->
        <SoulConfigSection v-model:soul="configForm.soul" />

        <!-- 模型配置区 -->
        <Divider orientation="left" style="margin: 8px 0 12px; font-size: 13px">模型配置</Divider>
        <Form :label-col="{ span: 6 }" size="small">
          <FormItem label="供应商">
            <Select
              v-model:value="configForm.modelProviderId"
              allow-clear
              placeholder="选择模型提供商"
              style="width: 100%"
            >
              <SelectOption
                v-for="p in enabledProviders"
                :key="p.id"
                :value="p.id"
              >
                {{ p.name }}
              </SelectOption>
            </Select>
          </FormItem>

          <FormItem label="模型" :validate-status="modelsError ? 'warning' : undefined" :help="modelsError ? '加载失败，点击下拉框重试' : undefined">
            <Select
              v-model:value="configForm.modelName"
              :disabled="!configForm.modelProviderId"
              :filter-option="filterModelOption"
              :loading="loadingModels"
              :not-found-content="modelsError ? '加载失败，点击重试' : loadingModels ? '加载中…' : '暂无模型'"
              :placeholder="configForm.modelProviderId ? '请选择模型' : '请先选择供应商'"
              allow-clear
              show-search
              style="width: 100%"
              @focus="ensureModelsLoaded"
            >
              <SelectOption
                v-for="m in providerModels"
                :key="m.id"
                :value="m.id"
              >
                {{ m.id }}
              </SelectOption>
            </Select>
          </FormItem>

          <FormItem label="温度">
            <Row :gutter="8" align="middle">
              <Col :span="16">
                <Slider
                  v-model:value="configForm.temperature"
                  :max="2"
                  :min="0"
                  :step="0.1"
                />
              </Col>
              <Col :span="8">
                <InputNumber
                  v-model:value="configForm.temperature"
                  :max="2"
                  :min="0"
                  :step="0.1"
                  size="small"
                  style="width: 100%"
                />
              </Col>
            </Row>
          </FormItem>

          <FormItem label="最大 Token">
            <InputNumber
              v-model:value="configForm.maxTokens"
              :max="128000"
              :min="256"
              :step="256"
              style="width: 100%"
            />
          </FormItem>
        </Form>

        <!-- 工具配置区 -->
        <Collapse :bordered="false" style="background: transparent">
          <CollapsePanel key="tools" header="🔧 内置工具">
            <div v-if="configForm.tools.length > 0">
              <Space wrap>
                <Tag
                  v-for="tool in configForm.tools"
                  :key="tool"
                  color="processing"
                >
                  🔧 {{ tool }}
                </Tag>
              </Space>
            </div>
            <Typography.Text v-else type="secondary">
              暂无已配置的工具
            </Typography.Text>
          </CollapsePanel>

          <CollapsePanel key="mcp" header="🧩 MCP 工具">
            <Spin :spinning="loadingMcpBindings">
              <div v-if="mcpBindings.length > 0" style="margin-bottom: 8px">
                <div
                  v-for="binding in mcpBindings"
                  :key="binding.mcpServerConfigId"
                  style="display: flex; align-items: center; justify-content: space-between; padding: 4px 6px; margin-bottom: 4px; background: var(--ant-color-bg-container-disabled); border-radius: 6px"
                >
                  <Space size="small">
                    <Tag color="cyan" style="margin: 0">{{ binding.mcpServerName }}</Tag>
                    <span style="font-size: 12px">{{ binding.mcpServerConfigName }}</span>
                  </Space>
                  <Button
                    type="text"
                    size="small"
                    danger
                    @click="removeMcpBinding(binding.mcpServerConfigId)"
                  >
                    移除
                  </Button>
                </div>
              </div>
              <Typography.Text v-else type="secondary" style="display: block; margin-bottom: 8px">
                暂未绑定 MCP 工具
              </Typography.Text>

              <Space>
                <Select
                  v-model:value="selectedMcpConfigId"
                  placeholder="选择 MCP 配置"
                  style="width: 180px"
                  allow-clear
                  show-search
                  size="small"
                  :filter-option="filterMcpOption"
                >
                  <SelectOption
                    v-for="cfg in availableMcpConfigs"
                    :key="cfg.id"
                    :value="cfg.id"
                  >
                    {{ cfg.name }}
                  </SelectOption>
                </Select>
                <Button
                  type="dashed"
                  size="small"
                  :disabled="!selectedMcpConfigId"
                  @click="addMcpBinding"
                >
                  ＋ 添加
                </Button>
              </Space>
            </Spin>
          </CollapsePanel>
        </Collapse>
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
  width: 380px;
  min-width: 380px;
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
