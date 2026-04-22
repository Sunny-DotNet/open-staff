<script lang="ts" setup>
import { Button, Modal, Tag } from 'ant-design-vue';

import type { PermissionPrompt } from '@/composables/usePermissionRequests';

const props = defineProps<{
  request: null | PermissionPrompt;
  respondingRequestId?: null | string;
}>();

const emit = defineEmits<{
  (event: 'respond', kind: 'accept' | 'reject'): void;
}>();

function formatContext(prompt: PermissionPrompt) {
  return [
    prompt.projectName ? `项目：${prompt.projectName}` : null,
    prompt.roleName ? `角色：${prompt.roleName}` : null,
    prompt.scene ? `场景：${prompt.scene}` : null,
    prompt.toolName ? `工具：${prompt.toolName}` : null,
    prompt.fileName ? `文件：${prompt.fileName}` : null,
    prompt.url ? `URL：${prompt.url}` : null,
  ]
    .filter(Boolean)
    .join(' · ');
}

function formatDetails(detailsJson?: null | string) {
  if (!detailsJson) {
    return '';
  }

  try {
    return JSON.stringify(JSON.parse(detailsJson), null, 2);
  } catch {
    return detailsJson;
  }
}
</script>

<template>
  <Modal
    :open="!!request"
    :closable="false"
    :mask-closable="false"
    title="权限授权"
  >
    <template #footer>
      <Button
        :disabled="!request"
        :loading="respondingRequestId === request?.requestId"
        @click="emit('respond', 'reject')"
      >
        拒绝
      </Button>
      <Button
        type="primary"
        :disabled="!request"
        :loading="respondingRequestId === request?.requestId"
        @click="emit('respond', 'accept')"
      >
        批准
      </Button>
    </template>

    <div v-if="request" class="permission-request">
      <div class="permission-request__summary">
        {{ request.message }}
      </div>

      <div class="permission-request__meta">
        <Tag color="blue">
          {{ request.kind }}
        </Tag>
        <Tag v-if="request.timeoutMs">
          {{ Math.ceil(request.timeoutMs / 1000) }}s 内需要处理
        </Tag>
      </div>

      <div v-if="formatContext(request)" class="permission-request__context">
        {{ formatContext(request) }}
      </div>

      <div v-if="request.warning" class="permission-request__warning">
        {{ request.warning }}
      </div>

      <pre
        v-if="request.commandText"
        class="permission-request__snippet"
      >{{ request.commandText }}</pre>
      <pre
        v-else-if="request.detailsJson"
        class="permission-request__details"
      >{{ formatDetails(request.detailsJson) }}</pre>
    </div>
  </Modal>
</template>

<style scoped>
.permission-request {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.permission-request__summary {
  font-size: 14px;
  line-height: 1.6;
}

.permission-request__meta {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.permission-request__context {
  font-size: 12px;
  color: var(--ant-color-text-secondary);
}

.permission-request__warning {
  padding: 10px 12px;
  border-radius: 8px;
  background: hsl(var(--warning) / 0.14);
  color: hsl(var(--warning-700));
  white-space: pre-wrap;
}

.permission-request__snippet,
.permission-request__details {
  margin: 0;
  padding: 12px;
  border-radius: 8px;
  background: var(--ant-color-fill-quaternary);
  font-size: 12px;
  line-height: 1.5;
  white-space: pre-wrap;
  word-break: break-word;
  overflow: auto;
}
</style>
