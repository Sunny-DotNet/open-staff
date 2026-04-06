<script lang="ts" setup>
import type { McpApi } from '#/api/openstaff/mcp';

import { computed, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Badge,
  Button,
  Card,
  Col,
  Empty,
  Input,
  Row,
  Space,
  Spin,
  Tag,
  Tooltip,
  Typography,
} from 'ant-design-vue';

import { getMcpConfigsApi, getMcpServersApi } from '#/api/openstaff/mcp';

import McpConfigModal from './McpConfigModal.vue';

// ===== 常量 =====
const CATEGORIES = [
  { key: '', label: '全部' },
  { key: 'Development', label: '开发工具' },
  { key: 'Search', label: '搜索' },
  { key: 'FileSystem', label: '文件系统' },
  { key: 'Database', label: '数据库' },
  { key: 'Memory', label: '记忆' },
  { key: 'Network', label: '网络' },
  { key: 'Browser', label: '浏览器' },
  { key: 'Utility', label: '工具' },
] as const;

const CATEGORY_ICONS: Record<string, string> = {
  Browser: '🌍',
  Database: '🗄️',
  Development: '💻',
  FileSystem: '📁',
  Memory: '🧠',
  Network: '🌐',
  Search: '🔍',
  Utility: '🔧',
};

function getCategoryIcon(category: string): string {
  return CATEGORY_ICONS[category] ?? '📦';
}

function getSourceColor(source: string): string {
  switch (source) {
    case 'builtin': {
      return 'blue';
    }
    case 'custom': {
      return 'green';
    }
    case 'marketplace': {
      return 'purple';
    }
    default: {
      return 'default';
    }
  }
}

function getSourceLabel(source: string): string {
  switch (source) {
    case 'builtin': {
      return '内置';
    }
    case 'custom': {
      return '自定义';
    }
    case 'marketplace': {
      return '市场';
    }
    default: {
      return source;
    }
  }
}

// ===== 状态 =====
const servers = ref<McpApi.McpServer[]>([]);
const configs = ref<McpApi.McpServerConfig[]>([]);
const loading = ref(false);
const searchText = ref('');
const activeCategory = ref('');

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

const filteredServers = computed(() => {
  let list = servers.value;
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
async function fetchData() {
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
function openConfigModal(server: McpApi.McpServer) {
  selectedServer.value = server;
  showConfigModal.value = true;
}

function handleConfigSaved() {
  fetchData();
}

onMounted(fetchData);
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

    <!-- 分类过滤 -->
    <div style="margin-bottom: 20px">
      <Space wrap>
        <Tag
          v-for="cat in CATEGORIES"
          :key="cat.key"
          :color="activeCategory === cat.key ? 'blue' : undefined"
          style="cursor: pointer; padding: 4px 12px; font-size: 13px"
          @click="activeCategory = cat.key"
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
        v-if="filteredServers.length === 0"
        description="没有找到匹配的 MCP 服务器"
        style="padding: 60px 0"
      />

      <Row :gutter="[16, 16]">
        <Col
          v-for="server in filteredServers"
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
                  <Badge
                    v-if="configCountMap.get(server.id)"
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

            <!-- 标签 -->
            <div style="margin-top: auto; display: flex; align-items: center; justify-content: space-between">
              <Space :size="4" wrap>
                <Tag style="font-size: 11px; margin: 0">
                  {{ server.transportType }}
                </Tag>
                <Tag
                  :color="getSourceColor(server.source)"
                  style="font-size: 11px; margin: 0"
                >
                  {{ getSourceLabel(server.source) }}
                </Tag>
              </Space>
              <Tooltip title="管理配置">
                <Button
                  type="primary"
                  size="small"
                  @click="openConfigModal(server)"
                >
                  配置
                </Button>
              </Tooltip>
            </div>
          </Card>
        </Col>
      </Row>
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
