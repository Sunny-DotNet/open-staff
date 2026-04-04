import type { ProjectApi } from '#/api/openstaff/project';

import { ref } from 'vue';

import { defineStore } from 'pinia';

import { getProjectApi, getProjectsApi } from '#/api/openstaff/project';

export const useProjectStore = defineStore('openstaff-project', () => {
  const projects = ref<ProjectApi.Project[]>([]);
  const currentProject = ref<ProjectApi.Project | null>(null);
  const loading = ref(false);

  async function fetchProjects() {
    loading.value = true;
    try {
      projects.value = await getProjectsApi();
    } finally {
      loading.value = false;
    }
  }

  async function fetchProject(id: string) {
    loading.value = true;
    try {
      currentProject.value = await getProjectApi(id);
    } finally {
      loading.value = false;
    }
  }

  function setCurrentProject(project: ProjectApi.Project | null) {
    currentProject.value = project;
  }

  function $reset() {
    projects.value = [];
    currentProject.value = null;
    loading.value = false;
  }

  return {
    $reset,
    currentProject,
    fetchProject,
    fetchProjects,
    loading,
    projects,
    setCurrentProject,
  };
});
