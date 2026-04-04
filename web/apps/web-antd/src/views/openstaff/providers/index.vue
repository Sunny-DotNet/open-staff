<script lang="ts" setup>
import type { SettingsApi } from '#/api/openstaff/settings';

import { onMounted, onUnmounted, reactive, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Badge,
  Button,
  Form,
  FormItem,
  Input,
  InputPassword,
  message,
  Modal,
  Popconfirm,
  Radio,
  RadioGroup,
  Select,
  Space,
  Switch,
  Table,
  Tag,
  Typography,
} from 'ant-design-vue';

import {
  cancelDeviceAuthApi,
  createModelProviderApi,
  deleteModelProviderApi,
  getModelProvidersApi,
  initiateDeviceAuthApi,
  pollDeviceAuthApi,
  updateModelProviderApi,
} from '#/api/openstaff/settings';

// 供应商类型定义
const PROVIDER_TYPES = [
  {
    value: 'openai',
    label: 'OpenAI',
    icon: '🤖',
    color: '#10a37f',
    defaultBaseUrl: 'https://api.openai.com/v1',
    defaultModel: 'gpt-4o',
    defaultEnvVar: 'OPENAI_API_KEY',
  },
  {
    value: 'google',
    label: 'Google',
    icon: '🔷',
    color: '#4285f4',
    defaultBaseUrl: 'https://generativelanguage.googleapis.com/v1beta',
    defaultModel: 'gemini-2.0-flash',
    defaultEnvVar: 'GOOGLE_API_KEY',
  },
  {
    value: 'anthropic',
    label: 'Anthropic',
    icon: '🟠',
    color: '#d97706',
    defaultBaseUrl: 'https://api.anthropic.com/v1',
    defaultModel: 'claude-sonnet-4-20250514',
    defaultEnvVar: 'ANTHROPIC_API_KEY',
  },
  {
    value: 'github_copilot',
    label: 'GitHub Copilot',
    icon: '🐙',
    color: '#6e40c9',
    defaultBaseUrl: 'https://api.githubcopilot.com',
    defaultModel: 'gpt-4o',
    defaultEnvVar: '',
  },
  {
    value: 'azure_openai',
    label: 'Azure OpenAI',
    icon: '☁️',
    color: '#0078d4',
    defaultBaseUrl: '',
    defaultModel: 'gpt-4o',
    defaultEnvVar: 'AZURE_OPENAI_API_KEY',
  },
  {
    value: 'generic_openai',
    label: '通用 OpenAI 兼容',
    icon: '🔌',
    color: '#8c8c8c',
    defaultBaseUrl: '',
    defaultModel: '',
    defaultEnvVar: '',
  },
];

function getProviderTypeInfo(type: string) {
  return PROVIDER_TYPES.find((t) => t.value === type);
}

// 状态
const providers = ref<SettingsApi.ModelProvider[]>([]);
const loading = ref(false);
const showModal = ref(false);
const editingProvider = ref<SettingsApi.ModelProvider | null>(null);
const saving = ref(false);

const form = reactive({
  name: '',
  providerType: 'openai',
  baseUrl: '',
  apiKeyMode: 'input' as 'device' | 'env' | 'input',
  apiKeyEnvVar: '',
  apiKey: '',
  defaultModel: '',
  isEnabled: true,
});

// 设备码授权
const deviceAuth = reactive({
  providerId: null as string | null,
  userCode: '',
  verificationUri: '',
  expiresIn: 0,
  interval: 5,
  polling: false,
  status: '',
});
let pollTimer: ReturnType<typeof setTimeout> | null = null;

// 表格列
const columns = [
  {
    title: '名称',
    dataIndex: 'name',
    key: 'name',
    width: 200,
  },
  {
    title: '类型',
    dataIndex: 'providerType',
    key: 'providerType',
    width: 180,
  },
  {
    title: 'API Base URL',
    dataIndex: 'baseUrl',
    key: 'baseUrl',
    ellipsis: true,
  },
  {
    title: '默认模型',
    dataIndex: 'defaultModel',
    key: 'defaultModel',
    width: 200,
  },
  {
    title: '认证方式',
    dataIndex: 'apiKeyMode',
    key: 'apiKeyMode',
    width: 120,
  },
  {
    title: '状态',
    dataIndex: 'isEnabled',
    key: 'isEnabled',
    width: 100,
  },
  {
    title: '操作',
    key: 'actions',
    width: 200,
    fixed: 'right' as const,
  },
];

const apiKeyModeLabels: Record<string, string> = {
  input: '手动输入',
  env: '环境变量',
  device: '设备码授权',
};

async function fetchProviders() {
  loading.value = true;
  try {
    providers.value = await getModelProvidersApi();
  } catch {
    providers.value = [];
  } finally {
    loading.value = false;
  }
}

function openCreateModal() {
  editingProvider.value = null;
  const defaultType = PROVIDER_TYPES[0]!;
  Object.assign(form, {
    name: '',
    providerType: defaultType.value,
    baseUrl: defaultType.defaultBaseUrl,
    apiKeyMode: 'input',
    apiKeyEnvVar: defaultType.defaultEnvVar,
    apiKey: '',
    defaultModel: defaultType.defaultModel,
    isEnabled: true,
  });
  showModal.value = true;
}

function openEditModal(provider: SettingsApi.ModelProvider) {
  editingProvider.value = provider;
  Object.assign(form, {
    name: provider.name,
    providerType: provider.providerType,
    baseUrl: provider.baseUrl || '',
    apiKeyMode: provider.apiKeyMode,
    apiKeyEnvVar: provider.apiKeyEnvVar || '',
    apiKey: '',
    defaultModel: provider.defaultModel || '',
    isEnabled: provider.isEnabled,
  });
  showModal.value = true;
}

function onProviderTypeChange(type: string) {
  const info = getProviderTypeInfo(type);
  if (info && !editingProvider.value) {
    form.baseUrl = info.defaultBaseUrl;
    form.defaultModel = info.defaultModel;
    form.apiKeyEnvVar = info.defaultEnvVar;
    if (type === 'github_copilot') {
      form.apiKeyMode = 'device';
    } else {
      form.apiKeyMode = 'input';
    }
  }
}

async function handleSave() {
  if (!form.name.trim()) {
    message.warning('请输入供应商名称');
    return;
  }
  saving.value = true;
  try {
    if (editingProvider.value) {
      await updateModelProviderApi(editingProvider.value.id, {
        name: form.name,
        baseUrl: form.baseUrl,
        apiKeyMode: form.apiKeyMode,
        apiKeyEnvVar: form.apiKeyMode === 'env' ? form.apiKeyEnvVar : undefined,
        apiKey: form.apiKeyMode === 'input' && form.apiKey ? form.apiKey : undefined,
        defaultModel: form.defaultModel,
        isEnabled: form.isEnabled,
      });
      message.success('供应商已更新');
    } else {
      await createModelProviderApi({
        name: form.name,
        providerType: form.providerType,
        baseUrl: form.baseUrl,
        apiKeyMode: form.apiKeyMode,
        apiKeyEnvVar: form.apiKeyMode === 'env' ? form.apiKeyEnvVar : undefined,
        apiKey: form.apiKeyMode === 'input' && form.apiKey ? form.apiKey : undefined,
        defaultModel: form.defaultModel,
        isEnabled: form.isEnabled,
      });
      message.success('供应商已创建');
    }
    showModal.value = false;
    await fetchProviders();
  } catch (e: any) {
    message.error('保存失败: ' + (e?.message || e));
  } finally {
    saving.value = false;
  }
}

async function handleDelete(id: string) {
  try {
    await deleteModelProviderApi(id);
    message.success('供应商已删除');
    await fetchProviders();
  } catch (e: any) {
    message.error('删除失败: ' + (e?.message || e));
  }
}

async function handleToggleEnabled(provider: SettingsApi.ModelProvider) {
  try {
    await updateModelProviderApi(provider.id, {
      isEnabled: !provider.isEnabled,
    });
    await fetchProviders();
  } catch {
    message.error('操作失败');
  }
}

// 设备码授权
async function startDeviceAuth(provider: SettingsApi.ModelProvider) {
  try {
    Object.assign(deviceAuth, {
      providerId: provider.id,
      userCode: '',
      verificationUri: '',
      expiresIn: 0,
      interval: 5,
      polling: false,
      status: 'initiating',
    });

    const result = await initiateDeviceAuthApi(provider.id);
    deviceAuth.userCode = result.userCode;
    deviceAuth.verificationUri = result.verificationUri;
    deviceAuth.expiresIn = result.expiresIn;
    deviceAuth.interval = result.interval;
    deviceAuth.status = 'waiting';
    deviceAuth.polling = true;

    window.open(result.verificationUri, '_blank');
    startPolling();
  } catch {
    message.error('无法发起设备码授权');
    deviceAuth.status = '';
    deviceAuth.providerId = null;
  }
}

function startPolling() {
  stopPolling();
  const interval = (deviceAuth.interval || 5) * 1000;
  pollTimer = setTimeout(pollDeviceAuth, interval);
}

function stopPolling() {
  if (pollTimer) {
    clearTimeout(pollTimer);
    pollTimer = null;
  }
  deviceAuth.polling = false;
}

async function pollDeviceAuth() {
  if (!deviceAuth.providerId) return;
  try {
    const result = await pollDeviceAuthApi(deviceAuth.providerId);
    if (result.status === 'success') {
      stopPolling();
      deviceAuth.status = 'success';
      message.success('GitHub 授权成功！');
      await fetchProviders();
    } else if (result.status === 'pending') {
      startPolling();
    } else {
      stopPolling();
      deviceAuth.status = result.status;
      message.error(result.message || '授权失败');
    }
  } catch {
    stopPolling();
    deviceAuth.status = 'error';
  }
}

async function cancelAuth() {
  if (deviceAuth.providerId) {
    try {
      await cancelDeviceAuthApi(deviceAuth.providerId);
    } catch { /* ignore */ }
  }
  stopPolling();
  deviceAuth.status = '';
  deviceAuth.providerId = null;
}

onMounted(fetchProviders);
onUnmounted(stopPolling);
</script>

<template>
  <Page title="供应商管理">
    <template #extra>
      <Button type="primary" @click="openCreateModal">
        ＋ 添加供应商
      </Button>
    </template>

    <Table
      :columns="columns"
      :data-source="providers"
      :loading="loading"
      :pagination="false"
      row-key="id"
      :scroll="{ x: 1100 }"
    >
      <template #bodyCell="{ column, record }">
        <!-- 名称 -->
        <template v-if="column.key === 'name'">
          <Space>
            <span style="font-size: 18px">
              {{ getProviderTypeInfo(record.providerType)?.icon || '🔌' }}
            </span>
            <span style="font-weight: 500">{{ record.name }}</span>
          </Space>
        </template>

        <!-- 类型 -->
        <template v-if="column.key === 'providerType'">
          <Tag :color="getProviderTypeInfo(record.providerType)?.color || '#8c8c8c'">
            {{ getProviderTypeInfo(record.providerType)?.label || record.providerType }}
          </Tag>
        </template>

        <!-- 认证方式 -->
        <template v-if="column.key === 'apiKeyMode'">
          <Space direction="vertical" :size="0">
            <span>{{ apiKeyModeLabels[record.apiKeyMode] || record.apiKeyMode }}</span>
            <Typography.Text v-if="record.apiKeyMode === 'env' && record.apiKeyEnvVar" type="secondary" style="font-size: 11px">
              {{ record.apiKeyEnvVar }}
            </Typography.Text>
            <Badge v-if="record.hasApiKey" status="success" text="已配置" />
            <Badge v-else status="warning" text="未配置" />
          </Space>
        </template>

        <!-- 状态 -->
        <template v-if="column.key === 'isEnabled'">
          <Switch
            :checked="record.isEnabled"
            @change="handleToggleEnabled(record)"
            checked-children="启用"
            un-checked-children="禁用"
          />
        </template>

        <!-- 操作 -->
        <template v-if="column.key === 'actions'">
          <Space>
            <Button size="small" type="link" @click="openEditModal(record)">
              编辑
            </Button>
            <Button
              v-if="record.providerType === 'github_copilot'"
              size="small"
              type="link"
              @click="startDeviceAuth(record)"
              :disabled="deviceAuth.providerId === record.id && deviceAuth.polling"
            >
              🐙 授权
            </Button>
            <Popconfirm
              title="确认删除该供应商？"
              @confirm="handleDelete(record.id)"
            >
              <Button size="small" type="link" danger>删除</Button>
            </Popconfirm>
          </Space>
        </template>
      </template>

      <template #emptyText>
        <div style="padding: 40px 0; text-align: center">
          <Typography.Text type="secondary" style="font-size: 16px">
            👋 还没有配置任何供应商
          </Typography.Text>
          <br />
          <Typography.Text type="secondary">
            点击「添加供应商」开始配置 AI 模型服务
          </Typography.Text>
          <br /><br />
          <Button type="primary" @click="openCreateModal">
            添加供应商
          </Button>
        </div>
      </template>
    </Table>

    <!-- 设备码授权提示 -->
    <Modal
      :open="deviceAuth.status === 'waiting'"
      title="GitHub 设备码授权"
      :footer="null"
      @cancel="cancelAuth"
    >
      <div style="text-align: center; padding: 20px 0">
        <Typography.Title :level="4">
          请在 GitHub 中输入以下代码：
        </Typography.Title>
        <div style="font-size: 36px; font-weight: bold; letter-spacing: 6px; padding: 16px; background: #f5f5f5; border-radius: 8px; margin: 16px 0">
          {{ deviceAuth.userCode }}
        </div>
        <Typography.Text type="secondary">
          浏览器应已自动打开验证页面
        </Typography.Text>
        <br />
        <Button
          type="link"
          :href="deviceAuth.verificationUri"
          target="_blank"
          style="margin-top: 8px"
        >
          手动打开验证页面 →
        </Button>
        <br /><br />
        <Badge status="processing" text="等待授权中..." />
        <br /><br />
        <Button @click="cancelAuth">取消</Button>
      </div>
    </Modal>

    <!-- 新建/编辑 Modal -->
    <Modal
      v-model:open="showModal"
      :title="editingProvider ? '编辑供应商' : '添加供应商'"
      :confirm-loading="saving"
      @ok="handleSave"
      :okText="editingProvider ? '保存' : '创建'"
      cancelText="取消"
      :width="560"
    >
      <Form layout="vertical" style="margin-top: 16px">
        <FormItem label="供应商类型" required>
          <Select
            v-model:value="form.providerType"
            :disabled="!!editingProvider"
            @change="onProviderTypeChange"
          >
            <Select.Option
              v-for="pt in PROVIDER_TYPES"
              :key="pt.value"
              :value="pt.value"
            >
              <Space>
                <span>{{ pt.icon }}</span>
                <span>{{ pt.label }}</span>
              </Space>
            </Select.Option>
          </Select>
        </FormItem>

        <FormItem label="名称" required>
          <Input
            v-model:value="form.name"
            placeholder="例如：我的 OpenAI 账户"
          />
        </FormItem>

        <FormItem label="API Base URL">
          <Input
            v-model:value="form.baseUrl"
            placeholder="https://api.openai.com/v1"
          />
        </FormItem>

        <FormItem label="默认模型">
          <Input
            v-model:value="form.defaultModel"
            placeholder="gpt-4o"
          />
        </FormItem>

        <FormItem label="认证方式">
          <RadioGroup v-model:value="form.apiKeyMode">
            <Radio value="input">手动输入 API Key</Radio>
            <Radio value="env">环境变量</Radio>
            <Radio
              v-if="form.providerType === 'github_copilot'"
              value="device"
            >
              GitHub 设备码授权
            </Radio>
          </RadioGroup>
        </FormItem>

        <FormItem v-if="form.apiKeyMode === 'input'" label="API Key">
          <InputPassword
            v-model:value="form.apiKey"
            :placeholder="editingProvider?.hasApiKey ? '留空保持不变' : '输入 API Key'"
          />
        </FormItem>

        <FormItem v-if="form.apiKeyMode === 'env'" label="环境变量名">
          <Input
            v-model:value="form.apiKeyEnvVar"
            placeholder="OPENAI_API_KEY"
          />
        </FormItem>

        <FormItem v-if="form.apiKeyMode === 'device'">
          <Typography.Text type="secondary">
            保存后，点击表格中的「🐙 授权」按钮进行 GitHub 设备码授权。
          </Typography.Text>
        </FormItem>

        <FormItem label="启用">
          <Switch
            v-model:checked="form.isEnabled"
            checked-children="启用"
            un-checked-children="禁用"
          />
        </FormItem>
      </Form>
    </Modal>
  </Page>
</template>
