<script setup lang="ts">
import type { AgentDto, TaskDto } from '@openstaff/api';

import {
  deleteApiProjectsByProjectIdTasksByTaskId,
  getApiProjectsByProjectIdTasks,
  postApiProjectsByProjectIdTasks,
  postApiProjectsByProjectIdTasksByTaskIdResume,
  putApiProjectsByProjectIdTasksByTaskId,
  unwrapClientEnvelope,
} from '@openstaff/api';
import { useMutation, useQuery } from '@tanstack/vue-query';
import { message } from 'ant-design-vue';
import { computed, reactive, ref } from 'vue';

import { t } from '@/i18n';

const props = defineProps<{
  projectAgents: AgentDto[];
  projectId: string;
}>();

const STATUS_COLUMNS = [
  { color: 'default', key: 'pending', title: t('project.taskStatusMap.pending') },
  { color: 'processing', key: 'in_progress', title: t('project.taskStatusMap.in_progress') },
  { color: 'success', key: 'done', title: t('project.taskStatusMap.done') },
  { color: 'error', key: 'blocked', title: t('project.taskStatusMap.blocked') },
];

const showModal = ref(false);
const editingTask = ref<null | TaskDto>(null);
const submitting = ref(false);
const form = reactive({
  assignedProjectAgentRoleId: undefined as string | undefined,
  description: '',
  priority: 5,
  title: '',
});

const tasksQuery = useQuery({
  queryKey: ['projects', 'tasks-board', computed(() => props.projectId)],
  enabled: computed(() => !!props.projectId),
  queryFn: async () =>
    unwrapClientEnvelope(
      await getApiProjectsByProjectIdTasks({
        path: { projectId: props.projectId },
      }),
    ),
});

const tasks = computed(() => tasksQuery.data.value ?? []);
const agentOptions = computed(() =>
  props.projectAgents
    .filter((agent) => agent.id)
    .map((agent) => ({
      label:
        agent.agentRole?.name ||
        agent.roleName ||
        agent.id ||
        '--',
      value: agent.id ?? '',
    })),
);

const saveTaskMutation = useMutation({
  mutationFn: async () => {
    if (!props.projectId) {
      throw new Error(t('project.validationProject'));
    }

    if (!form.title.trim()) {
      throw new Error(t('project.validationTaskTitle'));
    }

    if (editingTask.value?.id) {
      return unwrapClientEnvelope(
        await putApiProjectsByProjectIdTasksByTaskId({
          body: {
            assignedProjectAgentRoleId: form.assignedProjectAgentRoleId || null,
            description: form.description.trim() || null,
            priority: form.priority,
            title: form.title.trim(),
          },
          path: {
            projectId: props.projectId,
            taskId: editingTask.value.id,
          },
        }),
      );
    }

    return unwrapClientEnvelope(
      await postApiProjectsByProjectIdTasks({
        body: {
          assignedProjectAgentRoleId: form.assignedProjectAgentRoleId || undefined,
          description: form.description.trim() || undefined,
          priority: form.priority,
          title: form.title.trim(),
        },
        path: { projectId: props.projectId },
      }),
    );
  },
  onSuccess: async () => {
    message.success(
      editingTask.value ? t('project.updateTaskSuccess') : t('project.createTaskSuccess'),
    );
    showModal.value = false;
    await tasksQuery.refetch();
  },
});

const deleteTaskMutation = useMutation({
  mutationFn: async (taskId: string) =>
    deleteApiProjectsByProjectIdTasksByTaskId({
      path: { projectId: props.projectId, taskId },
    }),
  onSuccess: async () => {
    message.success(t('project.deleteTaskSuccess'));
    await tasksQuery.refetch();
  },
});

const resumeTaskMutation = useMutation({
  mutationFn: async (taskId: string) =>
    unwrapClientEnvelope(
      await postApiProjectsByProjectIdTasksByTaskIdResume({
        path: { projectId: props.projectId, taskId },
      }),
    ),
  onSuccess: async () => {
    message.success(t('project.resumeTaskSuccess'));
    await tasksQuery.refetch();
  },
});

const updateTaskStatusMutation = useMutation({
  mutationFn: async ({
    status,
    taskId,
  }: {
    status: string;
    taskId: string;
  }) =>
    unwrapClientEnvelope(
      await putApiProjectsByProjectIdTasksByTaskId({
        body: { status },
        path: { projectId: props.projectId, taskId },
      }),
    ),
  onSuccess: async () => {
    await tasksQuery.refetch();
  },
});

function tasksByStatus(status: string) {
  return tasks.value.filter((task) => task.status === status);
}

function openCreateModal() {
  editingTask.value = null;
  form.assignedProjectAgentRoleId = undefined;
  form.description = '';
  form.priority = 5;
  form.title = '';
  showModal.value = true;
}

function openEditModal(task: TaskDto) {
  editingTask.value = task;
  form.assignedProjectAgentRoleId = task.assignedProjectAgentRoleId ?? undefined;
  form.description = task.description ?? '';
  form.priority = Number(task.priority ?? 5);
  form.title = task.title ?? '';
  showModal.value = true;
}

async function saveTask() {
  submitting.value = true;
  try {
    await saveTaskMutation.mutateAsync();
  } catch (error) {
    message.error(getErrorMessage(error, t('project.actionFailed')));
  } finally {
    submitting.value = false;
  }
}

async function deleteTask(task: TaskDto) {
  if (!task.id) {
    message.error(t('project.validationTask'));
    return;
  }

  try {
    await deleteTaskMutation.mutateAsync(task.id);
  } catch (error) {
    message.error(getErrorMessage(error, t('project.actionFailed')));
  }
}

async function resumeTask(task: TaskDto) {
  if (!task.id) {
    message.error(t('project.validationTask'));
    return;
  }

  try {
    await resumeTaskMutation.mutateAsync(task.id);
  } catch (error) {
    message.error(getErrorMessage(error, t('project.actionFailed')));
  }
}

async function moveTask(task: TaskDto, status: string) {
  if (!task.id) {
    message.error(t('project.validationTask'));
    return;
  }

  try {
    await updateTaskStatusMutation.mutateAsync({
      status,
      taskId: task.id,
    });
  } catch (error) {
    message.error(getErrorMessage(error, t('project.actionFailed')));
  }
}

function resolvePriority(task: TaskDto) {
  const priority = Number(task.priority ?? 0);
  if (priority >= 8) {
    return { color: 'red', text: t('project.priorityHigh') };
  }
  if (priority >= 5) {
    return { color: 'orange', text: t('project.priorityMedium') };
  }
  return { color: 'default', text: t('project.priorityLow') };
}

function resolveTransitions(status: null | string | undefined) {
  switch (status) {
    case 'pending':
      return [{ label: t('project.transitionStart'), value: 'in_progress' }];
    case 'in_progress':
      return [
        { label: t('project.transitionDone'), value: 'done' },
        { label: t('project.transitionBlock'), value: 'blocked' },
        { label: t('project.transitionBacklog'), value: 'pending' },
      ];
    case 'done':
      return [{ label: t('project.transitionReopen'), value: 'in_progress' }];
    case 'blocked':
      return [{ label: t('project.transitionBacklog'), value: 'pending' }];
    default:
      return [];
  }
}

function resolveAgentName(agentId: null | string | undefined) {
  if (!agentId) {
    return t('project.unassigned');
  }

  const agent = props.projectAgents.find((item) => item.id === agentId);
  return (
    agent?.agentRole?.name ||
    agent?.roleName ||
    agentId
  );
}

function getErrorMessage(error: unknown, fallback: string) {
  if (error instanceof Error && error.message) {
    return error.message;
  }

  return fallback;
}
</script>

<template>
  <div class="space-y-4">
    <div class="flex justify-between gap-3">
      <a-button :loading="tasksQuery.isFetching.value" @click="tasksQuery.refetch()">
        {{ t('common.refresh') }}
      </a-button>
      <a-button type="primary" @click="openCreateModal">
        {{ t('project.newTask') }}
      </a-button>
    </div>

    <a-spin :spinning="tasksQuery.isLoading.value">
      <div class="grid gap-4 xl:grid-cols-4">
        <section
          v-for="column in STATUS_COLUMNS"
          :key="column.key"
          class="rounded-2xl border border-border/70 bg-background/75 p-4"
        >
          <div class="mb-3 flex items-center justify-between">
            <a-tag :color="column.color">
              {{ column.title }}
            </a-tag>
            <span class="text-xs text-muted-foreground">
              {{ tasksByStatus(column.key).length }}
            </span>
          </div>

          <div class="space-y-3">
            <article
              v-for="task in tasksByStatus(column.key)"
              :key="task.id"
              class="rounded-2xl border border-border/70 bg-background p-4"
            >
              <button
                type="button"
                class="w-full text-left text-sm font-medium text-foreground"
                @click="openEditModal(task)"
              >
                {{ task.title || '--' }}
              </button>

              <p v-if="task.description" class="mt-2 text-xs leading-6 text-muted-foreground">
                {{ task.description }}
              </p>

              <div class="mt-3 flex flex-wrap gap-2">
                <a-tag :color="resolvePriority(task).color">
                  P{{ task.priority ?? 0 }} · {{ resolvePriority(task).text }}
                </a-tag>
                <a-tag>
                  {{ task.assignedAgentName || task.assignedRoleName || resolveAgentName(task.assignedProjectAgentRoleId) }}
                </a-tag>
              </div>

              <div class="mt-3 flex flex-wrap gap-2">
                <a-button
                  v-for="transition in resolveTransitions(task.status)"
                  :key="transition.value"
                  size="small"
                  type="link"
                  @click="moveTask(task, transition.value)"
                >
                  {{ transition.label }}
                </a-button>
                <a-button
                  v-if="task.status === 'blocked'"
                  size="small"
                  type="link"
                  @click="resumeTask(task)"
                >
                  {{ t('project.resumeTask') }}
                </a-button>
                <a-popconfirm
                  :title="t('project.deleteTaskConfirm')"
                  @confirm="deleteTask(task)"
                >
                  <a-button danger size="small" type="link">
                    {{ t('project.delete') }}
                  </a-button>
                </a-popconfirm>
              </div>
            </article>

            <a-empty
              v-if="tasksByStatus(column.key).length === 0"
              :description="t('project.noTasks')"
            />
          </div>
        </section>
      </div>
    </a-spin>

    <a-modal
      v-model:open="showModal"
      :confirm-loading="submitting"
      :title="editingTask ? t('project.editTask') : t('project.newTask')"
      @ok="saveTask"
    >
      <a-form layout="vertical">
        <a-form-item :label="t('project.taskTitle')" required>
          <a-input
            v-model:value="form.title"
            :placeholder="t('project.taskTitlePlaceholder')"
          />
        </a-form-item>
        <a-form-item :label="t('project.taskDescription')">
          <a-textarea
            v-model:value="form.description"
            :auto-size="{ minRows: 3, maxRows: 6 }"
          />
        </a-form-item>
        <div class="grid gap-4 md:grid-cols-2">
          <a-form-item :label="t('project.priority')">
            <a-input-number
              v-model:value="form.priority"
              :max="10"
              :min="1"
              style="width: 100%"
            />
          </a-form-item>
          <a-form-item :label="t('project.assignee')">
            <a-select
              v-model:value="form.assignedProjectAgentRoleId"
              allow-clear
              :options="agentOptions"
              :placeholder="t('project.assigneePlaceholder')"
            />
          </a-form-item>
        </div>
      </a-form>
    </a-modal>
  </div>
</template>
