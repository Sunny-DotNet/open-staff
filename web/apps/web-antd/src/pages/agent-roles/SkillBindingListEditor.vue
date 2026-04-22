<script lang="ts" setup>
import { computed, ref } from 'vue';

import type { AgentRoleSkillBindingDto, InstalledSkillDto } from '../skills/api';

interface EditableSkillBinding extends AgentRoleSkillBindingDto {
  localId: string;
}

const props = withDefaults(
  defineProps<{
    adding?: boolean;
    availableSkills: InstalledSkillDto[];
    bindings: EditableSkillBinding[];
    emptyText?: string;
    loading?: boolean;
    pendingSkillInstallKey?: string;
  }>(),
  {
    adding: false,
    emptyText: '当前还没有 Skill 绑定',
    loading: false,
    pendingSkillInstallKey: undefined,
  },
);

const emit = defineEmits<{
  (event: 'add'): void;
  (event: 'remove', skillInstallKey: string): void;
  (event: 'update:pendingSkillInstallKey', value?: string): void;
}>();

const enabledCount = computed(() =>
  props.bindings.filter((binding) => binding.isEnabled).length,
);

const selectedPendingSkillInstallKey = computed({
  get: () => props.pendingSkillInstallKey,
  set: (value) => emit('update:pendingSkillInstallKey', value),
});

const activeKeys = ref<string[]>([]);
const collapseActiveKey = computed(() => activeKeys.value[0]);

function normalizeActiveKeys(value?: Array<number | string> | number | string) {
  if (Array.isArray(value)) {
    return value.map((item) => String(item));
  }

  return value === undefined ? [] : [String(value)];
}

function handleActiveKeysChange(value?: Array<number | string> | number | string) {
  activeKeys.value = normalizeActiveKeys(value).slice(0, 1);
}

function resolveRepoLabel(binding: EditableSkillBinding) {
  const owner = binding.owner?.trim();
  const repo = binding.repo?.trim();
  if (owner && repo) {
    return `${owner}/${repo}`;
  }

  return binding.displayName || binding.skillId || '仓库';
}
</script>

<template>
  <div class="skill-binding-editor">
    <div class="skill-binding-toolbar">
      <div class="skill-binding-summary">
        <a-tag color="blue">
          {{ enabledCount }}/{{ bindings.length }} 已启用
        </a-tag>
      </div>

      <a-space wrap>
        <a-select
          v-model:value="selectedPendingSkillInstallKey"
          allow-clear
          placeholder="选择已安装 Skill"
          style="width: 260px"
        >
          <a-select-option
            v-for="skill in availableSkills"
            :key="skill.installKey"
            :value="skill.installKey"
          >
            {{ skill.displayName }} · {{ skill.source }}
          </a-select-option>
        </a-select>
        <a-button
          :disabled="!pendingSkillInstallKey"
          :loading="adding"
          @click="emit('add')"
        >
          绑定 Skill
        </a-button>
      </a-space>
    </div>

    <a-typography-text v-if="loading" type="secondary">
      加载 Skill 绑定中...
    </a-typography-text>
    <a-empty
      v-else-if="bindings.length === 0"
      :description="emptyText"
      style="padding: 40px 0"
    />

    <a-collapse
      v-else
      accordion
      :active-key="collapseActiveKey"
      :bordered="false"
      destroy-inactive-panel
      class="skill-binding-list"
      @change="handleActiveKeysChange"
    >
      <a-collapse-panel
        v-for="binding in bindings"
        :key="binding.localId"
        class="skill-binding-panel"
      >
        <template #header>
          <div class="skill-binding-header">
            <span class="skill-binding-title">{{ binding.displayName }}</span>
            <a
              v-if="binding.githubUrl"
              :href="binding.githubUrl"
              rel="noreferrer"
              target="_blank"
              class="skill-binding-repo-link"
              @click.stop
            >
              <a-tag color="geekblue">{{ resolveRepoLabel(binding) }}</a-tag>
            </a>
            <a-tag v-else color="default">{{ resolveRepoLabel(binding) }}</a-tag>
            <a-tag v-if="binding.resolutionStatus === 'missing'" color="red">
              缺失
            </a-tag>
          </div>
        </template>

        <template #extra>
          <div class="skill-binding-actions" @click.stop>
            <a-switch v-model:checked="binding.isEnabled" size="small" />
            <a-button
              danger
              size="small"
              type="text"
              class="skill-binding-remove"
              @click="emit('remove', binding.skillInstallKey ?? '')"
            >
              <span aria-hidden="true">🗑️</span>
            </a-button>
          </div>
        </template>

        <div class="skill-binding-body">
          <div class="skill-binding-meta">
            <a-typography-text type="secondary">
              ID：{{ binding.skillId }}
            </a-typography-text>
            <a-typography-text v-if="binding.source" type="secondary">
              来源：{{ binding.source }}
            </a-typography-text>
            <a-typography-text v-if="binding.installRootPath" type="secondary">
              路径：{{ binding.installRootPath }}
            </a-typography-text>
          </div>

          <a-typography-paragraph
            v-if="binding.resolutionMessage"
            class="skill-binding-message"
            :type="binding.resolutionStatus === 'missing' ? 'danger' : 'secondary'"
          >
            {{ binding.resolutionMessage }}
          </a-typography-paragraph>
        </div>
      </a-collapse-panel>
    </a-collapse>

    <a-typography-paragraph class="skill-binding-note" type="secondary">
      说明：缺失的 Skill 绑定会保留，运行时会自动跳过，并在重新安装后恢复生效。
    </a-typography-paragraph>
  </div>
</template>

<style scoped>
.skill-binding-editor,
.skill-binding-body {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.skill-binding-toolbar,
.skill-binding-header {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.skill-binding-header {
  min-width: 0;
  justify-content: flex-start;
}

.skill-binding-title {
  max-width: min(100%, 300px);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 14px;
  font-weight: 600;
}

.skill-binding-repo-link {
  display: inline-flex;
}

.skill-binding-actions {
  display: flex;
  align-items: center;
  gap: 4px;
}

.skill-binding-remove {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  min-width: 28px;
  padding-inline: 0;
}

.skill-binding-meta {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.skill-binding-message {
  margin: 0;
}

:deep(.skill-binding-list .ant-collapse-header) {
  align-items: center;
}

:deep(.skill-binding-list .ant-collapse-extra) {
  display: flex;
  align-items: center;
}

:deep(.skill-binding-list .ant-collapse-content-box) {
  padding-top: 4px;
}
</style>
