<script lang="ts" setup>
import { computed } from 'vue';

import type { McpServerView } from '../mcp/api';
import type { McpParameterValues } from '../mcp/structured-values';
import ParameterEditor from '../mcp/parameter-editor.vue';
import {
  buildDefaultParameterValues,
  mergeParameterValues,
  resolveSelectedProfileId,
} from '../mcp/structured-values';

interface EditableMcpBindingItem {
  localId: string;
  mcpServerId: string;
  mcpServerName: string;
  icon?: null | string;
  mode?: null | string;
  transportType?: null | string;
  toolFilter?: null | string;
  selectedProfileId?: null | string;
  parameterValues: McpParameterValues;
  isEnabled: boolean;
}

interface McpBindingIssue {
  toolFilter?: string;
}

const props = withDefaults(
  defineProps<{
    activeKeys?: string[];
    adding?: boolean;
    availableServers: McpServerView[];
    serverCatalog: McpServerView[];
    bindings: EditableMcpBindingItem[];
    issues?: Record<string, McpBindingIssue>;
    loading?: boolean;
    modeLabels: Record<string, { color: string; label: string }>;
    pendingServerId?: string;
  }>(),
  {
    activeKeys: () => [],
    adding: false,
    issues: () => ({}),
    loading: false,
    pendingServerId: undefined,
  },
);

const emit = defineEmits<{
  (event: 'add'): void;
  (event: 'remove', mcpServerId: string): void;
  (event: 'update:activeKeys', value: string[]): void;
  (event: 'update:pendingServerId', value?: string): void;
}>();

const enabledCount = computed(() =>
  props.bindings.filter((binding) => binding.isEnabled).length,
);

const selectedPendingServerId = computed({
  get: () => props.pendingServerId,
  set: (value) => emit('update:pendingServerId', value),
});

const collapseActiveKey = computed(() => props.activeKeys[0]);

function normalizeActiveKeys(value?: Array<number | string> | number | string) {
  if (Array.isArray(value)) {
    return value.map((item) => String(item));
  }

  return value === undefined ? [] : [String(value)];
}

function handleActiveKeysChange(value?: Array<number | string> | number | string) {
  emit('update:activeKeys', normalizeActiveKeys(value).slice(0, 1));
}

function getBindingIssue(localId: string) {
  return props.issues[localId];
}

function hasBindingIssue(localId: string) {
  const issue = getBindingIssue(localId);
  return Boolean(issue?.toolFilter);
}

function summarizeBinding(binding: EditableMcpBindingItem) {
  const parts = [
    binding.toolFilter?.trim() ? '已限制工具' : '全部工具',
    Object.keys(binding.parameterValues).length > 0 ? '已配置参数' : '使用默认参数',
  ];

  return parts.join(' · ');
}

function resolveTypeLabel(binding: EditableMcpBindingItem) {
  const modeLabel = props.modeLabels[binding.mode ?? 'unknown']?.label ?? binding.mode ?? '未知';
  return binding.transportType ? `${modeLabel} · ${binding.transportType}` : modeLabel;
}

function resolveTypeColor(binding: EditableMcpBindingItem) {
  return props.modeLabels[binding.mode ?? 'unknown']?.color ?? 'default';
}

function resolveBindingServer(binding: EditableMcpBindingItem) {
  return props.serverCatalog.find((server) => server.id === binding.mcpServerId);
}

function getProfileOptions(binding: EditableMcpBindingItem) {
  return resolveBindingServer(binding)?.profiles ?? [];
}

function updateBindingProfile(binding: EditableMcpBindingItem, value?: string) {
  const server = resolveBindingServer(binding);
  const selectedProfileId = resolveSelectedProfileId(server, value);
  binding.selectedProfileId = selectedProfileId || null;
  binding.parameterValues = mergeParameterValues(
    buildDefaultParameterValues(server?.parameterSchema, selectedProfileId),
    binding.parameterValues,
  );
}
</script>

<template>
  <div class="mcp-editor">
    <div class="mcp-toolbar">
      <div class="mcp-toolbar-summary">
        <a-tag color="blue">
          {{ enabledCount }}/{{ bindings.length }} 已启用
        </a-tag>
      </div>

      <a-space class="mcp-toolbar-actions" wrap>
        <a-select
          v-model:value="selectedPendingServerId"
          allow-clear
          placeholder="选择已安装服务器"
          style="width: 220px"
        >
          <a-select-option
            v-for="server in availableServers"
            :key="server.id"
            :value="server.id"
          >
            {{ server.name }} · {{ server.mode }} · {{ server.transportType }}
          </a-select-option>
        </a-select>
        <a-button
          :disabled="!pendingServerId"
          :loading="adding"
          size="small"
          @click="emit('add')"
        >
          绑定 MCP
        </a-button>
      </a-space>
    </div>

    <a-typography-text v-if="loading" type="secondary">
      加载测试 MCP 配置中...
    </a-typography-text>
    <a-typography-text v-else-if="bindings.length === 0" type="secondary">
      当前测试角色还没有 MCP 绑定
    </a-typography-text>

    <a-collapse
      v-else
      accordion
      :active-key="collapseActiveKey"
      :bordered="false"
      destroy-inactive-panel
      class="mcp-accordion"
      @change="handleActiveKeysChange"
    >
      <a-collapse-panel
        v-for="binding in bindings"
        :key="binding.localId"
        class="mcp-accordion-panel"
      >
        <template #header>
          <div class="mcp-item-header">
            <span class="mcp-item-title">{{ binding.mcpServerName }}</span>
              <a-tag :color="resolveTypeColor(binding)">
                {{ resolveTypeLabel(binding) }}
              </a-tag>
              <a-tag v-if="hasBindingIssue(binding.localId)" color="red">
               参数错误
              </a-tag>
            </div>
        </template>

        <template #extra>
          <div class="mcp-item-actions" @click.stop>
            <a-switch v-model:checked="binding.isEnabled" size="small" />
            <a-button
              danger
              size="small"
              type="text"
              class="mcp-remove-button"
              @click="emit('remove', binding.mcpServerId)"
            >
              <span aria-hidden="true">🗑️</span>
            </a-button>
          </div>
        </template>

        <div class="mcp-item-body">
          <a-typography-text class="mcp-item-summary" type="secondary">
            {{ summarizeBinding(binding) }}
          </a-typography-text>

          <a-form layout="vertical">
            <div class="grid gap-4 lg:grid-cols-2">
              <a-form-item label="执行档案">
                <a-select
                  :value="binding.selectedProfileId"
                  @update:value="(value?: string) => updateBindingProfile(binding, value)"
                >
                  <a-select-option
                    v-for="profile in getProfileOptions(binding)"
                    :key="profile.id"
                    :value="profile.id"
                  >
                    {{ profile.displayName || profile.id || '--' }}
                  </a-select-option>
                </a-select>
              </a-form-item>
              <a-form-item label="工具白名单">
                <a-textarea
                  v-model:value="binding.toolFilter"
                  :auto-size="{ minRows: 3, maxRows: 6 }"
                  placeholder='输入字符串数组 JSON，例如 ["search","read"]'
                />
                <div v-if="getBindingIssue(binding.localId)?.toolFilter" class="json-error">
                  {{ getBindingIssue(binding.localId)?.toolFilter }}
                </div>
              </a-form-item>
            </div>
            <ParameterEditor
              v-model="binding.parameterValues"
              :schema="resolveBindingServer(binding)?.parameterSchema"
              :selected-profile-id="binding.selectedProfileId"
            />
          </a-form>
        </div>
      </a-collapse-panel>
    </a-collapse>
  </div>
</template>

<style scoped>
.mcp-editor,
.mcp-item-body {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.mcp-toolbar,
.mcp-item-header {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
}

.mcp-item-header {
  min-width: 0;
  justify-content: flex-start;
}

.mcp-item-title {
  max-width: min(100%, 340px);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-weight: 600;
}

.mcp-item-actions {
  display: flex;
  align-items: center;
  gap: 4px;
}

.mcp-remove-button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  min-width: 28px;
  padding-inline: 0;
}

.mcp-item-summary {
  display: block;
}

.json-error {
  margin-top: 6px;
  font-size: 12px;
  color: var(--ant-color-error);
}

:deep(.mcp-accordion .ant-collapse-header) {
  align-items: center;
}

:deep(.mcp-accordion .ant-collapse-extra) {
  display: flex;
  align-items: center;
}

:deep(.mcp-accordion .ant-collapse-content-box) {
  padding-top: 4px;
}
</style>
