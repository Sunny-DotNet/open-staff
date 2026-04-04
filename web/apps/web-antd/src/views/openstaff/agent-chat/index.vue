<script lang="ts" setup>
import { ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Avatar,
  Badge,
  Button,
  Card,
  Col,
  Input,
  List,
  ListItem,
  ListItemMeta,
  Row,
  Tag,
} from 'ant-design-vue';

import { $t } from '#/locales';

const InputTextArea = Input.TextArea;

interface Agent {
  id: string;
  name: string;
  role: string;
  status: 'idle' | 'thinking' | 'working';
  avatar: string;
}

interface Message {
  id: string;
  agentName: string;
  content: string;
  type: 'chat' | 'code' | 'error' | 'system';
  timestamp: string;
}

interface TaskItem {
  id: string;
  title: string;
  status: string;
  agent: string;
}

const agents = ref<Agent[]>([
  {
    id: '1',
    name: 'PM',
    role: '项目经理',
    status: 'idle',
    avatar: 'https://avatar.vercel.sh/pm.svg?text=PM',
  },
  {
    id: '2',
    name: 'Architect',
    role: '架构师',
    status: 'thinking',
    avatar: 'https://avatar.vercel.sh/arch.svg?text=AR',
  },
  {
    id: '3',
    name: 'Coder',
    role: '开发工程师',
    status: 'working',
    avatar: 'https://avatar.vercel.sh/coder.svg?text=CD',
  },
  {
    id: '4',
    name: 'Reviewer',
    role: '代码审查',
    status: 'idle',
    avatar: 'https://avatar.vercel.sh/review.svg?text=RV',
  },
  {
    id: '5',
    name: 'Tester',
    role: '测试工程师',
    status: 'idle',
    avatar: 'https://avatar.vercel.sh/test.svg?text=TS',
  },
]);

const messages = ref<Message[]>([
  {
    id: '1',
    agentName: 'PM',
    content: '开始分析需求文档，确定项目范围和目标...',
    type: 'chat',
    timestamp: '14:00:00',
  },
  {
    id: '2',
    agentName: 'Architect',
    content: '根据需求分析，建议采用微服务架构，主要包括用户服务、订单服务和通知服务。',
    type: 'chat',
    timestamp: '14:02:30',
  },
  {
    id: '3',
    agentName: 'System',
    content: '任务「系统设计」已分配给 Architect',
    type: 'system',
    timestamp: '14:03:00',
  },
  {
    id: '4',
    agentName: 'Coder',
    content:
      '```typescript\nexport class UserService {\n  async createUser(dto: CreateUserDto) {\n    // 实现用户创建逻辑\n  }\n}\n```',
    type: 'code',
    timestamp: '14:10:00',
  },
  {
    id: '5',
    agentName: 'Reviewer',
    content: '代码审查发现：UserService 缺少输入验证，建议添加 DTO 校验装饰器。',
    type: 'chat',
    timestamp: '14:15:00',
  },
]);

const tasks = ref<TaskItem[]>([
  { id: '1', title: '需求分析', status: 'done', agent: 'PM' },
  { id: '2', title: '系统设计', status: 'in_progress', agent: 'Architect' },
  { id: '3', title: '用户模块开发', status: 'in_progress', agent: 'Coder' },
  { id: '4', title: '代码审查', status: 'pending', agent: 'Reviewer' },
  { id: '5', title: '单元测试', status: 'pending', agent: 'Tester' },
]);

const inputMessage = ref('');

const statusColorMap: Record<string, string> = {
  idle: 'default',
  thinking: 'processing',
  working: 'success',
};

const statusTextMap: Record<string, string> = {
  idle: 'openstaff.agentChat.statusIdle',
  thinking: 'openstaff.agentChat.statusThinking',
  working: 'openstaff.agentChat.statusWorking',
};

const taskStatusColorMap: Record<string, string> = {
  done: 'green',
  in_progress: 'blue',
  pending: 'default',
};

function getMessageClass(type: string) {
  switch (type) {
    case 'code': {
      return 'bg-gray-100 dark:bg-gray-800 rounded p-2 font-mono text-sm';
    }
    case 'error': {
      return 'text-red-500';
    }
    case 'system': {
      return 'text-gray-400 text-sm italic';
    }
    default: {
      return '';
    }
  }
}

function handleSend() {
  if (!inputMessage.value.trim()) return;
  messages.value.push({
    id: String(Date.now()),
    agentName: 'You',
    content: inputMessage.value,
    type: 'chat',
    timestamp: new Date().toLocaleTimeString('zh-CN', { hour12: false }),
  });
  inputMessage.value = '';
}
</script>

<template>
  <Page :title="$t('openstaff.agentChat.title')">
    <Row :gutter="16" class="h-[calc(100vh-200px)]">
      <!-- 左侧：智能体列表 -->
      <Col :span="5">
        <Card
          :title="$t('openstaff.agentChat.agentList')"
          class="h-full overflow-auto"
        >
          <List :data-source="agents" size="small">
            <template #renderItem="{ item }">
              <ListItem>
                <ListItemMeta :description="item.role">
                  <template #avatar>
                    <Badge :status="statusColorMap[item.status]" dot>
                      <Avatar :src="item.avatar" />
                    </Badge>
                  </template>
                  <template #title>
                    <span>{{ item.name }}</span>
                    <Tag
                      :color="
                        item.status === 'working'
                          ? 'green'
                          : item.status === 'thinking'
                            ? 'blue'
                            : 'default'
                      "
                      class="ml-2"
                      size="small"
                    >
                      {{ $t(statusTextMap[item.status]) }}
                    </Tag>
                  </template>
                </ListItemMeta>
              </ListItem>
            </template>
          </List>
        </Card>
      </Col>

      <!-- 中间：消息流 -->
      <Col :span="13">
        <Card
          :title="$t('openstaff.agentChat.messageStream')"
          class="flex h-full flex-col"
        >
          <div class="mb-4 flex-1 overflow-auto">
            <div
              v-for="msg in messages"
              :key="msg.id"
              class="mb-3 flex items-start gap-2"
            >
              <Tag color="blue" class="shrink-0">{{ msg.agentName }}</Tag>
              <div class="flex-1">
                <div :class="getMessageClass(msg.type)">
                  <pre
                    v-if="msg.type === 'code'"
                    class="m-0 whitespace-pre-wrap"
                    >{{ msg.content }}</pre
                  >
                  <span v-else>{{ msg.content }}</span>
                </div>
                <span class="text-gray-400 text-xs">{{ msg.timestamp }}</span>
              </div>
            </div>
          </div>
          <div class="flex gap-2">
            <InputTextArea
              v-model:value="inputMessage"
              :placeholder="$t('openstaff.agentChat.inputPlaceholder')"
              :rows="2"
              class="flex-1"
              @press-enter="handleSend"
            />
            <Button type="primary" @click="handleSend">
              {{ $t('openstaff.agentChat.sendMessage') }}
            </Button>
          </div>
        </Card>
      </Col>

      <!-- 右侧：任务面板 -->
      <Col :span="6">
        <Card
          :title="$t('openstaff.agentChat.taskPanel')"
          class="h-full overflow-auto"
        >
          <List :data-source="tasks" size="small">
            <template #renderItem="{ item }">
              <ListItem>
                <ListItemMeta :description="`智能体: ${item.agent}`">
                  <template #title>
                    <span>{{ item.title }}</span>
                    <Tag :color="taskStatusColorMap[item.status]" class="ml-2">
                      {{ item.status }}
                    </Tag>
                  </template>
                </ListItemMeta>
              </ListItem>
            </template>
          </List>
        </Card>
      </Col>
    </Row>
  </Page>
</template>
