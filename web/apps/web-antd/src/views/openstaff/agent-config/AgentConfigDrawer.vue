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
  message,
} from 'ant-design-vue';

import { getRoleIcon } from '#/constants/agent';
import { useProviderModels } from '#/composables/useProviderModels';
import {
  getAgentMcpBindingsApi,
  getMcpConfigsApi,
  createAgentMcpBindingApi,
  deleteAgentMcpBindingApi,
} from '#/api/openstaff/mcp';
import type { McpApi } from '#/api/openstaff/mcp';
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
const {
  models: providerModels,
  loading: loadingModels,
  error: modelsError,
  ensureLoaded: ensureModelsLoaded,
} = useProviderModels(selectedProviderId);

const enabledProviders = computed(() =>
  props.providers.filter((p) => p.isEnabled),
);

watch(
  () => props.open,
  (val) => {
    if (val && props.mode === 'edit' && props.editingRole) {
      loadRoleToForm(props.editingRole);
      loadMcpData(props.editingRole.id);
    } else if (val && props.mode === 'create') {
      editForm.value = { ...DEFAULT_FORM, soul: { ...DEFAULT_FORM.soul } };
      mcpBindings.value = [];
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

// ===== MCP 绑定 =====
const mcpBindings = ref<McpApi.AgentMcpBinding[]>([]);
const allMcpConfigs = ref<McpApi.McpServerConfig[]>([]);
const loadingMcpBindings = ref(false);
const selectedMcpConfigId = ref<string | undefined>(undefined);

const availableMcpConfigs = computed(() => {
  const boundIds = new Set(mcpBindings.value.map((b) => b.mcpServerConfigId));
  return allMcpConfigs.value.filter((c) => c.isEnabled && !boundIds.has(c.id));
});

function filterMcpOption(input: string, option: any) {
  const search = input.toLowerCase();
  const val = (option?.children?.[0]?.children || '').toString().toLowerCase();
  return val.includes(search);
}

async function loadMcpData(roleId: string) {
  loadingMcpBindings.value = true;
  try {
    const [bindings, configs] = await Promise.all([
      getAgentMcpBindingsApi(roleId),
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
  if (!selectedMcpConfigId.value || !props.editingRole) return;
  try {
    await createAgentMcpBindingApi({
      agentRoleId: props.editingRole.id,
      mcpServerConfigId: selectedMcpConfigId.value,
    });
    selectedMcpConfigId.value = undefined;
    await loadMcpData(props.editingRole.id);
    message.success('MCP 工具已绑定');
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : String(e);
    message.error('绑定失败: ' + msg);
  }
}

async function removeMcpBinding(configId: string) {
  if (!props.editingRole) return;
  try {
    await deleteAgentMcpBindingApi(props.editingRole.id, configId);
    await loadMcpData(props.editingRole.id);
    message.success('已移除');
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : String(e);
    message.error('移除失败: ' + msg);
  }
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

      <FormItem label="模型" :validate-status="modelsError ? 'warning' : undefined" :help="modelsError ? '加载失败，点击下拉框重试' : undefined">
        <Select
          v-model:value="editForm.modelName"
          :disabled="!editForm.modelProviderId"
          :filter-option="filterModelOption"
          :loading="loadingModels"
          :not-found-content="modelsError ? '加载失败，点击重试' : loadingModels ? '加载中…' : '暂无模型'"
          :placeholder="editForm.modelProviderId ? '请选择模型' : '请先选择供应商'"
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
      <CollapsePanel key="tools" header="🔧 内置工具">
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

      <CollapsePanel key="mcp" header="🧩 MCP 工具">
        <Spin :spinning="loadingMcpBindings">
          <div v-if="mcpBindings.length > 0" style="margin-bottom: 8px">
            <div
              v-for="binding in mcpBindings"
              :key="binding.mcpServerConfigId"
              style="display: flex; align-items: center; justify-content: space-between; padding: 6px 8px; margin-bottom: 4px; background: var(--ant-color-bg-container-disabled); border-radius: 6px"
            >
              <Space>
                <Tag color="cyan">{{ binding.serverName }}</Tag>
                <span style="font-size: 13px">{{ binding.configName }}</span>
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

          <!-- 添加 MCP 绑定 -->
          <Space v-if="mode === 'edit' && editingRole">
            <Select
              v-model:value="selectedMcpConfigId"
              placeholder="选择 MCP 配置"
              style="width: 260px"
              allow-clear
              show-search
              :filter-option="filterMcpOption"
            >
              <SelectOption
                v-for="cfg in availableMcpConfigs"
                :key="cfg.id"
                :value="cfg.id"
              >
                {{ cfg.serverName }} - {{ cfg.name }}
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
          <Typography.Text v-else type="secondary" style="display: block; font-size: 12px">
            保存员工后可绑定 MCP 工具
          </Typography.Text>
        </Spin>
      </CollapsePanel>
    </Collapse>

    <!-- 底部保存 -->
    <Divider style="margin: 16px 0" />
    <Button block type="primary" size="large" @click="handleSave">
      {{ mode === 'create' ? '创建员工' : '保存配置' }}
    </Button>
  </Drawer>
</template>
