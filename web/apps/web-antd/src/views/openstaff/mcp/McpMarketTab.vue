<script lang="ts" setup>
import type { McpApi, McpMarketplaceApi } from '#/api/openstaff/mcp';

import { computed, onMounted, ref, watch } from 'vue';

import { IconifyIcon } from '@vben/icons';

import {
  Badge,
  Button,
  Empty,
  Input,
  message,
  Space,
  Spin,
  Tabs,
  TabPane,
  Tag,
  Tooltip,
} from 'ant-design-vue';

import {
  getMcpConfigsApi,
  getMarketplaceSourcesApi,
  searchMarketplaceApi,
  installFromMarketplaceApi,
} from '#/api/openstaff/mcp';

const emit = defineEmits<{
  goToConfigs: [serverId: string];
  installed: [];
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
  sse: 'orange',
  'streamable-http': 'purple',
};

// ===== 常量 =====
const CATEGORIES = [
  { key: '', label: '全部', icon: 'lucide:grid-3x3' },
  { key: 'dev-tools', label: '开发工具', icon: 'lucide:code' },
  { key: 'search', label: '搜索', icon: 'lucide:search' },
  { key: 'filesystem', label: '文件系统', icon: 'lucide:folder-open' },
  { key: 'database', label: '数据库', icon: 'lucide:database' },
  { key: 'memory', label: '记忆', icon: 'lucide:brain' },
  { key: 'communication', label: '网络', icon: 'lucide:wifi' },
  { key: 'browser', label: '浏览器', icon: 'lucide:globe' },
  { key: 'general', label: '通用', icon: 'lucide:package' },
] as const;

function getIconName(server: McpMarketplaceApi.MarketplaceServer): string {
  if (server.icon && ICON_MAP[server.icon]) return ICON_MAP[server.icon]!;
  if (server.category && ICON_MAP[server.category]) return ICON_MAP[server.category]!;
  return 'lucide:package';
}

function getDisplayName(server: McpMarketplaceApi.MarketplaceServer): string {
  const name = server.name;
  if (name.includes('/')) {
    return name.split('/').pop() ?? name;
  }
  return name;
}

// ===== 状态 =====
const sources = ref<McpMarketplaceApi.MarketplaceSource[]>([]);
const activeSource = ref('internal');
const items = ref<McpMarketplaceApi.MarketplaceServer[]>([]);
const configs = ref<McpApi.McpServerConfig[]>([]);
const loading = ref(false);
const searchText = ref('');
const activeCategory = ref('');
const nextCursor = ref<null | string>(null);
const loadingMore = ref(false);
const installingIds = ref(new Set<string>());

// ===== 计算属性 =====
const configCountMap = computed(() => {
  const map = new Map<string, number>();
  for (const c of configs.value) {
    map.set(c.mcpServerId, (map.get(c.mcpServerId) ?? 0) + 1);
  }
  return map;
});

const filteredItems = computed(() => {
  let list = items.value;
  if (activeCategory.value) {
    list = list.filter((s) => s.category === activeCategory.value);
  }
  if (searchText.value.trim()) {
    const q = searchText.value.trim().toLowerCase();
    list = list.filter(
      (s) =>
        s.name.toLowerCase().includes(q) ||
        (s.description && s.description.toLowerCase().includes(q)),
    );
  }
  return list;
});

// ===== 数据加载 =====
async function fetchSources() {
  try {
    sources.value = await getMarketplaceSourcesApi();
    if (sources.value.length > 0 && !sources.value.find(s => s.sourceKey === activeSource.value)) {
      activeSource.value = sources.value[0]!.sourceKey;
    }
  } catch {
    sources.value = [{ sourceKey: 'internal', displayName: '内置', iconUrl: null }];
  }
}

async function fetchItems(append = false) {
  if (!append) {
    loading.value = true;
    nextCursor.value = null;
  } else {
    loadingMore.value = true;
  }
  try {
    const result = await searchMarketplaceApi({
      sourceKey: activeSource.value,
      keyword: searchText.value.trim() || undefined,
      category: activeCategory.value || undefined,
      cursor: append ? (nextCursor.value ?? undefined) : undefined,
      pageSize: 24,
    });
    if (append) {
      items.value = [...items.value, ...result.items];
    } else {
      items.value = result.items;
    }
    nextCursor.value = result.nextCursor;
  } catch {
    if (!append) items.value = [];
  } finally {
    loading.value = false;
    loadingMore.value = false;
  }
}

async function fetchConfigs() {
  try {
    configs.value = await getMcpConfigsApi();
  } catch {
    configs.value = [];
  }
}

async function fetchAll() {
  await Promise.all([fetchSources(), fetchItems(), fetchConfigs()]);
}

// ===== 操作 =====
async function handleInstall(server: McpMarketplaceApi.MarketplaceServer) {
  installingIds.value.add(server.id);
  try {
    await installFromMarketplaceApi({
      sourceKey: activeSource.value,
      serverId: server.id,
    });
    server.isInstalled = true;
    message.success(`${server.name} 已安装到本地`);
    emit('installed');
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : String(e);
    message.error(`安装失败: ${msg}`);
  } finally {
    installingIds.value.delete(server.id);
  }
}

function goToConfigs(server: McpMarketplaceApi.MarketplaceServer) {
  emit('goToConfigs', server.id);
}

// ===== Watch =====
watch(activeSource, () => {
  items.value = [];
  fetchItems();
});

let searchTimer: ReturnType<typeof setTimeout>;
watch(searchText, () => {
  clearTimeout(searchTimer);
  searchTimer = setTimeout(() => fetchItems(), 300);
});

onMounted(fetchAll);

defineExpose({ refresh: fetchAll });
</script>

<template>
  <div>
    <!-- 搜索栏 -->
    <div style="display: flex; justify-content: flex-end; margin-bottom: 16px">
      <Input.Search
        v-model:value="searchText"
        placeholder="搜索 MCP 服务器..."
        style="width: 280px"
        allow-clear
      />
    </div>

    <!-- 数据源 Tabs -->
    <Tabs v-model:activeKey="activeSource" style="margin-bottom: 8px">
      <TabPane
        v-for="src in sources"
        :key="src.sourceKey"
        :tab="src.displayName"
      />
    </Tabs>

    <!-- 分类过滤（仅内置源显示） -->
    <div v-if="activeSource === 'internal'" class="category-bar">
      <div
        v-for="cat in CATEGORIES"
        :key="cat.key"
        :class="['category-chip', { active: activeCategory === cat.key }]"
        @click="activeCategory = cat.key; fetchItems()"
      >
        <IconifyIcon :icon="cat.icon" :width="14" />
        <span>{{ cat.label }}</span>
      </div>
    </div>

    <!-- 加载状态 -->
    <div v-if="loading" style="text-align: center; padding: 80px 0">
      <Spin size="large" />
    </div>

    <!-- 服务器卡片 -->
    <template v-else>
      <Empty
        v-if="filteredItems.length === 0"
        description="没有找到匹配的 MCP 服务器"
        style="padding: 60px 0"
      />

      <div class="server-grid">
        <div
          v-for="server in filteredItems"
          :key="server.id"
          class="server-card"
          @click="(server.isInstalled || activeSource === 'internal') && goToConfigs(server)"
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
              <Tooltip :title="server.name">
                <span class="card-name">{{ getDisplayName(server) }}</span>
              </Tooltip>
              <Tag
                v-if="server.version"
                class="version-tag"
              >
                {{ server.version }}
              </Tag>
            </div>
            <p class="card-desc">{{ server.description || '暂无描述' }}</p>
          </div>

          <!-- 底部：传输类型 + 操作 -->
          <div class="card-footer">
            <Space :size="4" wrap>
              <Tag
                v-for="tt in server.transportTypes"
                :key="tt"
                :color="TRANSPORT_COLORS[tt] || 'default'"
                class="transport-tag"
              >
                {{ tt }}
              </Tag>
            </Space>
            <Space :size="6">
              <template v-if="activeSource !== 'internal'">
                <Tag v-if="server.isInstalled" color="success" class="installed-tag">
                  <IconifyIcon icon="lucide:check" :width="12" style="margin-right: 2px" />
                  已安装
                </Tag>
                <Button
                  v-else
                  type="primary"
                  size="small"
                  :loading="installingIds.has(server.id)"
                  @click.stop="handleInstall(server)"
                >
                  <template #icon><IconifyIcon icon="lucide:download" :width="14" /></template>
                  安装
                </Button>
              </template>
              <Tooltip v-if="server.isInstalled || activeSource === 'internal'" title="管理配置">
                <Button
                  size="small"
                  @click.stop="goToConfigs(server)"
                >
                  <template #icon><IconifyIcon icon="lucide:settings" :width="14" /></template>
                  配置
                </Button>
              </Tooltip>
            </Space>
          </div>
        </div>
      </div>

      <!-- 加载更多 -->
      <div v-if="nextCursor" style="text-align: center; margin-top: 24px">
        <Button :loading="loadingMore" @click="fetchItems(true)">
          <template #icon><IconifyIcon icon="lucide:chevrons-down" :width="14" /></template>
          加载更多
        </Button>
      </div>
    </template>
  </div>
</template>

<style scoped>
/* 分类筛选栏 */
.category-bar {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 20px;
}

.category-chip {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 6px 14px;
  border-radius: 20px;
  font-size: 13px;
  cursor: pointer;
  background: var(--ant-color-fill-quaternary, rgba(255, 255, 255, 0.04));
  border: 1px solid transparent;
  transition: all 0.2s;
  user-select: none;
}

.category-chip:hover {
  background: var(--ant-color-fill-tertiary, rgba(255, 255, 255, 0.08));
}

.category-chip.active {
  color: var(--ant-color-primary);
  border-color: var(--ant-color-primary);
  background: var(--ant-color-primary-bg, rgba(22, 119, 255, 0.1));
}

/* 卡片网格 */
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

/* 图标区 */
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

/* 信息区 */
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

.version-tag {
  font-size: 11px;
  line-height: 1.4;
  padding: 0 6px;
  margin: 0;
  border-radius: 4px;
  opacity: 0.7;
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

/* 底部 */
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

.installed-tag {
  margin: 0;
  font-size: 12px;
}
</style>
