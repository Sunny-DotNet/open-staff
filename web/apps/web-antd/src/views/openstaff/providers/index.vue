<script lang="ts" setup>
import type { SettingsApi } from '#/api/openstaff/settings';

import { computed, onMounted, onUnmounted, reactive, ref } from 'vue';

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
  Select,
  Space,
  Switch,
  Table,
  Tag,
  Typography,
} from 'ant-design-vue';

import {
  cancelDeviceAuthApi,
  createProviderAccountApi,
  deleteProviderAccountApi,
  getProtocolsApi,
  getProviderAccountsApi,
  initiateDeviceAuthApi,
  pollDeviceAuthApi,
  updateProviderAccountApi,
} from '#/api/openstaff/settings';

// 协议图标映射
const PROTOCOL_ICONS: Record<string, { icon: string; color: string }> = {
  openai: { icon: '🤖', color: '#10a37f' },
  anthropic: { icon: '🟠', color: '#d97706' },
  google: { icon: '🔷', color: '#4285f4' },
  'github-copilot': { icon: '🐙', color: '#6e40c9' },
  newapi: { icon: '🔌', color: '#1890ff' },
};

function getProtocolIcon(name: string) {
  return PROTOCOL_ICONS[name] ?? { icon: '🔌', color: '#8c8c8c' };
}

// 状态
const protocols = ref<SettingsApi.ProtocolMetadata[]>([]);
const accounts = ref<SettingsApi.ProviderAccount[]>([]);
const loading = ref(false);
const showModal = ref(false);
const editingAccount = ref<SettingsApi.ProviderAccount | null>(null);
const saving = ref(false);

// 表单
const formState = reactive({
  name: '',
  protocolType: '',
  isEnabled: true,
  envConfig: {} as Record<string, any>,
});

// 当前选中协议的元数据
const currentProtocol = computed(() =>
  protocols.value.find((p) => p.name === formState.protocolType),
);

// 设备码授权
const deviceAuth = reactive({
  accountId: null as string | null,
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
  { title: '名称', dataIndex: 'name', key: 'name', width: 200 },
  { title: '协议', dataIndex: 'protocolType', key: 'protocolType', width: 180 },
  { title: '状态', dataIndex: 'isEnabled', key: 'isEnabled', width: 100 },
  { title: '创建时间', dataIndex: 'createdAt', key: 'createdAt', width: 180 },
  { title: '操作', key: 'actions', width: 220, fixed: 'right' as const },
];

function getProtocolLabel(name: string) {
  const proto = protocols.value.find((p) => p.name === name);
  return proto?.providerName ?? name;
}

async function fetchData() {
  loading.value = true;
  try {
    const [protocolList, accountList] = await Promise.all([
      getProtocolsApi(),
      getProviderAccountsApi(),
    ]);
    protocols.value = protocolList;
    accounts.value = accountList;
  } catch {
    protocols.value = [];
    accounts.value = [];
  } finally {
    loading.value = false;
  }
}

function buildDefaultEnvConfig(proto: SettingsApi.ProtocolMetadata): Record<string, any> {
  const config: Record<string, any> = {};
  for (const field of proto.envSchema) {
    config[field.name] = field.defaultValue ?? (field.fieldType === 'boolean' ? false : '');
  }
  return config;
}

function openCreateModal() {
  editingAccount.value = null;
  const defaultProto = protocols.value[0];
  formState.protocolType = defaultProto?.name ?? '';
  formState.name = '';
  formState.isEnabled = true;
  formState.envConfig = defaultProto ? buildDefaultEnvConfig(defaultProto) : {};
  showModal.value = true;
}

function openEditModal(account: SettingsApi.ProviderAccount) {
  editingAccount.value = account;
  formState.name = account.name;
  formState.protocolType = account.protocolType;
  formState.isEnabled = account.isEnabled;
  // envConfig 从服务端获取的已包含非密钥字段
  formState.envConfig = { ...(account.envConfig ?? {}) };
  showModal.value = true;
}

function onProtocolTypeChange(name: string) {
  const proto = protocols.value.find((p) => p.name === name);
  if (proto && !editingAccount.value) {
    formState.envConfig = buildDefaultEnvConfig(proto);
  }
}

async function handleSave() {
  if (!formState.name.trim()) {
    message.warning('请输入供应商名称');
    return;
  }
  saving.value = true;
  try {
    // 过滤空密码字段（编辑时留空表示不修改）
    const envConfig = { ...formState.envConfig };
    if (editingAccount.value) {
      const proto = currentProtocol.value;
      if (proto) {
        for (const field of proto.envSchema) {
          if (field.fieldType === 'secret' && !envConfig[field.name]) {
            delete envConfig[field.name];
          }
        }
      }
      await updateProviderAccountApi(editingAccount.value.id, {
        name: formState.name,
        envConfig,
        isEnabled: formState.isEnabled,
      });
      message.success('供应商已更新');
    } else {
      await createProviderAccountApi({
        name: formState.name,
        protocolType: formState.protocolType,
        envConfig,
        isEnabled: formState.isEnabled,
      });
      message.success('供应商已创建');
    }
    showModal.value = false;
    await fetchData();
  } catch (e: any) {
    message.error('保存失败: ' + (e?.message || e));
  } finally {
    saving.value = false;
  }
}

async function handleDelete(id: string) {
  try {
    await deleteProviderAccountApi(id);
    message.success('供应商已删除');
    await fetchData();
  } catch (e: any) {
    message.error('删除失败: ' + (e?.message || e));
  }
}

async function handleToggleEnabled(account: SettingsApi.ProviderAccount) {
  try {
    await updateProviderAccountApi(account.id, {
      isEnabled: !account.isEnabled,
    });
    await fetchData();
  } catch {
    message.error('操作失败');
  }
}

// ===== 设备码授权 =====

async function startDeviceAuth(account: SettingsApi.ProviderAccount) {
  try {
    Object.assign(deviceAuth, {
      accountId: account.id,
      userCode: '',
      verificationUri: '',
      expiresIn: 0,
      interval: 5,
      polling: false,
      status: 'initiating',
    });

    const result = await initiateDeviceAuthApi(account.id);
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
    deviceAuth.accountId = null;
  }
}

function startPolling() {
  stopPolling();
  const interval = (deviceAuth.interval || 5) * 1000;
  pollTimer = setTimeout(doPollDeviceAuth, interval);
}

function stopPolling() {
  if (pollTimer) {
    clearTimeout(pollTimer);
    pollTimer = null;
  }
  deviceAuth.polling = false;
}

async function doPollDeviceAuth() {
  if (!deviceAuth.accountId) return;
  try {
    const result = await pollDeviceAuthApi(deviceAuth.accountId);
    if (result.status === 'success') {
      stopPolling();
      deviceAuth.status = 'success';
      message.success('GitHub 授权成功！');
      await fetchData();
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
  if (deviceAuth.accountId) {
    try {
      await cancelDeviceAuthApi(deviceAuth.accountId);
    } catch { /* ignore */ }
  }
  stopPolling();
  deviceAuth.status = '';
  deviceAuth.accountId = null;
}

function formatDate(dateStr: string) {
  if (!dateStr) return '';
  return new Date(dateStr).toLocaleString('zh-CN');
}

onMounted(fetchData);
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
      :data-source="accounts"
      :loading="loading"
      :pagination="false"
      row-key="id"
      :scroll="{ x: 900 }"
    >
      <template #bodyCell="{ column, record }">
        <!-- 名称 -->
        <template v-if="column.key === 'name'">
          <Space>
            <span style="font-size: 18px">
              {{ getProtocolIcon(record.protocolType).icon }}
            </span>
            <span style="font-weight: 500">{{ record.name }}</span>
          </Space>
        </template>

        <!-- 协议 -->
        <template v-if="column.key === 'protocolType'">
          <Tag :color="getProtocolIcon(record.protocolType).color">
            {{ getProtocolLabel(record.protocolType) }}
          </Tag>
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

        <!-- 创建时间 -->
        <template v-if="column.key === 'createdAt'">
          <Typography.Text type="secondary" style="font-size: 12px">
            {{ formatDate(record.createdAt) }}
          </Typography.Text>
        </template>

        <!-- 操作 -->
        <template v-if="column.key === 'actions'">
          <Space>
            <Button size="small" type="link" @click="openEditModal(record)">
              编辑
            </Button>
            <Button
              v-if="record.protocolType === 'github-copilot'"
              size="small"
              type="link"
              @click="startDeviceAuth(record)"
              :disabled="deviceAuth.accountId === record.id && deviceAuth.polling"
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

    <!-- 设备码授权 Modal -->
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
        <div style="font-size: 36px; font-weight: bold; letter-spacing: 6px; padding: 16px; background: var(--ant-color-fill-secondary, #f5f5f5); border-radius: 8px; margin: 16px 0">
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
      :title="editingAccount ? '编辑供应商' : '添加供应商'"
      :confirm-loading="saving"
      @ok="handleSave"
      :okText="editingAccount ? '保存' : '创建'"
      cancelText="取消"
      :width="560"
    >
      <Form layout="vertical" style="margin-top: 16px">
        <!-- 协议类型 -->
        <FormItem label="供应商协议" required>
          <Select
            v-model:value="formState.protocolType"
            :disabled="!!editingAccount"
            @change="onProtocolTypeChange"
            placeholder="选择协议类型"
          >
            <Select.Option
              v-for="proto in protocols"
              :key="proto.name"
              :value="proto.name"
            >
              <Space>
                <span>{{ getProtocolIcon(proto.name).icon }}</span>
                <span>{{ proto.providerName }}</span>
                <Tag v-if="proto.isVendor" style="font-size: 10px; margin-left: 4px">厂商</Tag>
              </Space>
            </Select.Option>
          </Select>
        </FormItem>

        <!-- 名称 -->
        <FormItem label="名称" required>
          <Input
            v-model:value="formState.name"
            placeholder="例如：我的 OpenAI 账户"
          />
        </FormItem>

        <!-- 动态 EnvConfig 字段 -->
        <template v-if="currentProtocol">
          <FormItem
            v-for="field in currentProtocol.envSchema"
            :key="field.name"
            :label="field.displayName"
            :required="field.isRequired"
          >
            <!-- boolean 字段 -->
            <Switch
              v-if="field.fieldType === 'boolean'"
              v-model:checked="formState.envConfig[field.name]"
            />

            <!-- secret 字段 -->
            <InputPassword
              v-else-if="field.fieldType === 'secret'"
              v-model:value="formState.envConfig[field.name]"
              :placeholder="editingAccount ? '留空保持不变' : `输入 ${field.displayName}`"
            />

            <!-- 普通 text 字段 -->
            <Input
              v-else
              v-model:value="formState.envConfig[field.name]"
              :placeholder="field.defaultValue ? String(field.defaultValue) : `输入 ${field.displayName}`"
            />
          </FormItem>
        </template>

        <!-- 启用开关 -->
        <FormItem label="启用">
          <Switch
            v-model:checked="formState.isEnabled"
            checked-children="启用"
            un-checked-children="禁用"
          />
        </FormItem>
      </Form>
    </Modal>
  </Page>
</template>
