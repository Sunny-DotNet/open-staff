import type { AgentApi } from '#/api/openstaff/agent';

import { ref } from 'vue';

import { defineStore } from 'pinia';

import { getAgentsApi, getMessagesApi } from '#/api/openstaff/agent';

export const useAgentStore = defineStore('openstaff-agent', () => {
  const agents = ref<AgentApi.Agent[]>([]);
  const messages = ref<AgentApi.Message[]>([]);
  const loading = ref(false);

  async function fetchAgents(projectId: string) {
    loading.value = true;
    try {
      agents.value = await getAgentsApi(projectId);
    } finally {
      loading.value = false;
    }
  }

  async function fetchMessages(projectId: string) {
    try {
      messages.value = await getMessagesApi(projectId);
    } catch {
      // Silently fail on message fetch
    }
  }

  function addMessage(message: AgentApi.Message) {
    messages.value.push(message);
  }

  function updateAgentStatus(
    agentId: string,
    status: AgentApi.Agent['status'],
  ) {
    const agent = agents.value.find((a) => a.id === agentId);
    if (agent) {
      agent.status = status;
    }
  }

  function $reset() {
    agents.value = [];
    messages.value = [];
    loading.value = false;
  }

  return {
    $reset,
    addMessage,
    agents,
    fetchAgents,
    fetchMessages,
    loading,
    messages,
    updateAgentStatus,
  };
});
