<script setup lang="ts">
import type { AgentRoleDto, ProjectDto } from '@openstaff/api';

import { getApiProjectsByIdReadme, unwrapClientEnvelope } from '@openstaff/api';
import { useQuery } from '@tanstack/vue-query';
import { Alert } from 'ant-design-vue';
import { MdPreview } from 'md-editor-v3';
import 'md-editor-v3/lib/preview.css';
import { computed } from 'vue';

import { t } from '@/i18n';
import { useThemeMode } from '@/composables/useThemeMode';

import ProjectConversationTab from './ProjectConversationTab.vue';

type HeaderAgent = {
  avatar?: string;
  jobTitle?: string;
  key: string;
  label: string;
  projectAgentId?: string;
  role?: AgentRoleDto | null;
};

const props = defineProps<{
  brainstormAgent?: HeaderAgent | null;
  project: ProjectDto | null;
  projectId: string;
}>();

const emit = defineEmits<{
  (event: 'refresh'): void;
}>();

const project = computed(() => props.project);
const projectId = computed(() => props.projectId);

const brainstormDocumentQuery = useQuery({
  queryKey: ['projects', 'brainstorm-document', projectId],
  enabled: computed(() => !!projectId.value),
  queryFn: async () =>
    unwrapClientEnvelope(
      await getApiProjectsByIdReadme({
        path: { id: projectId.value },
      }),
    ),
});

const brainstormDocument = computed(
  () => brainstormDocumentQuery.data.value?.content ?? '',
);

const canInputBrainstorm = computed(
  () => project.value?.phase === 'brainstorming' || project.value?.phase === 'ready_to_start',
);

const isReadyToStart = computed(() => project.value?.phase === 'ready_to_start');
const { isDarkMode } = useThemeMode();
const markdownTheme = computed(() => (isDarkMode.value ? 'dark' : 'light'));
const markdownPreviewTheme = computed(() => (isDarkMode.value ? 'dark' : 'github'));
const markdownCodeTheme = computed(() => (isDarkMode.value ? 'atom' : 'github'));

async function handleProjectStateChanged() {
  await brainstormDocumentQuery.refetch();
  emit('refresh');
}
</script>

<template>
  <div class="brainstorm-layout">
    <section class="brainstorm-document-card">
      <div class="flex items-center justify-between border-b border-border/70 px-5 py-4">
        <div>
          <h3 class="text-base font-semibold">{{ t('project.brainstormDocumentTitle') }}</h3>
          <p class="mt-1 text-sm text-muted-foreground">
            {{ t('project.brainstormDocumentDescription') }}
          </p>
        </div>
        <a-tag>{{ '.staff/project-brainstorm.md' }}</a-tag>
      </div>

      <div class="brainstorm-document-body">
        <a-spin :spinning="brainstormDocumentQuery.isLoading.value || brainstormDocumentQuery.isFetching.value">
          <div class="brainstorm-document-scroll">
          <a-empty
            v-if="!brainstormDocument"
            :description="t('project.noBrainstormDocument')"
          />
          <MdPreview
            v-else
            :editor-id="`project-brainstorm-${projectId}`"
            class="brainstorm-preview"
            :code-theme="markdownCodeTheme"
            :model-value="brainstormDocument"
            :preview-theme="markdownPreviewTheme"
            :theme="markdownTheme"
          />
        </div>
        </a-spin>
      </div>
    </section>

    <div class="brainstorm-chat-column">
      <Alert
        v-if="isReadyToStart"
        show-icon
        type="success"
        :message="t('project.brainstormReadyMessage')"
        :description="t('project.brainstormReadyHint')"
      />

      <ProjectConversationTab
        class="min-h-0 flex-1"
        :brainstorm-agent="brainstormAgent"
        :can-access="true"
        :can-input="canInputBrainstorm"
        :empty-description="t('project.brainstormEmpty')"
        :input-disabled-hint="t('project.brainstormReadOnlyHint')"
        :input-placeholder="t('project.brainstormPlaceholder')"
        :project="project"
        :project-id="projectId"
        :read-only-description="t('project.brainstormReadOnlyHint')"
        :read-only-message="t('project.brainstormReadOnly')"
        scene="ProjectBrainstorm"
        :start-failed-message="t('project.brainstormStartFailed')"
        @project-state-changed="handleProjectStateChanged"
      />
    </div>
  </div>
</template>

<style scoped>
.brainstorm-layout {
  height: 100%;
  min-height: 0;
  display: grid;
  gap: 1.25rem;
  grid-template-columns: minmax(0, 0.95fr) minmax(0, 1.05fr);
}

.brainstorm-document-card {
  min-height: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  border-radius: 1rem;
  border: 1px solid hsl(var(--border) / 0.7);
  background: hsl(var(--background));
}

.brainstorm-document-body {
  min-height: 0;
  flex: 1;
  overflow: hidden;
}

.brainstorm-document-body :deep(.ant-spin-nested-loading),
.brainstorm-document-body :deep(.ant-spin-container) {
  height: 100%;
}

.brainstorm-document-scroll {
  height: 100%;
  overflow-y: auto;
  padding: 1rem 1.25rem;
}

.brainstorm-chat-column {
  min-height: 0;
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.brainstorm-preview {
  background: transparent;
}

.brainstorm-preview :deep(.md-editor-preview-wrapper) {
  padding: 0;
}

@media (max-width: 1279px) {
  .brainstorm-layout {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
