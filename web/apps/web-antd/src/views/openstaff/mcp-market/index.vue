<script lang="ts" setup>
import type { McpApi, McpMarketplaceApi } from '#/api/openstaff/mcp';

import { computed, onMounted, ref, watch } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Badge,
  Button,
  Card,
  Col,
  Empty,
  Input,
  message,
  Row,
  Space,
  Spin,
  Tabs,
  TabPane,
  Tag,
  Tooltip,
  Typography,
} from 'ant-design-vue';

import {
  getMcpConfigsApi,
  getMarketplaceSourcesApi,
  searchMarketplaceApi,
  installFromMarketplaceApi,
} from '#/api/openstaff/mcp';

import McpConfigModal from './McpConfigModal.vue';

// ===== 常量 =====
const CATEGORIES = [
  { key: '', label: '全部' },
  { key: 'dev-tools', label: '开发工具' },
  { key: 'search', label: '搜索' },
  { key: 'filesystem', label: '文件系统' },
  { key: 'database', label: '数据库' },
  { key: 'memory', label: '记忆' },
  { key: 'communication', label: '网络' },
  { key: 'browser', label: '浏览器' },
  { key: 'general', label: '通用' },
] as const;

const CATEGORY_ICONS: Record<string, string> = {
  browser: '🌍',
  'dev-tools': '💻',
  database: '🗄️',
  filesystem: '📁',
  general: '📦',
  memory: '🧠',
  communication: '🌐',
  search: '🔍',
};

function getCategoryIcon(category: string): string {
  return CATEGORY_ICONS[category] ?? '📦';
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

// Modal
const showConfigModal = ref(false);
const selectedServer = ref<McpApi.McpServer | null>(null);

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
function openConfigModal(server: McpMarketplaceApi.MarketplaceServer) {
  // 转换为 McpServer 格式给 Modal（仅内置源支持配置）
  selectedServer.value = {
    id: server.id,
    name: server.name,
    description: server.description,
    icon: server.icon,
    category: server.category,
    transportType: server.transportTypes[0] ?? 'stdio',
    source: server.source,
    defaultConfig: server.defaultConfig,
    marketplaceUrl: server.repositoryUrl,
  } as McpApi.McpServer;
  showConfigModal.value = true;
}

async function handleInstall(server: McpMarketplaceApi.MarketplaceServer) {
  installingIds.value.add(server.id);
  try {
    await installFromMarketplaceApi({
      sourceKey: activeSource.value,
      serverId: server.id,
    });
    server.isInstalled = true;
    message.success(`${server.name} 已安装到本地`);
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : String(e);
    message.error(`安装失败: ${msg}`);
  } finally {
    installingIds.value.delete(server.id);
  }
}

function handleConfigSaved() {
  fetchConfigs();
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
</script>

<template>
  <Page title="MCP 市场">
    <template #extra>
      <Input.Search
        v-model:value="searchText"
        placeholder="搜索 MCP 服务器..."
        style="width: 280px"
        allow-clear
      />
    </template>

    <!-- 数据源 Tabs -->
    <Tabs v-model:activeKey="activeSource" style="margin-bottom: 8px">
      <TabPane
        v-for="src in sources"
        :key="src.sourceKey"
        :tab="src.displayName"
      />
    </Tabs>

    <!-- 分类过滤（仅内置源显示） -->
    <div v-if="activeSource === 'internal'" style="margin-bottom: 20px">
      <Space wrap>
        <Tag
          v-for="cat in CATEGORIES"
          :key="cat.key"
          :color="activeCategory === cat.key ? 'blue' : undefined"
          style="cursor: pointer; padding: 4px 12px; font-size: 13px"
          @click="activeCategory = cat.key; fetchItems()"
        >
          {{ cat.label }}
        </Tag>
      </Space>
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

      <Row :gutter="[16, 16]">
        <Col
          v-for="server in filteredItems"
          :key="server.id"
          :xs="24"
          :sm="12"
          :md="8"
          :lg="6"
        >
          <Card
            hoverable
            style="height: 100%"
            :body-style="{ padding: '20px', display: 'flex', flexDirection: 'column', height: '100%' }"
          >
            <!-- 头部：图标 + 名称 -->
            <div style="display: flex; align-items: flex-start; gap: 12px; margin-bottom: 12px">
              <span style="font-size: 32px; line-height: 1">
                {{ server.icon || getCategoryIcon(server.category) }}
              </span>
              <div style="flex: 1; min-width: 0">
                <div style="display: flex; align-items: center; gap: 8px">
                  <Typography.Text
                    strong
                    style="font-size: 15px"
                    :ellipsis="{ tooltip: server.name }"
                  >
                    {{ server.name }}
                  </Typography.Text>
                  <Tag v-if="server.version" style="font-size: 10px; margin: 0">
                    v{{ server.version }}
                  </Tag>
                  <Badge
                    v-if="server.isInstalled && configCountMap.get(server.id)"
                    :count="configCountMap.get(server.id)"
                    :number-style="{ backgroundColor: '#52c41a', fontSize: '11px' }"
                  />
                </div>
                <Typography.Paragraph
                  type="secondary"
                  :ellipsis="{ rows: 2, tooltip: true }"
                  :content="server.description || '暂无描述'"
                  style="margin-bottom: 0; font-size: 12px; margin-top: 4px"
                />
              </div>
            </div>

            <!-- 标签 + 操作 -->
            <div style="margin-top: auto; display: flex; align-items: center; justify-content: space-between">
              <Space :size="4" wrap>
                <Tag
                  v-for="tt in server.transportTypes"
                  :key="tt"
                  style="font-size: 11px; margin: 0"
                >
                  {{ tt }}
                </Tag>
              </Space>
              <Space :size="4">
                <!-- 外部源：安装按钮 -->
                <template v-if="activeSource !== 'internal'">
                  <Tag v-if="server.isInstalled" color="green" style="margin: 0">
                    已安装
                  </Tag>
                  <Button
                    v-else
                    type="primary"
                    size="small"
                    :loading="installingIds.has(server.id)"
                    @click="handleInstall(server)"
                  >
                    安装
                  </Button>
                </template>
                <!-- 已安装：配置按钮 -->
                <Tooltip v-if="server.isInstalled || activeSource === 'internal'" title="管理配置">
                  <Button
                    type="primary"
                    size="small"
                    ghost
                    @click="openConfigModal(server)"
                  >
                    配置
                  </Button>
                </Tooltip>
              </Space>
            </div>
          </Card>
        </Col>
      </Row>

      <!-- 加载更多 -->
      <div v-if="nextCursor" style="text-align: center; margin-top: 24px">
        <Button :loading="loadingMore" @click="fetchItems(true)">
          加载更多
        </Button>
      </div>
    </template>

    <!-- 配置 Modal -->
    <McpConfigModal
      :open="showConfigModal"
      :server="selectedServer"
      @update:open="showConfigModal = $event"
      @saved="handleConfigSaved"
    />
  </Page>
</template>
