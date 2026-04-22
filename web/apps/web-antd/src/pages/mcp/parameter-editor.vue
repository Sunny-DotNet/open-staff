<script setup lang="ts">
import { computed } from 'vue';

import { t } from '@/i18n';

import type { McpParameterSchemaItemView } from './api';
import type { McpParameterValues } from './structured-values';
import { filterSchemaByProfile } from './structured-values';

const props = defineProps<{
  modelValue: McpParameterValues;
  schema?: McpParameterSchemaItemView[];
  selectedProfileId?: null | string;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: McpParameterValues];
}>();

const visibleSchema = computed(() => filterSchemaByProfile(props.schema, props.selectedProfileId));

function getValue(key?: null | string) {
  if (!key) {
    return undefined;
  }

  return props.modelValue[key];
}

function updateValue(key: string, value: boolean | string) {
  emit('update:modelValue', {
    ...props.modelValue,
    [key]: value,
  });
}

function describeParameterDefault(item: McpParameterSchemaItemView) {
  if (item.projectOverrideValueSource === 'project-workspace') {
    return t('mcp.parameterDefaultProjectWorkspace');
  }

  switch (item.defaultValueSource) {
    case 'host-temp-directory':
      return t('mcp.parameterDefaultHostTemp');
    case 'user-input':
      return t('mcp.parameterDefaultUserInput');
    case 'template-default':
      return item.defaultValue === null || item.defaultValue === undefined || item.defaultValue === ''
        ? t('mcp.parameterDefaultNone')
        : `${t('mcp.parameterDefaultTemplate')}: ${String(item.defaultValue)}`;
    default:
      return item.defaultValue === null || item.defaultValue === undefined || item.defaultValue === ''
        ? t('mcp.parameterDefaultNone')
        : String(item.defaultValue);
  }
}
</script>

<template>
  <div
    v-if="visibleSchema.length > 0"
    class="grid gap-3 lg:grid-cols-2"
  >
    <div
      v-for="item in visibleSchema"
      :key="`${selectedProfileId || 'default'}-${item.key}`"
    >
      <a-form-item :label="item.label || item.key || '--'" :required="item.required">
        <a-switch
          v-if="item.type === 'boolean'"
          :checked="Boolean(getValue(item.key))"
          @update:checked="(checked: boolean) => updateValue(item.key || '', checked)"
        />
        <a-input-password
          v-else-if="item.type === 'password'"
          :value="String(getValue(item.key) ?? '')"
          @update:value="(value: string) => updateValue(item.key || '', value)"
        />
        <a-input
          v-else
          :value="String(getValue(item.key) ?? '')"
          @update:value="(value: string) => updateValue(item.key || '', value)"
        />
        <div class="mt-1 text-xs text-muted-foreground">
          {{ describeParameterDefault(item) }}
        </div>
        <div v-if="item.description" class="mt-1 text-xs text-muted-foreground">
          {{ item.description }}
        </div>
      </a-form-item>
    </div>
  </div>
</template>
