<script lang="ts" setup>
import type { AgentApi } from '#/api/openstaff/agent';
import type { SettingsApi } from '#/api/openstaff/settings';

import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Badge,
  Button,
  Card,
  Col,
  Collapse,
  CollapsePanel,
  Divider,
  Drawer,
  Empty,
  Form,
  FormItem,
  Input,
  InputNumber,
  message,
  Modal,
  Popconfirm,
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
const CheckableTag = Tag.CheckableTag as any;

import {
  createAgentRoleApi,
  deleteAgentRoleApi,
  getAgentRolesApi,
  testAgentChatApi,
  updateAgentRoleApi,
} from '#/api/openstaff/agent';
import { useNotification } from '#/composables/useNotification';
import {
  getModelProvidersApi,
  getProviderModelsApi,
} from '#/api/openstaff/settings';

const { streamSession } = useNotification();

// ===== 类型定义 =====
interface SoulConfig {
  traits?: string[];
  style?: string;
  attitudes?: string[];
  custom?: string;
}

interface EditFormState {
  name: string;
  description: string;
  modelProviderId: string;
  modelName: string;
  temperature: number;
  maxTokens: number;
  tools: string[];
  soul: SoulConfig;
}

// ===== 常量 =====
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

const TRAIT_OPTIONS = ['严谨', '幽默', '友善', '直率', '耐心', '果断', '细腻', '冷静'];
const STYLE_OPTIONS = ['正式', '轻松', '技术流', '导师型', '鼓励型'];
const ATTITUDE_OPTIONS = ['追求完美', '高效优先', '注重细节', '创新思维', '团队协作'];

// ===== 状态 =====
const roles = ref<AgentApi.AgentRole[]>([]);
const providers = ref<SettingsApi.ModelProvider[]>([]);
const loadingRoles = ref(false);
const saving = ref(false);

// Drawer 状态
const drawerVisible = ref(false);
const drawerMode = ref<'create' | 'edit'>('edit');
const editingRoleId = ref<string>('');

// 编辑表单
const editForm = ref<EditFormState>({
  name: '',
  description: '',
  modelProviderId: '',
  modelName: '',
  temperature: 0.7,
  maxTokens: 4096,
  tools: [],
  soul: { traits: [], style: '', attitudes: [], custom: '' },
});

// 模型列表
const providerModels = ref<SettingsApi.ProviderModel[]>([]);
const loadingModels = ref(false);

// 对话测试 Modal
const chatModalVisible = ref(false);
const chatRoleId = ref<string>('');
const chatRoleName = ref<string>('');
const chatMessages = ref<
  Array<{ content: string; role: 'assistant' | 'user'; streaming?: boolean }>
>([]);
const chatInput = ref('');
const chatLoading = ref(false);
const chatContainerRef = ref<HTMLElement | null>(null);
const currentSubscription = ref<any>(null);

// ===== 计算属性 =====
const editingRole = computed(() =>
  roles.value.find((r) => r.id === editingRoleId.value),
);

const enabledProviders = computed(() =>
  providers.value.filter((p) => p.isEnabled),
);

const selectedProvider = computed(() =>
  providers.value.find((p) => p.id === editForm.value.modelProviderId),
);

const soulPromptPreview = computed(() => generateSoulPrompt(editForm.value.soul));

// ===== 灵魂 prompt 生成 =====
function generateSoulPrompt(soul: SoulConfig): string {
  const parts: string[] = [];
  if (soul.traits?.length) parts.push(`你的性格特征：${soul.traits.join('、')}`);
  if (soul.style) parts.push(`你的沟通风格：${soul.style}`);
  if (soul.attitudes?.length)
    parts.push(`你的工作态度：${soul.attitudes.join('、')}`);
  if (soul.custom) parts.push(soul.custom);
  return parts.length > 0 ? `${parts.join('。')}。` : '';
}

// ===== 解析 config =====
function parseConfig(configStr: string | null): AgentApi.AgentRoleConfig & { soul?: SoulConfig } {
  try {
    return configStr ? JSON.parse(configStr) : {};
  } catch {
    return {};
  }
}

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
  } finally {
    loadingRoles.value = false;
  }
});

// 切换提供商时加载模型列表
watch(
  () => editForm.value.modelProviderId,
  async (newId, oldId) => {
    if (newId && newId !== oldId) {
      await fetchProviderModels(newId);
    } else if (!newId) {
      providerModels.value = [];
    }
  },
);

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

// ===== 卡片 / Drawer =====
function getRoleIcon(roleType: string): string {
  return roleIcons[roleType] || '🤖';
}

function openConfigDrawer(role: AgentApi.AgentRole) {
  drawerMode.value = 'edit';
  editingRoleId.value = role.id;
  loadRoleToForm(role);
  drawerVisible.value = true;
}

function openCreateDrawer() {
  drawerMode.value = 'create';
  editingRoleId.value = '';
  editForm.value = {
    name: '',
    description: '',
    modelProviderId: '',
    modelName: '',
    temperature: 0.7,
    maxTokens: 4096,
    tools: [],
    soul: { traits: [], style: '', attitudes: [], custom: '' },
  };
  providerModels.value = [];
  drawerVisible.value = true;
}

function closeDrawer() {
  drawerVisible.value = false;
}

function loadRoleToForm(role: AgentApi.AgentRole) {
  const config = parseConfig(role.config);

  editForm.value = {
    name: role.name,
    description: role.description ?? '',
    modelProviderId: role.modelProviderId ?? '',
    modelName: role.modelName ?? '',
    temperature: config.modelParameters?.temperature ?? 0.7,
    maxTokens: config.modelParameters?.maxTokens ?? 4096,
    tools: config.tools ?? [],
    soul: {
      traits: config.soul?.traits ?? [],
      style: config.soul?.style ?? '',
      attitudes: config.soul?.attitudes ?? [],
      custom: config.soul?.custom ?? '',
    },
  };

  if (role.modelProviderId) {
    fetchProviderModels(role.modelProviderId);
  } else {
    providerModels.value = [];
  }
}

// ===== Checkable tag helpers =====
function toggleTrait(tag: string) {
  const idx = editForm.value.soul.traits?.indexOf(tag) ?? -1;
  if (!editForm.value.soul.traits) editForm.value.soul.traits = [];
  if (idx >= 0) {
    editForm.value.soul.traits.splice(idx, 1);
  } else {
    editForm.value.soul.traits.push(tag);
  }
}

function toggleAttitude(tag: string) {
  const idx = editForm.value.soul.attitudes?.indexOf(tag) ?? -1;
  if (!editForm.value.soul.attitudes) editForm.value.soul.attitudes = [];
  if (idx >= 0) {
    editForm.value.soul.attitudes.splice(idx, 1);
  } else {
    editForm.value.soul.attitudes.push(tag);
  }
}

// ===== 保存配置 =====
async function saveConfig() {
  if (drawerMode.value === 'create') {
    await createRole();
    return;
  }
  const role = editingRole.value;
  if (!role) return;

  saving.value = true;
  try {
    const existingConfig = parseConfig(role.config);
    const soulPrompt = generateSoulPrompt(editForm.value.soul);
    const updatedConfig = {
      ...existingConfig,
      modelParameters: {
        temperature: editForm.value.temperature,
        maxTokens: editForm.value.maxTokens,
      },
      tools: editForm.value.tools,
      soul: editForm.value.soul,
    };

    const updateData: AgentApi.UpdateAgentRoleParams = {
      modelProviderId: editForm.value.modelProviderId || undefined,
      modelName: editForm.value.modelName || undefined,
      config: JSON.stringify(updatedConfig),
    };

    // Non-builtin roles can also update name/description/systemPrompt
    if (!role.isBuiltin) {
      updateData.name = editForm.value.name;
      updateData.description = editForm.value.description;
      if (soulPrompt) {
        updateData.systemPrompt = soulPrompt + (role.systemPrompt ?? '');
      }
    } else if (soulPrompt) {
      updateData.systemPrompt = soulPrompt + (role.systemPrompt ?? '');
    }

    await updateAgentRoleApi(role.id, updateData);
    const updated = await getAgentRolesApi();
    roles.value = updated;
    message.success('配置已保存');
    closeDrawer();
  } catch {
    message.error('保存失败');
  } finally {
    saving.value = false;
  }
}

// ===== 创建角色 =====
async function createRole() {
  if (!editForm.value.name.trim()) {
    message.warning('请输入员工名称');
    return;
  }

  saving.value = true;
  try {
    const roleType = editForm.value.name
      .trim()
      .toLowerCase()
      .replace(/\s+/g, '_')
      .replace(/[^a-z0-9_\u4e00-\u9fff]/g, '');
    const soulPrompt = generateSoulPrompt(editForm.value.soul);
    const config = {
      modelParameters: {
        temperature: editForm.value.temperature,
        maxTokens: editForm.value.maxTokens,
      },
      tools: editForm.value.tools,
      soul: editForm.value.soul,
    };

    await createAgentRoleApi({
      name: editForm.value.name.trim(),
      roleType,
      description: editForm.value.description || undefined,
      systemPrompt: soulPrompt || undefined,
      modelProviderId: editForm.value.modelProviderId || undefined,
      modelName: editForm.value.modelName || undefined,
      config: JSON.stringify(config),
    });

    const updated = await getAgentRolesApi();
    roles.value = updated;
    message.success('员工创建成功');
    closeDrawer();
  } catch {
    message.error('创建失败');
  } finally {
    saving.value = false;
  }
}

// ===== 删除角色 =====
async function deleteRole(role: AgentApi.AgentRole) {
  try {
    await deleteAgentRoleApi(role.id);
    const updated = await getAgentRolesApi();
    roles.value = updated;
    message.success('已删除');
    if (editingRoleId.value === role.id) {
      closeDrawer();
    }
  } catch {
    message.error('删除失败');
  }
}

// ===== 对话测试 Modal =====
function openChatModal(role: AgentApi.AgentRole) {
  chatRoleId.value = role.id;
  chatRoleName.value = role.name;
  chatMessages.value = [];
  chatInput.value = '';
  chatLoading.value = false;
  chatModalVisible.value = true;
}

function closeChatModal() {
  chatModalVisible.value = false;
  chatMessages.value = [];
  chatInput.value = '';
  chatLoading.value = false;
  if (currentSubscription.value) {
    currentSubscription.value.dispose();
    currentSubscription.value = null;
  }
}

async function sendTestMessage() {
  if (!chatRoleId.value || !chatInput.value.trim()) return;

  const userMsg = chatInput.value.trim();
  chatMessages.value.push({ role: 'user', content: userMsg });
  chatInput.value = '';
  chatLoading.value = true;

  await nextTick();
  scrollToBottom();

  try {
    const { sessionId } = await testAgentChatApi(chatRoleId.value, userMsg);
    const assistantIdx = chatMessages.value.length;
    chatMessages.value.push({
      role: 'assistant',
      content: '思考中...',
      streaming: true,
    });

    currentSubscription.value = await streamSession(
      sessionId,
      (evt) => {
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
          // ignore
        }
        nextTick(() => scrollToBottom());
      },
      () => {
        chatLoading.value = false;
        currentSubscription.value = null;
        if (chatMessages.value[assistantIdx]?.streaming) {
          chatMessages.value[assistantIdx]!.streaming = false;
        }
        nextTick(() => scrollToBottom());
      },
      (err) => {
        chatLoading.value = false;
        currentSubscription.value = null;
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
  } catch (err: any) {
    chatMessages.value.push({
      role: 'assistant',
      content: `❌ 请求失败: ${err?.response?.data?.error || err?.message || '网络错误'}`,
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

onUnmounted(() => {
  if (currentSubscription.value) {
    currentSubscription.value.dispose();
    currentSubscription.value = null;
  }
});
</script>

<template>
  <Page title="员工管理">
    <Spin :spinning="loadingRoles">
      <!-- 顶部操作栏 -->
      <div style="margin-bottom: 20px; display: flex; justify-content: space-between; align-items: center">
        <Typography.Title :level="5" style="margin: 0">
          团队成员（{{ roles.length }}）
        </Typography.Title>
        <Button type="primary" @click="openCreateDrawer">
          ＋ 新增员工
        </Button>
      </div>

      <!-- 空状态 -->
      <Empty v-if="!loadingRoles && roles.length === 0" description="暂无员工，点击上方按钮创建" />

      <!-- 卡片网格 -->
      <Row :gutter="[16, 16]">
        <Col
          v-for="role in roles"
          :key="role.id"
          :lg="6"
          :md="8"
          :sm="12"
          :xs="24"
        >
          <Card
            hoverable
            class="staff-card"
            :body-style="{ padding: '20px' }"
            @click="openConfigDrawer(role)"
          >
            <!-- 卡片头部：图标 + 标签 -->
            <div style="display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 12px">
              <span style="font-size: 36px; line-height: 1">
                {{ getRoleIcon(role.roleType) }}
              </span>
              <Space :size="4">
                <Tag v-if="role.isBuiltin" color="blue">内置</Tag>
                <Tag v-if="role.modelProviderName" color="green">
                  {{ role.modelProviderName }}
                </Tag>
              </Space>
            </div>

            <!-- 名称 -->
            <Typography.Title :level="5" :ellipsis="true" style="margin-bottom: 4px">
              {{ role.name }}
            </Typography.Title>

            <!-- 描述 -->
            <Typography.Paragraph
              type="secondary"
              :ellipsis="{ rows: 2 }"
              :content="role.description || '暂无描述'"
              style="margin-bottom: 12px; font-size: 13px; min-height: 40px"
            />

            <!-- 角色类型标签 -->
            <div style="margin-bottom: 12px">
              <Tag color="default">{{ role.roleType }}</Tag>
            </div>

            <!-- 操作按钮 -->
            <div style="display: flex; gap: 8px" @click.stop>
              <Button size="small" type="primary" ghost @click="openChatModal(role)">
                💬 对话测试
              </Button>
              <Popconfirm
                v-if="!role.isBuiltin"
                title="确定要删除该员工吗？"
                ok-text="确定"
                cancel-text="取消"
                @confirm="deleteRole(role)"
              >
                <Button size="small" danger>删除</Button>
              </Popconfirm>
            </div>
          </Card>
        </Col>
      </Row>
    </Spin>

    <!-- ===== 配置 Drawer ===== -->
    <Drawer
      :open="drawerVisible"
      :title="undefined"
      :width="560"
      placement="right"
      :destroy-on-close="true"
      @close="closeDrawer"
    >
      <template #title>
        <Space>
          <span style="font-size: 24px">
            {{ drawerMode === 'create' ? '🆕' : getRoleIcon(editingRole?.roleType ?? '') }}
          </span>
          <span style="font-size: 16px; font-weight: 600">
            {{ drawerMode === 'create' ? '新增员工' : (editingRole?.name ?? '') }}
          </span>
          <Tag v-if="drawerMode === 'edit' && editingRole?.isBuiltin" color="blue">内置</Tag>
        </Space>
      </template>

      <!-- 基本信息区 -->
      <Divider orientation="left" style="margin: 0 0 16px 0">基本信息</Divider>
      <Form :label-col="{ span: 6 }" size="small">
        <FormItem label="名称" required>
          <Input
            v-model:value="editForm.name"
            :readonly="drawerMode === 'edit' && (editingRole?.isBuiltin ?? false)"
            placeholder="请输入员工名称"
          />
        </FormItem>
        <FormItem label="描述 / 职位说明">
          <Input.TextArea
            v-model:value="editForm.description"
            :readonly="drawerMode === 'edit' && (editingRole?.isBuiltin ?? false)"
            :rows="3"
            placeholder="请输入描述"
          />
        </FormItem>
      </Form>

      <!-- 灵魂配置区 -->
      <Divider orientation="left" style="margin: 8px 0 16px 0">灵魂配置</Divider>

      <div style="margin-bottom: 16px">
        <div style="margin-bottom: 8px; font-weight: 500">🎭 性格特征</div>
        <Space :size="[4, 8]" wrap>
          <CheckableTag
            v-for="t in TRAIT_OPTIONS"
            :key="t"
            :checked="editForm.soul.traits?.includes(t) ?? false"
            @change="toggleTrait(t)"
          >
            {{ t }}
          </CheckableTag>
        </Space>
      </div>

      <div style="margin-bottom: 16px">
        <div style="margin-bottom: 8px; font-weight: 500">🗣️ 沟通风格</div>
        <Select
          v-model:value="editForm.soul.style"
          allow-clear
          placeholder="选择沟通风格"
          style="width: 100%"
        >
          <SelectOption v-for="s in STYLE_OPTIONS" :key="s" :value="s">
            {{ s }}
          </SelectOption>
        </Select>
      </div>

      <div style="margin-bottom: 16px">
        <div style="margin-bottom: 8px; font-weight: 500">🎯 工作态度</div>
        <Space :size="[4, 8]" wrap>
          <CheckableTag
            v-for="a in ATTITUDE_OPTIONS"
            :key="a"
            :checked="editForm.soul.attitudes?.includes(a) ?? false"
            @change="toggleAttitude(a)"
          >
            {{ a }}
          </CheckableTag>
        </Space>
      </div>

      <div style="margin-bottom: 16px">
        <div style="margin-bottom: 8px; font-weight: 500">📖 自定义性格描述</div>
        <Input.TextArea
          v-model:value="editForm.soul.custom"
          :rows="2"
          placeholder="（可选）自由描述该员工的个性特点…"
        />
      </div>

      <!-- 灵魂 prompt 预览 -->
      <div
        v-if="soulPromptPreview"
        style="
          margin-bottom: 16px;
          padding: 10px 12px;
          background: #f6f8fa;
          border-radius: 6px;
          font-size: 12px;
          color: #666;
          line-height: 1.6;
        "
      >
        <strong>生成的灵魂 Prompt 预览：</strong><br />
        {{ soulPromptPreview }}
      </div>

      <!-- 模型配置区 -->
      <Divider orientation="left" style="margin: 8px 0 16px 0">模型配置</Divider>
      <Form :label-col="{ span: 6 }" size="small">
        <FormItem label="供应商">
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

        <FormItem label="模型">
          <Select
            v-model:value="editForm.modelName"
            :disabled="!editForm.modelProviderId"
            :filter-option="filterModelOption"
            :loading="loadingModels"
            :placeholder="selectedProvider?.defaultModel || '请先选择供应商'"
            allow-clear
            show-search
            style="width: 100%"
          >
            <SelectOption
              v-for="m in providerModels"
              :key="m.id"
              :value="m.id"
            >
              {{ m.id }}
              <span
                v-if="m.displayName"
                style="color: #999; margin-left: 6px; font-size: 12px"
              >
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

      <!-- 工具配置区 -->
      <Collapse :bordered="false" style="background: transparent">
        <CollapsePanel key="tools" header="🔧 工具配置">
          <div v-if="editForm.tools.length > 0">
            <Space wrap>
              <Tag
                v-for="tool in editForm.tools"
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
      </Collapse>

      <!-- 底部保存 -->
      <Divider style="margin: 16px 0" />
      <Button :loading="saving" block type="primary" size="large" @click="saveConfig">
        {{ drawerMode === 'create' ? '创建员工' : '保存配置' }}
      </Button>
    </Drawer>

    <!-- ===== 对话测试 Modal ===== -->
    <Modal
      :open="chatModalVisible"
      :title="`对话测试 - ${chatRoleName}`"
      :footer="null"
      :width="'80%'"
      :body-style="{ padding: 0, display: 'flex', flexDirection: 'column', height: '70vh' }"
      :destroy-on-close="true"
      @cancel="closeChatModal"
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
                ? { background: '#1677ff', color: '#fff' }
                : { background: '#f5f5f5', color: '#333' }),
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
              background: #f5f5f5;
            "
          >
            <Badge color="blue" status="processing" text="思考中..." />
          </div>
        </div>
      </div>

      <!-- 输入区域 -->
      <div
        style="
          border-top: 1px solid #f0f0f0;
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
          @keydown="handleChatKeydown"
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
          <Button
            @click="() => {
              if (currentSubscription) {
                currentSubscription.dispose();
                currentSubscription = null;
              }
              chatLoading = false;
              chatMessages = [];
            }"
          >
            🗑️
          </Button>
        </Tooltip>
      </div>
    </Modal>
  </Page>
</template>

<style scoped>
.staff-card {
  border-radius: 10px;
  transition: all 0.3s ease;
  border: 1px solid #f0f0f0;
}

.staff-card:hover {
  box-shadow: 0 6px 20px rgba(0, 0, 0, 0.08);
  transform: translateY(-2px);
}
</style>
