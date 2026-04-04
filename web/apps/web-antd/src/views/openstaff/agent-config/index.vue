<script lang="ts" setup>
import type { AgentApi } from '#/api/openstaff/agent';

import { computed, nextTick, onMounted, ref, watch } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Alert,
  Badge,
  Button,
  Card,
  Col,
  Divider,
  Empty,
  Form,
  FormItem,
  Input,
  InputNumber,
  Menu,
  MenuItem,
  message,
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
  getAgentRolesApi,
  createSessionApi,
  cancelSessionApi,
  updateAgentRoleApi,
} from '#/api/openstaff/agent';
import { getModelProvidersApi, getProviderModelsApi } from '#/api/openstaff/settings';
import type { SettingsApi } from '#/api/openstaff/settings';
import { useNotification } from '#/composables/useNotification';

// ===== 状态 =====
const roles = ref<AgentApi.AgentRole[]>([]);
const providers = ref<SettingsApi.ModelProvider[]>([]);
const selectedRoleId = ref<string>('');
const saving = ref(false);
const loadingRoles = ref(false);

// 编辑表单
const editForm = ref({
  modelProviderId: '' as string,
  modelName: '' as string,
  temperature: 0.7,
  maxTokens: 4096,
  tools: [] as string[],
});

// 模型列表
const providerModels = ref<SettingsApi.ProviderModel[]>([]);
const loadingModels = ref(false);

// 对话测试
const chatMessages = ref<Array<{ content: string; role: 'assistant' | 'user'; streaming?: boolean }>>([]);
const chatInput = ref('');
const chatLoading = ref(false);
const chatContainerRef = ref<HTMLElement | null>(null);

// 选中菜单key
const selectedKeys = computed({
  get: () => [selectedRoleId.value],
  set: (val: string[]) => {
    if (val.length > 0) selectedRoleId.value = val[0]!;
  },
});
const roleIcons: Record<string, string> = {
  orchestrator: '🎯',
  communicator: '💬',
  decision_maker: '🧠',
  architect: '📐',
  producer: '⚙️',
  debugger: '🔍',
  image_creator: '🎨',
  video_creator: '🎬',
};

// ===== 计算属性 =====
const selectedRole = computed(() =>
  roles.value.find((r) => r.id === selectedRoleId.value),
);

const enabledProviders = computed(() =>
  providers.value.filter((p) => p.isEnabled),
);

const selectedProvider = computed(() =>
  providers.value.find((p) => p.id === editForm.value.modelProviderId),
);

const configJson = computed<AgentApi.AgentRoleConfig>(() => {
  try {
    return selectedRole.value?.config
      ? JSON.parse(selectedRole.value.config)
      : {};
  } catch {
    return {};
  }
});

const availableTools = computed(() => {
  return configJson.value.tools ?? [];
});

// ===== 加载数据 =====
onMounted(async () => {
  loadingRoles.value = true;
  try {
    const [rolesData, providersData] = await Promise.all([
      getAgentRolesApi(),
      getModelProvidersApi(),
    ]);
    roles.value = rolesData;
    providers.value = providersData;

    if (roles.value.length > 0) {
      selectedRoleId.value = roles.value[0]!.id;
    }
  } finally {
    loadingRoles.value = false;
  }
});

// 切换角色时加载配置到编辑表单
watch(selectedRoleId, () => {
  loadRoleToForm();
  chatMessages.value = [];
  chatInput.value = '';
});

// 切换提供商时加载模型列表
watch(() => editForm.value.modelProviderId, async (newId, oldId) => {
  if (newId && newId !== oldId) {
    await fetchProviderModels(newId);
  } else if (!newId) {
    providerModels.value = [];
  }
});

async function fetchProviderModels(providerId: string) {
  loadingModels.value = true;
  providerModels.value = [];
  try {
    providerModels.value = await getProviderModelsApi(providerId);
  } catch {
    providerModels.value = [];
  } finally {
    loadingModels.value = false;
  }
}

function loadRoleToForm() {
  const role = selectedRole.value;
  if (!role) return;

  const config = configJson.value;

  editForm.value = {
    modelProviderId: role.modelProviderId ?? '',
    modelName: role.modelName ?? '',
    temperature: config.modelParameters?.temperature ?? 0.7,
    maxTokens: config.modelParameters?.maxTokens ?? 4096,
    tools: config.tools ?? [],
  };

  // 加载选中提供商的模型列表
  if (role.modelProviderId) {
    fetchProviderModels(role.modelProviderId);
  } else {
    providerModels.value = [];
  }
}

// ===== 保存配置 =====
async function saveConfig() {
  const role = selectedRole.value;
  if (!role) return;

  saving.value = true;
  try {
    const config = configJson.value;
    const updatedConfig = {
      ...config,
      modelParameters: {
        temperature: editForm.value.temperature,
        maxTokens: editForm.value.maxTokens,
      },
      tools: editForm.value.tools,
    };

    await updateAgentRoleApi(role.id, {
      modelProviderId: editForm.value.modelProviderId || undefined,
      modelName: editForm.value.modelName || undefined,
      config: JSON.stringify(updatedConfig),
    });

    // 刷新数据
    const updated = await getAgentRolesApi();
    roles.value = updated;
    message.success('配置已保存');
  } catch {
    message.error('保存失败');
  } finally {
    saving.value = false;
  }
}

// ===== 对话测试（基于 Session + SignalR Streaming） =====
const { streamSession } = useNotification();
let activeSubscription: { dispose: () => void } | null = null;

async function sendTestMessage() {
  const role = selectedRole.value;
  if (!role || !chatInput.value.trim()) return;

  const userMsg = chatInput.value.trim();
  chatMessages.value.push({ role: 'user', content: userMsg });
  chatInput.value = '';
  chatLoading.value = true;

  await nextTick();
  scrollToBottom();

  // 关闭之前的 Streaming 订阅
  if (activeSubscription) {
    activeSubscription.dispose();
    activeSubscription = null;
  }

  try {
    const session = await createSessionApi({
      projectId: '00000000-0000-0000-0000-000000000000',
      input: userMsg,
    });

    // 通过 SignalR Streaming 订阅会话事件
    let assistantContent = '';
    activeSubscription = streamSession(
      session.sessionId,
      (event) => {
        if (event.eventType === 'message' && event.payload) {
          try {
            const payload = typeof event.payload === 'string'
              ? JSON.parse(event.payload)
              : event.payload;
            if (payload.content) {
              assistantContent = payload.content;
              const lastMsg = chatMessages.value[chatMessages.value.length - 1];
              if (lastMsg && lastMsg.role === 'assistant' && lastMsg.streaming) {
                lastMsg.content = assistantContent;
              } else {
                chatMessages.value.push({
                  role: 'assistant',
                  content: assistantContent,
                  streaming: true,
                });
              }
              nextTick(() => scrollToBottom());
            }
          } catch {
            // ignore parse errors
          }
        } else if (event.eventType === 'session_error' || event.eventType === 'error') {
          chatMessages.value.push({
            role: 'assistant',
            content: `❌ ${event.payload || '未知错误'}`,
          });
        }
      },
      () => {
        // Streaming 完成
        chatLoading.value = false;
        const lastMsg = chatMessages.value[chatMessages.value.length - 1];
        if (lastMsg?.streaming) lastMsg.streaming = false;
        if (!assistantContent) {
          chatMessages.value.push({
            role: 'assistant',
            content: '（无响应）',
          });
        }
        nextTick(() => scrollToBottom());
      },
      () => {
        // Streaming 错误
        chatLoading.value = false;
        chatMessages.value.push({
          role: 'assistant',
          content: '❌ 连接失败',
        });
        nextTick(() => scrollToBottom());
      },
    );
  } catch (err: any) {
    chatMessages.value.push({
      role: 'assistant',
      content: `❌ 请求失败: ${err?.message || '网络错误'}`,
    });
    chatLoading.value = false;
    await nextTick();
    scrollToBottom();
  }
}

function scrollToBottom() {
  if (chatContainerRef.value) {
    chatContainerRef.value.scrollTop = chatContainerRef.value.scrollHeight;
  }
}

function handleChatKeydown(e: KeyboardEvent) {
  if (e.key === 'Enter' && !e.shiftKey) {
    e.preventDefault();
    sendTestMessage();
  }
}

function filterModelOption(input: string, option: any) {
  const search = input.toLowerCase();
  const val = (option?.value || '').toString().toLowerCase();
  return val.includes(search);
}
</script>

<template>
  <Page title="代理体配置">
    <Spin :spinning="loadingRoles">
      <Row :gutter="16" style="height: calc(100vh - 160px)">
        <!-- 左侧：代理体列表 + 配置 -->
        <Col :span="12" style="height: 100%; overflow-y: auto">
          <Card :body-style="{ padding: '0' }" size="small">
            <!-- 代理体选择列表 -->
            <Menu
              v-model:selectedKeys="selectedKeys"
              mode="horizontal"
              style="margin-bottom: 0"
            >
              <MenuItem
                v-for="role in roles"
                :key="role.id"
                @click="selectedRoleId = role.id"
              >
                <Space :size="4">
                  <span>{{ roleIcons[role.roleType] || '🤖' }}</span>
                  <span>{{ role.name }}</span>
                </Space>
              </MenuItem>
            </Menu>
          </Card>

          <!-- 角色配置详情 -->
          <Card v-if="selectedRole" class="mt-3" size="small">
            <template #title>
              <Space>
                <span class="text-xl">
                  {{ roleIcons[selectedRole.roleType] || '🤖' }}
                </span>
                <span>{{ selectedRole.name }}</span>
                <Tag v-if="selectedRole.isBuiltin" color="blue">内置</Tag>
              </Space>
            </template>

            <!-- 角色描述 -->
            <div class="mb-4">
              <Typography.Text type="secondary">
                {{ selectedRole.description || '无描述' }}
              </Typography.Text>
            </div>

            <Alert
              v-if="selectedRole.isBuiltin"
              class="mb-4"
              message="内置代理体仅允许修改模型配置和参数"
              show-icon
              type="info"
            />

            <Divider orientation="left" style="margin: 12px 0">
              模型配置
            </Divider>

            <Form :label-col="{ span: 6 }" size="small">
              <!-- 模型提供商 -->
              <FormItem label="模型提供商">
                <Select
                  v-model:value="editForm.modelProviderId"
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
                    <Tag v-if="p.defaultModel" color="default" style="margin-left: 8px">
                      {{ p.defaultModel }}
                    </Tag>
                  </SelectOption>
                </Select>
              </FormItem>

              <!-- 模型名称 -->
              <FormItem label="模型名称">
                <Select
                  v-model:value="editForm.modelName"
                  :loading="loadingModels"
                  :not-found-content="loadingModels ? undefined : undefined"
                  allow-clear
                  show-search
                  :filter-option="filterModelOption"
                  :placeholder="
                    selectedProvider?.defaultModel || '请先选择模型提供商'
                  "
                  :disabled="!editForm.modelProviderId"
                  style="width: 100%"
                >
                  <SelectOption
                    v-for="m in providerModels"
                    :key="m.id"
                    :value="m.id"
                  >
                    {{ m.id }}
                    <span v-if="m.displayName" style="color: #999; margin-left: 6px; font-size: 12px">
                      {{ m.displayName }}
                    </span>
                  </SelectOption>
                </Select>
                <Typography.Text
                  v-if="selectedProvider?.defaultModel && !editForm.modelName"
                  type="secondary"
                  style="font-size: 12px"
                >
                  留空将使用提供商默认模型：{{ selectedProvider.defaultModel }}
                </Typography.Text>
              </FormItem>

              <!-- 温度 -->
              <FormItem label="温度">
                <Row :gutter="16" align="middle">
                  <Col :span="16">
                    <Slider
                      v-model:value="editForm.temperature"
                      :max="2"
                      :min="0"
                      :step="0.1"
                    />
                  </Col>
                  <Col :span="8">
                    <InputNumber
                      v-model:value="editForm.temperature"
                      :max="2"
                      :min="0"
                      :step="0.1"
                      size="small"
                      style="width: 100%"
                    />
                  </Col>
                </Row>
              </FormItem>

              <!-- 最大 Token -->
              <FormItem label="最大 Token">
                <InputNumber
                  v-model:value="editForm.maxTokens"
                  :max="128000"
                  :min="256"
                  :step="256"
                  style="width: 100%"
                />
              </FormItem>
            </Form>

            <!-- 工具配置 -->
            <Divider orientation="left" style="margin: 12px 0">
              工具配置
            </Divider>

            <div v-if="availableTools.length > 0" class="mb-3">
              <Space wrap>
                <Tag
                  v-for="tool in availableTools"
                  :key="tool"
                  color="processing"
                >
                  🔧 {{ tool }}
                </Tag>
              </Space>
            </div>
            <Typography.Text v-else type="secondary">
              该代理体暂无已配置的工具
            </Typography.Text>

            <!-- MCP 配置（占位） -->
            <Divider orientation="left" style="margin: 12px 0">
              MCP 配置
            </Divider>

            <Typography.Text type="secondary">
              MCP Server 配置即将推出
            </Typography.Text>

            <!-- 保存按钮 -->
            <Divider style="margin: 12px 0" />
            <Button
              :loading="saving"
              block
              type="primary"
              @click="saveConfig"
            >
              保存配置
            </Button>
          </Card>

          <Empty v-else description="请选择一个代理体" />
        </Col>

        <!-- 右侧：对话测试 -->
        <Col :span="12" style="height: 100%; display: flex; flex-direction: column">
          <Card
            size="small"
            style="flex: 1; display: flex; flex-direction: column; overflow: hidden"
          >
            <template #title>
              <Space>
                <span>💬</span>
                <span>对话测试</span>
                <Tag v-if="selectedRole" color="default">
                  {{ selectedRole.name }}
                </Tag>
              </Space>
            </template>

            <template #extra>
              <Tooltip title="清空对话">
                <Button
                  size="small"
                  type="text"
                  @click="chatMessages = []"
                >
                  🗑️
                </Button>
              </Tooltip>
            </template>

            <!-- 消息列表 -->
            <div
              ref="chatContainerRef"
              style="flex: 1; overflow-y: auto; min-height: 300px; max-height: calc(100vh - 340px); padding: 8px 0"
            >
              <div v-if="chatMessages.length === 0" class="py-16 text-center">
                <Empty description="发送消息开始测试对话" />
              </div>

              <div
                v-for="(msg, idx) in chatMessages"
                :key="idx"
                :class="[
                  'mb-3 flex',
                  msg.role === 'user' ? 'justify-end' : 'justify-start',
                ]"
              >
                <div
                  :class="[
                    'max-w-[85%] rounded-lg px-4 py-2',
                    msg.role === 'user'
                      ? 'bg-blue-500 text-white'
                      : 'bg-gray-100 text-gray-800',
                  ]"
                  style="white-space: pre-wrap; word-break: break-word"
                >
                  {{ msg.content }}
                </div>
              </div>

              <!-- 加载中 -->
              <div v-if="chatLoading" class="mb-3 flex justify-start">
                <div class="rounded-lg bg-gray-100 px-4 py-2">
                  <Badge color="blue" status="processing" text="思考中..." />
                </div>
              </div>
            </div>

            <!-- 输入区域 -->
            <div
              style="
                border-top: 1px solid #f0f0f0;
                padding-top: 12px;
                margin-top: auto;
              "
            >
              <Space.Compact style="width: 100%">
                <Input
                  v-model:value="chatInput"
                  :disabled="chatLoading || !selectedRole"
                  placeholder="输入消息测试代理体..."
                  @keydown="handleChatKeydown"
                />
                <Button
                  :disabled="!chatInput.trim() || chatLoading || !selectedRole"
                  :loading="chatLoading"
                  type="primary"
                  @click="sendTestMessage"
                >
                  发送
                </Button>
              </Space.Compact>
            </div>
          </Card>
        </Col>
      </Row>
    </Spin>
  </Page>
</template>
