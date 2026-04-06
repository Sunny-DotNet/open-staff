<script lang="ts" setup>
import type { ProjectApi } from '#/api/openstaff/project';

import { onMounted, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';

import { Button, message, Space, Spin, Tabs } from 'ant-design-vue';

import { getProjectApi } from '#/api/openstaff/project';

import AgentConfigTab from './AgentConfigTab.vue';
import BasicInfoTab from './BasicInfoTab.vue';
import ModelParamsTab from './ModelParamsTab.vue';

const route = useRoute();
const router = useRouter();
const projectId = route.params.id as string;

const loading = ref(true);
const saving = ref(false);
const project = ref<ProjectApi.Project | null>(null);

async function fetchProject() {
  loading.value = true;
  try {
    project.value = await getProjectApi(projectId);
  } catch {
    message.error('加载项目失败');
  } finally {
    loading.value = false;
  }
}

onMounted(fetchProject);
</script>

<template>
  <Page
    :title="`项目配置 — ${project?.name || '...'}`"
    content-class="p-4"
  >
    <template #extra>
      <Space>
        <Button @click="router.push('/projects/list')">返回列表</Button>
        <Button
          v-if="project?.status === 'active'"
          type="primary"
          @click="router.push(`/projects/${projectId}/chat`)"
        >
          💬 进入群聊
        </Button>
      </Space>
    </template>

    <Spin :spinning="loading">
      <Tabs v-if="project" type="card">
        <Tabs.TabPane key="basic" tab="📋 基本信息">
          <BasicInfoTab
            :project-id="projectId"
            :saving="saving"
            :initial-data="{
              name: project.name,
              description: project.description || '',
              language: project.language || 'zh-CN',
            }"
            @update:saving="saving = $event"
          />
        </Tabs.TabPane>

        <Tabs.TabPane key="agents" tab="👥 员工配置">
          <AgentConfigTab
            :project-id="projectId"
            :saving="saving"
            @update:saving="saving = $event"
          />
        </Tabs.TabPane>

        <Tabs.TabPane key="model" tab="🤖 模型与参数">
          <ModelParamsTab
            :project-id="projectId"
            :saving="saving"
            :initial-provider-id="project.defaultProviderId ?? null"
            :initial-model-name="project.defaultModelName ?? ''"
            :initial-extra-config="project.extraConfig ?? null"
            @update:saving="saving = $event"
          />
        </Tabs.TabPane>
      </Tabs>
    </Spin>
  </Page>
</template>
