import { requestClient } from '#/api/request';

export namespace TaskApi {
  export interface Task {
    id: string;
    title: string;
    description: string | null;
    status: string;
    priority: number;
    assignedAgentId: string | null;
    parentTaskId: string | null;
    metadata: string | null;
    createdAt: string;
    updatedAt: string;
    subTaskCount?: number;
    dependencyIds?: string[];
  }

  export interface CreateParams {
    title: string;
    description?: string;
    priority?: number;
    parentTaskId?: string;
    assignedAgentId?: string;
    dependsOn?: string[];
  }

  export interface UpdateParams {
    title?: string;
    description?: string;
    status?: string;
    priority?: number;
    assignedAgentId?: string;
  }
}

/** 获取项目任务列表 */
export async function getTasksApi(projectId: string, status?: string) {
  const query = status ? `?status=${status}` : '';
  const resp = await requestClient.get(
    `/projects/${projectId}/tasks${query}`,
  );
  return (resp as any)?.data ?? resp;
}

/** 创建任务 */
export async function createTaskApi(
  projectId: string,
  data: TaskApi.CreateParams,
) {
  const resp = await requestClient.post(
    `/projects/${projectId}/tasks`,
    data,
  );
  return (resp as any)?.data ?? resp;
}

/** 更新任务 */
export async function updateTaskApi(
  projectId: string,
  taskId: string,
  data: TaskApi.UpdateParams,
) {
  const resp = await requestClient.put(
    `/projects/${projectId}/tasks/${taskId}`,
    data,
  );
  return (resp as any)?.data ?? resp;
}

/** 删除任务 */
export async function deleteTaskApi(projectId: string, taskId: string) {
  await requestClient.delete(`/projects/${projectId}/tasks/${taskId}`);
}

/** 批量更新任务状态 */
export async function batchUpdateTaskStatusApi(
  projectId: string,
  tasks: Array<{ taskId: string; status: string }>,
) {
  const resp = await requestClient.patch(
    `/projects/${projectId}/tasks/batch-status`,
    { tasks },
  );
  return (resp as any)?.data ?? resp;
}
