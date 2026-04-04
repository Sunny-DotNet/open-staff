import { requestClient } from '#/api/request';

export namespace TaskApi {
  export interface Task {
    id: string;
    title: string;
    description: string;
    status: 'pending' | 'in_progress' | 'done' | 'blocked';
    priority: 'low' | 'medium' | 'high' | 'critical';
    assignedAgentId?: string;
    assignedAgentName?: string;
    createdAt: string;
    updatedAt: string;
  }

  export interface UpdateParams {
    title?: string;
    description?: string;
    status?: Task['status'];
    priority?: Task['priority'];
    assignedAgentId?: string;
  }
}

/** 获取项目任务列表 */
export async function getTasksApi(projectId: string) {
  return requestClient.get<TaskApi.Task[]>(`/projects/${projectId}/tasks`);
}

/** 更新任务 */
export async function updateTaskApi(
  projectId: string,
  taskId: string,
  data: TaskApi.UpdateParams,
) {
  return requestClient.put<TaskApi.Task>(
    `/projects/${projectId}/tasks/${taskId}`,
    data,
  );
}
