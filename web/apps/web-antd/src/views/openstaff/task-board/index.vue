<script lang="ts" setup>
import type { TaskApi } from '#/api/openstaff/task';

import { computed, onMounted, ref } from 'vue';
import { useRoute } from 'vue-router';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Col,
  Empty,
  Form,
  FormItem,
  Input,
  InputNumber,
  message,
  Modal,
  Popconfirm,
  Row,
  Space,
  Spin,
  Tag,
  Tooltip,
} from 'ant-design-vue';

import {
  createTaskApi,
  deleteTaskApi,
  getTasksApi,
  updateTaskApi,
} from '#/api/openstaff/task';

const route = useRoute();
const projectId = computed(() => route.params.id as string);

const tasks = ref<TaskApi.Task[]>([]);
const loading = ref(false);

// 新建/编辑 Modal
const showModal = ref(false);
const editingTask = ref<TaskApi.Task | null>(null);
const form = ref({
  title: '',
  description: '',
  priority: 5,
});

const columns = [
  { key: 'pending', title: '待办', color: 'default', icon: '📋' },
  { key: 'in_progress', title: '进行中', color: 'processing', icon: '⚡' },
  { key: 'done', title: '已完成', color: 'success', icon: '✅' },
  { key: 'blocked', title: '阻塞', color: 'error', icon: '🚫' },
];

const priorityLabel = (p: number) => {
  if (p >= 8) return { text: '紧急', color: 'red' };
  if (p >= 5) return { text: '高', color: 'orange' };
  if (p >= 3) return { text: '中', color: 'blue' };
  return { text: '低', color: 'default' };
};

function getTasksByStatus(status: string) {
  return tasks.value.filter((t) => t.status === status);
}

async function loadTasks() {
  if (!projectId.value) return;
  loading.value = true;
  try {
    const result = await getTasksApi(projectId.value);
    tasks.value = Array.isArray(result) ? result : [];
  } catch (e: unknown) {
    message.error('加载任务失败: ' + (e instanceof Error ? e.message : String(e)));
  } finally {
    loading.value = false;
  }
}

function openCreateModal() {
  editingTask.value = null;
  form.value = { title: '', description: '', priority: 5 };
  showModal.value = true;
}

function openEditModal(task: TaskApi.Task) {
  editingTask.value = task;
  form.value = {
    title: task.title,
    description: task.description || '',
    priority: task.priority,
  };
  showModal.value = true;
}

async function handleSave() {
  if (!form.value.title.trim()) {
    message.warning('请输入任务标题');
    return;
  }
  try {
    if (editingTask.value) {
      await updateTaskApi(projectId.value, editingTask.value.id, {
        title: form.value.title,
        description: form.value.description,
        priority: form.value.priority,
      });
      message.success('任务已更新');
    } else {
      await createTaskApi(projectId.value, {
        title: form.value.title,
        description: form.value.description,
        priority: form.value.priority,
      });
      message.success('任务已创建');
    }
    showModal.value = false;
    await loadTasks();
  } catch (e: unknown) {
    message.error('保存失败: ' + (e instanceof Error ? e.message : String(e)));
  }
}

async function handleStatusChange(taskId: string, newStatus: string) {
  try {
    await updateTaskApi(projectId.value, taskId, { status: newStatus });
    await loadTasks();
  } catch (e: unknown) {
    message.error('更新状态失败: ' + (e instanceof Error ? e.message : String(e)));
  }
}

async function handleDelete(taskId: string) {
  try {
    await deleteTaskApi(projectId.value, taskId);
    message.success('任务已删除');
    await loadTasks();
  } catch (e: unknown) {
    message.error('删除失败: ' + (e instanceof Error ? e.message : String(e)));
  }
}

const statusTransitions: Record<string, Array<{ label: string; value: string }>> = {
  pending: [
    { label: '开始', value: 'in_progress' },
    { label: '阻塞', value: 'blocked' },
  ],
  in_progress: [
    { label: '完成', value: 'done' },
    { label: '阻塞', value: 'blocked' },
    { label: '退回', value: 'pending' },
  ],
  done: [{ label: '重开', value: 'in_progress' }],
  blocked: [
    { label: '解除', value: 'pending' },
    { label: '开始', value: 'in_progress' },
  ],
};

onMounted(loadTasks);
</script>

<template>
  <Page title="任务看板">
    <template #extra>
      <Space>
        <Button @click="loadTasks" :loading="loading">刷新</Button>
        <Button type="primary" @click="openCreateModal">新建任务</Button>
      </Space>
    </template>

    <Spin :spinning="loading">
      <Row :gutter="12">
        <Col v-for="column in columns" :key="column.key" :span="6">
          <div class="kanban-column">
            <div class="kanban-header">
              <Tag :color="column.color">
                {{ column.icon }} {{ column.title }}
              </Tag>
              <span class="task-count">
                {{ getTasksByStatus(column.key).length }}
              </span>
            </div>

            <div class="kanban-body">
              <Card
                v-for="task in getTasksByStatus(column.key)"
                :key="task.id"
                size="small"
                class="task-card"
                hoverable
              >
                <div class="task-title" @click="openEditModal(task)">
                  {{ task.title }}
                </div>
                <div v-if="task.description" class="task-desc">
                  {{ task.description }}
                </div>
                <div class="task-footer">
                  <Tag :color="priorityLabel(task.priority).color" size="small">
                    P{{ task.priority }} {{ priorityLabel(task.priority).text }}
                  </Tag>
                  <Space size="small">
                    <Tooltip
                      v-for="trans in (statusTransitions[task.status] || [])"
                      :key="trans.value"
                      :title="trans.label"
                    >
                      <Button
                        size="small"
                        type="link"
                        @click="handleStatusChange(task.id, trans.value)"
                      >
                        {{ trans.label }}
                      </Button>
                    </Tooltip>
                    <Popconfirm title="确认删除？" @confirm="handleDelete(task.id)">
                      <Button size="small" type="link" danger>删除</Button>
                    </Popconfirm>
                  </Space>
                </div>
              </Card>
              <Empty
                v-if="getTasksByStatus(column.key).length === 0"
                description="暂无任务"
                :image="Empty.PRESENTED_IMAGE_SIMPLE"
              />
            </div>
          </div>
        </Col>
      </Row>
    </Spin>

    <Modal
      v-model:open="showModal"
      :title="editingTask ? '编辑任务' : '新建任务'"
      @ok="handleSave"
      :okText="editingTask ? '保存' : '创建'"
      cancelText="取消"
    >
      <Form layout="vertical">
        <FormItem label="标题" required>
          <Input v-model:value="form.title" placeholder="任务标题" />
        </FormItem>
        <FormItem label="描述">
          <Input.TextArea
            v-model:value="form.description"
            placeholder="任务描述"
            :rows="3"
          />
        </FormItem>
        <FormItem label="优先级 (1-10)">
          <InputNumber v-model:value="form.priority" :min="1" :max="10" />
        </FormItem>
      </Form>
    </Modal>
  </Page>
</template>

<style scoped>
.kanban-column {
  background: hsl(var(--accent));
  border-radius: 8px;
  min-height: 500px;
  padding: 12px;
}

.kanban-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 12px;
  padding-bottom: 8px;
  border-bottom: 1px solid hsl(var(--border));
}

.task-count {
  color: hsl(var(--muted-foreground));
  font-size: 13px;
}

.kanban-body {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.task-card {
  cursor: pointer;
  transition: box-shadow 0.2s;
}

.task-card:hover {
  box-shadow: 0 2px 8px hsl(var(--foreground) / 0.1);
}

.task-title {
  font-weight: 500;
  margin-bottom: 4px;
}

.task-desc {
  color: hsl(var(--muted-foreground));
  font-size: 12px;
  margin-bottom: 8px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.task-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
</style>
