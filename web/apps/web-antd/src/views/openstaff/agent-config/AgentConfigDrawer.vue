<script lang="ts" setup>
import type { AgentApi } from '#/api/openstaff/agent';
import type { SettingsApi } from '#/api/openstaff/settings';

import { computed, ref, watch } from 'vue';

import {
  Button,
  Collapse,
  CollapsePanel,
  Divider,
  Drawer,
  Form,
  FormItem,
  Input,
  InputNumber,
  Row,
  Col,
  Select,
  SelectOption,
  Slider,
  Space,
  Spin,
  Tag,
  Typography,
} from 'ant-design-vue';

import { getRoleIcon } from '#/constants/agent';
import { useProviderModels } from '#/composables/useProviderModels';
import SoulConfigSection from './SoulConfigSection.vue';

interface SoulConfig {
  attitudes?: string[];
  custom?: string;
  style?: string;
  traits?: string[];
}

interface EditFormState {
  description: string;
  maxTokens: number;
  modelProviderId: string;
  modelName: string;
  name: string;
  soul: SoulConfig;
  temperature: number;
  tools: string[];
}

const DEFAULT_FORM: EditFormState = {
  name: '',
  description: '',
  modelProviderId: '',
  modelName: '',
  temperature: 0.7,
  maxTokens: 4096,
  tools: [],
  soul: { traits: [], style: '', attitudes: [], custom: '' },
};

const props = defineProps<{
  open: boolean;
  mode: 'create' | 'edit';
  editingRole: AgentApi.AgentRole | undefined;
  providers: SettingsApi.ProviderAccount[];
}>();

const emit = defineEmits<{
  (e: 'update:open', value: boolean): void;
  (e: 'save', form: EditFormState): void;
}>();

const editForm = ref<EditFormState>({ ...DEFAULT_FORM, soul: { ...DEFAULT_FORM.soul } });

const selectedProviderId = computed(() => editForm.value.modelProviderId);
const { models: providerModels, loading: loadingModels } =
  useProviderModels(selectedProviderId);

const enabledProviders = computed(() =>
  props.providers.filter((p) => p.isEnabled),
);

watch(
  () => props.open,
  (val) => {
    if (val && props.mode === 'edit' && props.editingRole) {
      loadRoleToForm(props.editingRole);
    } else if (val && props.mode === 'create') {
      editForm.value = { ...DEFAULT_FORM, soul: { ...DEFAULT_FORM.soul } };
    }
  },
);

function parseConfig(configStr: string | null): AgentApi.AgentRoleConfig & { soul?: SoulConfig } {
  try {
    return configStr ? JSON.parse(configStr) : {};
  } catch {
    return {};
  }
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
}

function filterModelOption(input: string, option: any) {
  const search = input.toLowerCase();
  const val = (option?.value || '').toString().toLowerCase();
  return val.includes(search);
}

function handleSave() {
  emit('save', editForm.value);
}
</script>

<template>
  <Drawer
    :open="open"
    :title="undefined"
    :width="560"
    placement="right"
    :destroy-on-close="true"
    @update:open="emit('update:open', $event)"
    @close="emit('update:open', false)"
  >
    <template #title>
      <Space>
        <span style="font-size: 24px">
          {{ mode === 'create' ? '🆕' : getRoleIcon(editingRole?.roleType ?? '') }}
        </span>
        <span style="font-size: 16px; font-weight: 600">
          {{ mode === 'create' ? '新增员工' : (editingRole?.name ?? '') }}
        </span>
        <Tag v-if="mode === 'edit' && editingRole?.isBuiltin" color="blue">内置</Tag>
      </Space>
    </template>

    <!-- 基本信息区 -->
    <Divider orientation="left" style="margin: 0 0 16px 0">基本信息</Divider>
    <Form :label-col="{ span: 6 }" size="small">
      <FormItem label="名称" required>
        <Input
          v-model:value="editForm.name"
          :readonly="mode === 'edit' && (editingRole?.isBuiltin ?? false)"
          placeholder="请输入员工名称"
        />
      </FormItem>
      <FormItem label="描述 / 职位说明">
        <Input.TextArea
          v-model:value="editForm.description"
          :readonly="mode === 'edit' && (editingRole?.isBuiltin ?? false)"
          :rows="3"
          placeholder="请输入描述"
        />
      </FormItem>
    </Form>

    <!-- 灵魂配置区 -->
    <SoulConfigSection v-model:soul="editForm.soul" />

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
          </SelectOption>
        </Select>
      </FormItem>

      <FormItem label="模型">
        <Select
          v-model:value="editForm.modelName"
          :disabled="!editForm.modelProviderId"
          :filter-option="filterModelOption"
          :loading="loadingModels"
          :placeholder="'请先选择供应商'"
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
          </SelectOption>
        </Select>
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
    <Button block type="primary" size="large" @click="handleSave">
      {{ mode === 'create' ? '创建员工' : '保存配置' }}
    </Button>
  </Drawer>
</template>
