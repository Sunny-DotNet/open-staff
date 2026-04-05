<script lang="ts" setup>
import type { ProjectApi } from '#/api/openstaff/project';

import { onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Col,
  Empty,
  Input,
  message,
  Modal,
  Row,
  Space,
  Spin,
  Tag,
  Typography,
} from 'ant-design-vue';

import {
  createProjectApi,
  deleteProjectApi,
  getProjectsApi,
  initializeProjectApi,
} from '#/api/openstaff/project';

const router = useRouter();
const projects = ref<ProjectApi.Project[]>([]);
const loading = ref(false);

// 新建项目对话框
const showCreateModal = ref(false);
const createForm = ref({ name: '', description: '' });
const creating = ref(false);

const statusMap: Record<string, { color: string; label: string }> = {
  active: { color: 'green', label: '活跃' },
  archived: { color: 'default', label: '已归档' },
  created: { color: 'blue', label: '新建' },
  initializing: { color: 'processing', label: '初始化中' },
  paused: { color: 'warning', label: '已暂停' },
  completed: { color: 'success', label: '已完成' },
};

async function fetchProjects() {
  loading.value = true;
  try {
    const data = await getProjectsApi();
    projects.value = (data as any)?.data ?? data ?? [];
  } catch {
    projects.value = [];
  } finally {
    loading.value = false;
  }
}

async function handleCreate() {
  if (!createForm.value.name.trim()) {
    message.warning('请输入项目名称');
    return;
  }
  creating.value = true;
  try {
    const result = await createProjectApi({
      name: createForm.value.name.trim(),
      description: createForm.value.description.trim() || undefined,
    });
    message.success('项目已创建，跳转到配置页...');
    showCreateModal.value = false;
    createForm.value = { name: '', description: '' };
    const project = (result as any)?.data ?? result;
    router.push(`/projects/${project.id}/settings`);
  } catch {
    message.error('创建失败');
  } finally {
    creating.value = false;
  }
}

function handleDelete(project: ProjectApi.Project) {
  Modal.confirm({
    title: '确认删除',
    content: `确定要删除项目「${project.name}」吗？此操作不可撤销。`,
    okType: 'danger',
    async onOk() {
      try {
        await deleteProjectApi(project.id);
        message.success('已删除');
        await fetchProjects();
      } catch {
        message.error('删除失败');
      }
    },
  });
}

function goToProject(project: ProjectApi.Project) {
  router.push(`/projects/${project.id}/chat`);
}

const initializingIds = ref(new Set<string>());

async function handleInitialize(project: ProjectApi.Project) {
  initializingIds.value.add(project.id);
  try {
    await initializeProjectApi(project.id);
    message.success('项目初始化成功');
    await fetchProjects();
  } catch (e: any) {
    message.error('初始化失败: ' + (e?.message || e));
  } finally {
    initializingIds.value.delete(project.id);
  }
}

function formatDate(isoStr: string): string {
  return new Date(isoStr).toLocaleDateString('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  });
}

onMounted(fetchProjects);
</script>

<template>
  <Page title="项目管理">
    <template #extra>
      <Button type="primary" @click="showCreateModal = true">
        ＋ 新建项目
      </Button>
    </template>

    <Spin :spinning="loading">
      <Row v-if="projects.length > 0" :gutter="[16, 16]">
        <Col v-for="project in projects" :key="project.id" :span="8">
          <Card
            hoverable
            @click="goToProject(project)"
            style="cursor: pointer"
          >
            <template #title>
              <Space>
                <span>📁</span>
                <span>{{ project.name }}</span>
              </Space>
            </template>
            <template #extra>
              <Tag
                :color="statusMap[project.status]?.color ?? 'default'"
                size="small"
              >
                {{ statusMap[project.status]?.label ?? project.status }}
              </Tag>
            </template>

            <Typography.Paragraph
              :ellipsis="{ rows: 2 }"
              type="secondary"
              style="min-height: 44px"
            >
              {{ project.description || '暂无描述' }}
            </Typography.Paragraph>

            <div class="flex items-center justify-between" style="margin-top: 8px">
              <Typography.Text type="secondary" style="font-size: 12px">
                创建于 {{ formatDate(project.createdAt) }}
              </Typography.Text>
              <Space size="small">
                <Button
                  v-if="project.status === 'created'"
                  type="primary"
                  size="small"
                  :loading="initializingIds.has(project.id)"
                  @click.stop="handleInitialize(project)"
                >
                  🚀 初始化
                </Button>
                <Button
                  v-if="project.status === 'active'"
                  size="small"
                  type="link"
                  @click.stop="goToProject(project)"
                >
                  💬 进入群聊
                </Button>
                <Button
                  size="small"
                  type="link"
                  @click.stop="router.push(`/projects/${project.id}/settings`)"
                >
                  ⚙ 配置
                </Button>
                <Button
                  danger
                  size="small"
                  type="text"
                  @click.stop="handleDelete(project)"
                >
                  删除
                </Button>
              </Space>
            </div>
          </Card>
        </Col>
      </Row>

      <Empty v-else description="暂无项目，点击「新建项目」开始">
        <Button type="primary" @click="showCreateModal = true">
          新建项目
        </Button>
      </Empty>
    </Spin>

    <!-- 新建项目对话框 -->
    <Modal
      v-model:open="showCreateModal"
      :confirm-loading="creating"
      title="新建项目"
      @ok="handleCreate"
    >
      <div style="padding: 16px 0">
        <div style="margin-bottom: 16px">
          <label style="display: block; margin-bottom: 4px; font-weight: 500">
            项目名称
          </label>
          <Input
            v-model:value="createForm.name"
            placeholder="输入项目名称"
            @press-enter="handleCreate"
          />
        </div>
        <div>
          <label style="display: block; margin-bottom: 4px; font-weight: 500">
            描述（可选）
          </label>
          <Input.TextArea
            v-model:value="createForm.description"
            :rows="3"
            placeholder="简要描述项目目标"
          />
        </div>
      </div>
    </Modal>
  </Page>
</template>
