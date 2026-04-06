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
export async function getTasksApi(
  projectId: string,
  status?: string,
): Promise<TaskApi.Task[]> {
  const query = status ? `?status=${status}` : '';
  return requestClient.get<TaskApi.Task[]>(
    `/projects/${projectId}/tasks${query}`,
  );
}

/** 创建任务 */
export async function createTaskApi(
  projectId: string,
  data: TaskApi.CreateParams,
): Promise<TaskApi.Task> {
  return requestClient.post<TaskApi.Task>(
    `/projects/${projectId}/tasks`,
    data,
  );
}

/** 更新任务 */
export async function updateTaskApi(
  projectId: string,
  taskId: string,
  data: TaskApi.UpdateParams,
): Promise<TaskApi.Task> {
  return requestClient.put<TaskApi.Task>(
    `/projects/${projectId}/tasks/${taskId}`,
    data,
  );
}

/** 删除任务 */
export async function deleteTaskApi(
  projectId: string,
  taskId: string,
): Promise<void> {
  await requestClient.delete(`/projects/${projectId}/tasks/${taskId}`);
}

/** 批量更新任务状态 */
export async function batchUpdateTaskStatusApi(
  projectId: string,
  tasks: Array<{ status: string; taskId: string }>,
): Promise<void> {
  await requestClient.patch(
    `/projects/${projectId}/tasks/batch-status`,
    { tasks },
  );
}
