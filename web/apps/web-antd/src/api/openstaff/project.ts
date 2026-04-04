import { requestClient } from '#/api/request';

export namespace ProjectApi {
  export interface Project {
    id: string;
    name: string;
    description: string;
    status: string;
    mainSessionId?: string;
    workspacePath?: string;
    createdAt: string;
    updatedAt: string;
  }

  export interface CreateParams {
    name: string;
    description?: string;
  }

  export interface UpdateParams {
    name?: string;
    description?: string;
  }
}

/** 获取项目列表 */
export async function getProjectsApi() {
  return requestClient.get<ProjectApi.Project[]>('/projects');
}

/** 获取项目详情 */
export async function getProjectApi(id: string) {
  return requestClient.get<ProjectApi.Project>(`/projects/${id}`);
}

/** 创建项目 */
export async function createProjectApi(data: ProjectApi.CreateParams) {
  return requestClient.post<ProjectApi.Project>('/projects', data);
}

/** 更新项目 */
export async function updateProjectApi(
  id: string,
  data: ProjectApi.UpdateParams,
) {
  return requestClient.put<ProjectApi.Project>(`/projects/${id}`, data);
}

/** 删除项目 */
export async function deleteProjectApi(id: string) {
  return requestClient.delete(`/projects/${id}`);
}

/** 初始化项目（创建工作目录 + Git + 群聊 Session） */
export async function initializeProjectApi(id: string) {
  return requestClient.post<ProjectApi.Project>(
    `/projects/${id}/initialize`,
  );
}
