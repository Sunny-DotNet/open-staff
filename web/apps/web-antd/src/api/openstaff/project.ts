import { requestClient } from '#/api/request';

export namespace ProjectApi {
  export interface Project {
    id: string;
    name: string;
    description: string;
    language: string;
    status: string;
    mainSessionId?: string;
    sessionId?: string;
    workspacePath?: string;
    defaultProviderId?: string;
    defaultModelName?: string;
    extraConfig?: string;
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
    language?: string;
    defaultProviderId?: string | null;
    defaultModelName?: string | null;
    extraConfig?: string | null;
  }

  export interface ProjectAgent {
    id: string;
    projectId: string;
    agentRoleId: string;
    status: string;
    currentTask?: string;
    agentRole?: {
      id: string;
      name: string;
      roleType: string;
      description?: string;
    };
  }
}

/** 获取项目列表 */
export async function getProjectsApi(): Promise<ProjectApi.Project[]> {
  return requestClient.get<ProjectApi.Project[]>('/projects');
}

/** 获取项目详情 */
export async function getProjectApi(
  id: string,
): Promise<ProjectApi.Project> {
  return requestClient.get<ProjectApi.Project>(`/projects/${id}`);
}

/** 创建项目 */
export async function createProjectApi(
  data: ProjectApi.CreateParams,
): Promise<ProjectApi.Project> {
  return requestClient.post<ProjectApi.Project>('/projects', data);
}

/** 更新项目 */
export async function updateProjectApi(
  id: string,
  data: ProjectApi.UpdateParams,
): Promise<ProjectApi.Project> {
  return requestClient.put<ProjectApi.Project>(`/projects/${id}`, data);
}

/** 删除项目 */
export async function deleteProjectApi(id: string): Promise<void> {
  await requestClient.delete(`/projects/${id}`);
}

/** 初始化项目（创建工作目录 + Git + 群聊 Session） */
export async function initializeProjectApi(
  id: string,
): Promise<ProjectApi.Project> {
  return requestClient.post<ProjectApi.Project>(
    `/projects/${id}/initialize`,
  );
}

/** 获取项目员工列表 */
export async function getProjectAgentsApi(
  id: string,
): Promise<ProjectApi.ProjectAgent[]> {
  return requestClient.get<ProjectApi.ProjectAgent[]>(
    `/projects/${id}/agents`,
  );
}

/** 批量设置项目员工 */
export async function setProjectAgentsApi(
  id: string,
  agentRoleIds: string[],
): Promise<void> {
  await requestClient.put(`/projects/${id}/agents`, { agentRoleIds });
}
