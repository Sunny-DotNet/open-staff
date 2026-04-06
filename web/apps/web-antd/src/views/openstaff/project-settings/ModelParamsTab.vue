<script lang="ts" setup>
import type { SettingsApi } from '#/api/openstaff/settings';

import { onMounted, ref } from 'vue';

import {
  Button,
  Card,
  Divider,
  Form,
  Input,
  message,
  Popconfirm,
  Select,
  Table,
  Typography,
} from 'ant-design-vue';

import { updateProjectApi } from '#/api/openstaff/project';
import { getProviderAccountsApi } from '#/api/openstaff/settings';
import { useProviderModels } from '#/composables/useProviderModels';

const props = defineProps<{
  projectId: string;
  saving: boolean;
  initialProviderId: string | null;
  initialModelName: string;
  initialExtraConfig: string | null;
}>();

const emit = defineEmits<{
  (e: 'update:saving', value: boolean): void;
}>();

const providers = ref<SettingsApi.ProviderAccount[]>([]);
const defaultProviderId = ref<string | null>(props.initialProviderId);
const defaultModelName = ref(props.initialModelName);
const extraConfigList = ref<{ key: string; value: string }[]>([]);

const providerIdRef = computed(() => defaultProviderId.value ?? '');
const {
  models: providerModels,
  loading: loadingModels,
  error: modelsError,
  ensureLoaded: ensureModelsLoaded,
} = useProviderModels(providerIdRef);

const enabledProviders = computed(() =>
  providers.value.filter((p) => p.isEnabled),
);

// 初始化 extraConfig
function parseExtraConfig(configStr: string | null) {
  try {
    const parsed = configStr ? JSON.parse(configStr) : {};
    extraConfigList.value = Object.entries(parsed).map(([key, value]) => ({
      key,
      value: String(value),
    }));
  } catch {
    extraConfigList.value = [];
  }
}

function onProviderChange(providerId: string | null) {
  defaultModelName.value = '';
}

function addExtraParam() {
  extraConfigList.value.push({ key: '', value: '' });
}

function removeExtraParam(index: number) {
  extraConfigList.value.splice(index, 1);
}

async function saveModelAndParams() {
  emit('update:saving', true);
  try {
    const extraObj: Record<string, string> = {};
    for (const item of extraConfigList.value) {
      if (item.key.trim()) {
        extraObj[item.key.trim()] = item.value;
      }
    }
    await updateProjectApi(props.projectId, {
      defaultProviderId: defaultProviderId.value,
      defaultModelName: defaultModelName.value || undefined,
      extraConfig: JSON.stringify(extraObj),
    });
    message.success('模型与参数已保存');
  } catch {
    message.error('保存失败');
  } finally {
    emit('update:saving', false);
  }
}

async function fetchProviders() {
  try {
    providers.value = await getProviderAccountsApi();
  } catch {
    providers.value = [];
  }
}

onMounted(() => {
  parseExtraConfig(props.initialExtraConfig);
  fetchProviders();
});

import { computed } from 'vue';
</script>

<template>
  <Card :bordered="false" style="max-width: 700px">
    <Typography.Title :level="5">备用模型</Typography.Title>
    <Typography.Paragraph type="secondary">
      项目备用模型用于非 Agent 的辅助思考问题。
    </Typography.Paragraph>

    <Form layout="vertical">
      <Form.Item label="供应商">
        <Select
          v-model:value="defaultProviderId"
          :options="
            enabledProviders.map((p) => ({
              label: `${p.name} (${p.protocolType})`,
              value: p.id,
            }))
          "
          allow-clear
          placeholder="选择供应商账户"
          style="width: 100%"
          @change="onProviderChange"
        />
      </Form.Item>
      <Form.Item label="模型名称" :validate-status="modelsError ? 'warning' : undefined" :help="modelsError ? '加载失败，点击下拉框重试' : undefined">
        <Select
          v-if="providerModels.length > 0 || loadingModels || modelsError"
          v-model:value="defaultModelName"
          :loading="loadingModels"
          :not-found-content="modelsError ? '加载失败，点击重试' : loadingModels ? '加载中…' : '暂无模型'"
          :options="
            providerModels.map((m) => ({
              label: m.id,
              value: m.id,
            }))
          "
          allow-clear
          placeholder="选择模型"
          show-search
          style="width: 100%"
          @focus="ensureModelsLoaded"
        />
        <Input
          v-else
          v-model:value="defaultModelName"
          placeholder="手动输入模型名称（如 gpt-4o）"
        />
      </Form.Item>
    </Form>

    <Divider />

    <Typography.Title :level="5">扩展参数</Typography.Title>
    <Typography.Paragraph type="secondary">
      键值对形式的扩展参数，可用于存储环境变量等。
    </Typography.Paragraph>

    <Table
      :columns="[
        { title: '键', dataIndex: 'key', key: 'key' },
        { title: '值', dataIndex: 'value', key: 'value' },
        { title: '操作', key: 'action', width: 80 },
      ]"
      :data-source="extraConfigList"
      :pagination="false"
      row-key="key"
      size="small"
    >
      <template #bodyCell="{ column, record, index }">
        <template v-if="column.key === 'key'">
          <Input
            v-model:value="record.key"
            placeholder="参数名"
            size="small"
          />
        </template>
        <template v-else-if="column.key === 'value'">
          <Input
            v-model:value="record.value"
            placeholder="参数值"
            size="small"
          />
        </template>
        <template v-else-if="column.key === 'action'">
          <Popconfirm
            title="确认删除？"
            @confirm="removeExtraParam(index)"
          >
            <Button danger size="small" type="text">删除</Button>
          </Popconfirm>
        </template>
      </template>
    </Table>

    <Button
      block
      type="dashed"
      style="margin-top: 8px"
      @click="addExtraParam"
    >
      ＋ 添加参数
    </Button>

    <Divider />
    <Button
      :loading="saving"
      type="primary"
      @click="saveModelAndParams"
    >
      保存模型与参数
    </Button>
  </Card>
</template>
