<script lang="ts" setup>
import type { McpApi } from '#/api/openstaff/mcp';

import { computed, onMounted, ref } from 'vue';

import { IconifyIcon } from '@vben/icons';

import {
  Badge,
  Button,
  Empty,
  message,
  Popconfirm,
  Space,
  Spin,
  Tag,
} from 'ant-design-vue';

import {
  deleteMcpServerApi,
  getMcpConfigsApi,
  getMcpServersApi,
} from '#/api/openstaff/mcp';

const emit = defineEmits<{
  goToConfigs: [serverId: string];
}>();

// ===== 图标映射 =====
const ICON_MAP: Record<string, string> = {
  search: 'lucide:search',
  folder: 'lucide:folder',
  globe: 'lucide:globe',
  github: 'lucide:github',
  brain: 'lucide:brain',
  database: 'lucide:database',
  chrome: 'lucide:chrome',
  lightbulb: 'lucide:lightbulb',
  memory: 'lucide:brain',
  browser: 'lucide:globe',
  filesystem: 'lucide:folder-open',
  'dev-tools': 'lucide:code',
  communication: 'lucide:wifi',
  general: 'lucide:package',
};

const TRANSPORT_COLORS: Record<string, string> = {
  stdio: 'cyan',
  http: 'orange',
  sse: 'orange',
  'streamable-http': 'purple',
};

const SOURCE_LABELS: Record<string, { label: string; color: string }> = {
  builtin: { label: '内置', color: 'blue' },
  marketplace: { label: '市场安装', color: 'green' },
  custom: { label: '自定义', color: 'orange' },
};

function getIconName(server: McpApi.McpServer): string {
  if (server.icon && ICON_MAP[server.icon]) return ICON_MAP[server.icon]!;
  if (server.category && ICON_MAP[server.category]) return ICON_MAP[server.category]!;
  return 'lucide:package';
}

// ===== 状态 =====
const servers = ref<McpApi.McpServer[]>([]);
const configs = ref<McpApi.McpServerConfig[]>([]);
const loading = ref(false);

const configCountMap = computed(() => {
  const map = new Map<string, number>();
  for (const c of configs.value) {
    map.set(c.mcpServerId, (map.get(c.mcpServerId) ?? 0) + 1);
  }
  return map;
});

// ===== 数据加载 =====
async function fetchServers() {
  loading.value = true;
  try {
    const [serverList, configList] = await Promise.all([
      getMcpServersApi(),
      getMcpConfigsApi(),
    ]);
    servers.value = serverList;
    configs.value = configList;
  } catch {
    servers.value = [];
    configs.value = [];
  } finally {
    loading.value = false;
  }
}

// ===== 操作 =====
async function handleDelete(server: McpApi.McpServer) {
  try {
    await deleteMcpServerApi(server.id);
    message.success(`${server.name} 已删除`);
    await fetchServers();
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : String(e);
    message.error(`删除失败: ${msg}`);
  }
}

function goToConfigs(serverId: string) {
  emit('goToConfigs', serverId);
}

onMounted(fetchServers);

defineExpose({ refresh: fetchServers });
</script>

<template>
  <div>
    <!-- 加载状态 -->
    <div v-if="loading" style="text-align: center; padding: 80px 0">
      <Spin size="large" />
    </div>

    <template v-else>
      <Empty
        v-if="servers.length === 0"
        description="暂无已安装的 MCP 服务器"
        style="padding: 60px 0"
      />

      <div class="server-grid">
        <div
          v-for="server in servers"
          :key="server.id"
          class="server-card"
          @click="goToConfigs(server.id)"
        >
          <!-- 图标区 -->
          <div class="card-icon">
            <IconifyIcon :icon="getIconName(server)" :width="28" />
            <Badge
              v-if="configCountMap.get(server.id)"
              :count="configCountMap.get(server.id)"
              :number-style="{ backgroundColor: '#52c41a', fontSize: '10px', boxShadow: 'none' }"
              class="config-badge"
            />
          </div>

          <!-- 信息区 -->
          <div class="card-body">
            <div class="card-header">
              <span class="card-name">{{ server.name }}</span>
              <Tag
                :color="SOURCE_LABELS[server.source]?.color || 'default'"
                class="source-tag"
              >
                {{ SOURCE_LABELS[server.source]?.label || server.source }}
              </Tag>
            </div>
            <p class="card-desc">{{ server.description || '暂无描述' }}</p>
          </div>

          <!-- 底部：传输类型 + 操作 -->
          <div class="card-footer">
            <Tag
              :color="TRANSPORT_COLORS[server.transportType] || 'default'"
              class="transport-tag"
            >
              {{ server.transportType }}
            </Tag>
            <Space :size="6">
              <Button
                size="small"
                @click.stop="goToConfigs(server.id)"
              >
                <template #icon><IconifyIcon icon="lucide:settings" :width="14" /></template>
                查看配置
              </Button>
              <Popconfirm
                v-if="server.source !== 'builtin'"
                :title="`确认删除 ${server.name}？相关配置也将被删除。`"
                @confirm="handleDelete(server)"
              >
                <Button
                  size="small"
                  danger
                  @click.stop
                >
                  <template #icon><IconifyIcon icon="lucide:trash-2" :width="14" /></template>
                  删除
                </Button>
              </Popconfirm>
            </Space>
          </div>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
.server-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
  gap: 16px;
}

.server-card {
  display: flex;
  flex-direction: column;
  padding: 20px;
  border-radius: 12px;
  border: 1px solid var(--ant-color-border-secondary, rgba(255, 255, 255, 0.06));
  background: var(--ant-color-bg-container);
  cursor: pointer;
  transition: all 0.25s ease;
}

.server-card:hover {
  border-color: var(--ant-color-primary);
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.15);
  transform: translateY(-2px);
}

.card-icon {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 52px;
  height: 52px;
  border-radius: 12px;
  background: var(--ant-color-primary-bg, rgba(22, 119, 255, 0.08));
  color: var(--ant-color-primary);
  margin-bottom: 14px;
  flex-shrink: 0;
}

.config-badge {
  position: absolute;
  top: -4px;
  right: -4px;
}

.card-body {
  flex: 1;
  min-height: 0;
  margin-bottom: 14px;
}

.card-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 6px;
}

.card-name {
  font-size: 15px;
  font-weight: 600;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: 200px;
}

.source-tag {
  font-size: 11px;
  line-height: 1.4;
  padding: 0 6px;
  margin: 0;
  border-radius: 4px;
}

.card-desc {
  font-size: 13px;
  color: var(--ant-color-text-secondary);
  margin: 0;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  line-height: 1.5;
}

.card-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding-top: 12px;
  border-top: 1px solid var(--ant-color-border-secondary, rgba(255, 255, 255, 0.06));
}

.transport-tag {
  font-size: 11px;
  margin: 0;
  border-radius: 4px;
}
</style>
