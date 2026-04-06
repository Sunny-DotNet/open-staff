<script lang="ts" setup>
import type { SettingsApi } from '#/api/openstaff/settings';

import { computed, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Button,
  message,
  Modal,
  Popconfirm,
  Space,
  Spin,
  Switch,
  Table,
  Tag,
  Typography,
} from 'ant-design-vue';

import {
  deleteProviderAccountApi,
  getProtocolsApi,
  getProviderAccountApi,
  getProviderAccountsApi,
  getProviderModelsApi,
  updateProviderAccountApi,
} from '#/api/openstaff/settings';
import { formatDateTime } from '#/utils/format';
import { getLogoUrl, getProtocolColor } from '#/constants/provider';

import DeviceAuthModal from './DeviceAuthModal.vue';
import ProviderFormModal from './ProviderFormModal.vue';

// ===== 状态 =====
const protocols = ref<SettingsApi.ProtocolMetadata[]>([]);
const accounts = ref<SettingsApi.ProviderAccount[]>([]);
const loading = ref(false);
const showFormModal = ref(false);
const editingAccount = ref<SettingsApi.ProviderAccount | null>(null);

// 设备码授权
const showDeviceAuth = ref(false);
const deviceAuthAccount = ref<SettingsApi.ProviderAccount | null>(null);

// 查看模型
const showModelsModal = ref(false);
const modelsLoading = ref(false);
const modelsList = ref<SettingsApi.ProviderModel[]>([]);
const modelsAccountName = ref('');

const modelsColumns = [
  { title: '模型 ID', dataIndex: 'id', key: 'id' },
  { title: '供应商', dataIndex: 'vendor', key: 'vendor', width: 150 },
  { title: '协议', dataIndex: 'protocols', key: 'protocols', width: 150 },
];

// ===== 表格列 =====
const columns = [
  { title: '名称', dataIndex: 'name', key: 'name', width: 200 },
  { title: '协议', dataIndex: 'protocolType', key: 'protocolType', width: 180 },
  { title: '状态', dataIndex: 'isEnabled', key: 'isEnabled', width: 100 },
  { title: '创建时间', dataIndex: 'createdAt', key: 'createdAt', width: 180 },
  { title: '操作', key: 'actions', width: 280, fixed: 'right' as const },
];

// ===== 计算属性 =====
const protocolMetaMap = computed(() => {
  const map = new Map<string, SettingsApi.ProtocolMetadata>();
  for (const p of protocols.value) {
    map.set(p.providerKey, p);
  }
  return map;
});

function getProtocolMeta(key: string) {
  return protocolMetaMap.value.get(key);
}

// ===== 数据加载 =====
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

// ===== 操作 =====
function openCreateModal() {
  editingAccount.value = null;
  showFormModal.value = true;
}

async function openEditModal(account: SettingsApi.ProviderAccount) {
  try {
    const detail = await getProviderAccountApi(account.id);
    editingAccount.value = detail;
    showFormModal.value = true;
  } catch {
    message.error('加载供应商详情失败');
  }
}

async function handleDelete(id: string) {
  try {
    await deleteProviderAccountApi(id);
    message.success('供应商已删除');
    await fetchData();
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : String(e);
    message.error('删除失败: ' + msg);
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

function startDeviceAuth(account: SettingsApi.ProviderAccount) {
  deviceAuthAccount.value = account;
  showDeviceAuth.value = true;
}

async function openModelsModal(account: SettingsApi.ProviderAccount) {
  modelsAccountName.value = account.name;
  modelsList.value = [];
  showModelsModal.value = true;
  modelsLoading.value = true;
  try {
    modelsList.value = await getProviderModelsApi(account.id);
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : String(e);
    message.error('加载模型列表失败: ' + msg);
  } finally {
    modelsLoading.value = false;
  }
}

onMounted(fetchData);
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
            <img
              v-if="getProtocolMeta(record.protocolType)?.logo"
              :src="getLogoUrl(getProtocolMeta(record.protocolType)!.logo)"
              :alt="record.protocolType"
              style="width: 20px; height: 20px; vertical-align: middle"
            />
            <span v-else style="font-size: 18px">🔌</span>
            <span style="font-weight: 500">{{ record.name }}</span>
          </Space>
        </template>

        <!-- 协议 -->
        <template v-if="column.key === 'protocolType'">
          <Tag :color="getProtocolColor(record.protocolType)">
            {{ getProtocolMeta(record.protocolType)?.providerName ?? record.protocolType }}
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
            {{ formatDateTime(record.createdAt) }}
          </Typography.Text>
        </template>

        <!-- 操作 -->
        <template v-if="column.key === 'actions'">
          <Space>
            <Button size="small" type="link" @click="openEditModal(record)">
              编辑
            </Button>
            <Button size="small" type="link" @click="openModelsModal(record)">
              模型
            </Button>
            <Button
              v-if="record.protocolType === 'github-copilot'"
              size="small"
              type="link"
              @click="startDeviceAuth(record)"
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
    <DeviceAuthModal
      :open="showDeviceAuth"
      :account="deviceAuthAccount"
      @update:open="showDeviceAuth = $event"
      @success="fetchData"
    />

    <!-- 新建/编辑 Modal -->
    <ProviderFormModal
      :open="showFormModal"
      :editing-account="editingAccount"
      :protocols="protocols"
      @update:open="showFormModal = $event"
      @saved="fetchData"
    />

    <!-- 查看模型 Modal -->
    <Modal
      :open="showModelsModal"
      :title="`${modelsAccountName} — 模型列表`"
      :footer="null"
      :width="700"
      @update:open="showModelsModal = $event"
    >
      <Spin :spinning="modelsLoading">
        <Table
          :columns="modelsColumns"
          :data-source="modelsList"
          :pagination="modelsList.length > 20 ? { pageSize: 20 } : false"
          row-key="id"
          size="small"
        >
          <template #emptyText>
            <Typography.Text v-if="!modelsLoading" type="secondary">
              暂无模型数据
            </Typography.Text>
          </template>
        </Table>
        <Typography.Text v-if="!modelsLoading && modelsList.length > 0" type="secondary" style="font-size: 12px">
          共 {{ modelsList.length }} 个模型
        </Typography.Text>
      </Spin>
    </Modal>
  </Page>
</template>
