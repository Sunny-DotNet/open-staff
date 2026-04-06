<script lang="ts" setup>
import { onMounted, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';

import { Tabs, TabPane } from 'ant-design-vue';

import McpConfigsTab from './McpConfigsTab.vue';
import McpInstalledTab from './McpInstalledTab.vue';
import McpMarketTab from './McpMarketTab.vue';

const route = useRoute();
const router = useRouter();

const activeTab = ref((route.query.tab as string) || 'configs');
const configsRef = ref<InstanceType<typeof McpConfigsTab>>();
const installedRef = ref<InstanceType<typeof McpInstalledTab>>();

// Sync tab to URL query
watch(activeTab, (tab) => {
  router.replace({ query: { ...route.query, tab } });
});

// Handle cross-tab navigation
function handleGoToConfigs(serverId: string) {
  activeTab.value = 'configs';
  configsRef.value?.setServerFilter(serverId);
}

function handleSwitchTab(tab: string) {
  activeTab.value = tab;
}

function handleInstalled() {
  installedRef.value?.refresh();
}

// Handle initial serverId from URL query
onMounted(() => {
  const serverId = route.query.serverId as string | undefined;
  if (serverId) {
    activeTab.value = 'configs';
    // Wait for next tick so configsRef is available
    setTimeout(() => {
      configsRef.value?.setServerFilter(serverId);
    }, 0);
  }
});
</script>

<template>
  <Page title="MCP 管理" description="管理 MCP 服务器配置、已安装服务器和市场">
    <Tabs v-model:activeKey="activeTab" size="large">
      <TabPane key="configs" tab="⚙️ 配置">
        <McpConfigsTab
          ref="configsRef"
          @switch-tab="handleSwitchTab"
        />
      </TabPane>
      <TabPane key="installed" tab="📦 已安装">
        <McpInstalledTab
          ref="installedRef"
          @go-to-configs="handleGoToConfigs"
        />
      </TabPane>
      <TabPane key="market" tab="🏪 市场">
        <McpMarketTab
          @go-to-configs="handleGoToConfigs"
          @installed="handleInstalled"
        />
      </TabPane>
    </Tabs>
  </Page>
</template>
