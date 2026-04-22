<script setup lang="ts">
import type { AgentRoleDto, ProjectDto, ProviderAccountDto } from '@openstaff/api';

type HeaderAgent = {
  avatar?: string;
  jobTitle?: string;
  key: string;
  label: string;
  projectAgentId?: string;
  role?: AgentRoleDto | null;
};

import { computed } from 'vue';

import { t } from '@/i18n';

import ProjectConversationTab from './ProjectConversationTab.vue';

const props = defineProps<{
  project: ProjectDto | null;
  projectId: string;
  headerAgents?: HeaderAgent[];
  providers?: ProviderAccountDto[];
}>();

const canUseProjectGroup = computed(
  () => props.project?.phase === 'running' || props.project?.phase === 'completed',
);
</script>

<template>
  <ProjectConversationTab
    :can-access="canUseProjectGroup"
    :can-input="canUseProjectGroup"
    :empty-description="t('project.chatEmpty')"
    :input-disabled-hint="t('project.chatUnavailableHint')"
    :input-placeholder="t('project.chatPlaceholder')"
    :project="project"
    :header-agents="headerAgents"
    :project-id="projectId"
    :providers="providers"
    scene="ProjectGroup"
    :start-failed-message="t('project.chatStartFailed')"
    :unavailable-description="t('project.chatUnavailableHint')"
    :unavailable-message="t('project.chatUnavailable')"
  />
</template>
