<script lang="ts" setup>
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue';
import { useRoute } from 'vue-router';
import { Page } from '@vben/common-ui';
import {
  Avatar, Badge, Button, Drawer, Empty, Input, Space, Spin, Tag, Tooltip,
} from 'ant-design-vue';
import type { AgentApi } from '#/api/openstaff/agent';
import {
  cancelSessionApi, createSessionApi, getChatMessagesApi,
  getSessionEventsApi, sendSessionMessageApi,
} from '#/api/openstaff/agent';
import { getProjectApi, getProjectAgentsApi } from '#/api/openstaff/project';
import type { ProjectApi } from '#/api/openstaff/project';
import { useNotification } from '#/composables/useNotification';
import { AGENT_STATES, ROLE_NAMES, getRoleColor } from '#/constants/agent';
import { formatTime } from '#/utils/format';
import { $t } from '#/locales';

const InputTextArea = Input.TextArea;

function roleColor(role: string) { return getRoleColor(role); }
function roleName(role: string) {
  const pa = projectAgents.value.find((a) => a.agentRole?.roleType === role);
  return pa?.agentRole?.name || ROLE_NAMES[role] || role;
}

interface ChatMessage {
  id: string;
  sender: 'user' | 'agent' | 'system';
  agent?: string;
  content: string;
  timestamp: string;
}

type SessionStatus = 'none' | 'active' | 'awaiting_input' | 'completed' | 'error' | 'idle';

interface AgentStatus { role: string; state: 'idle' | 'thinking' | 'routing' | 'working'; }

const route = useRoute();
const projectId = computed(() => route.params.id as string);
const project = ref<ProjectApi.Project | null>(null);
const sessionId = ref<string | null>(null);
const sessionStatus = ref<SessionStatus>('none');
const messages = ref<ChatMessage[]>([]);
const inputMessage = ref('');
const loading = ref(false);
const sending = ref(false);
const thinkingAgent = ref<string | null>(null);
const hasMore = ref(false);
const totalMessages = ref(0);
const agentDrawerOpen = ref(false);
const taskDrawerOpen = ref(false);
const statsDrawerOpen = ref(false);
const chatContainerRef = ref<HTMLElement | null>(null);
const projectAgents = ref<ProjectApi.ProjectAgent[]>([]);
const agentStatuses = ref<AgentStatus[]>([]);
const tokenUsage = ref({ events: 0 });
let streamSub: { dispose: () => void } | null = null;
const { connected, streamSession } = useNotification();

const canInput = computed(() =>
  sessionId.value !== null &&
  (sessionStatus.value === 'awaiting_input' || sessionStatus.value === 'active'),
);

function scrollToBottom(smooth = true) {
  nextTick(() => {
    chatContainerRef.value?.scrollTo({
      top: chatContainerRef.value.scrollHeight,
      behavior: smooth ? 'smooth' : 'auto',
    });
  });
}

function setAgentState(role: string, state: AgentStatus['state']) {
  const a = agentStatuses.value.find((x) => x.role === role);
  if (a) a.state = state;
}
function resetAgentStates() { agentStatuses.value.forEach((a) => (a.state = 'idle')); }

function addMsg(m: Omit<ChatMessage, 'id'>) {
  messages.value.push({ ...m, id: `${m.sender}-${Date.now()}-${Math.random()}` });
  scrollToBottom();
}

function handleSessionEvent(evt: AgentApi.SessionEvent) {
  tokenUsage.value.events++;
  let p: Record<string, unknown> = {};
  if (evt.payload) { try { p = JSON.parse(evt.payload); } catch { p = { raw: evt.payload }; } }

  switch (evt.eventType) {
    case 'session_created': sessionStatus.value = 'active'; break;
    case 'user_input': break; // already added locally
    case 'thought': {
      const agent = p.agent || 'unknown';
      setAgentState(agent, 'thinking');
      thinkingAgent.value = agent;
      addMsg({ sender: 'system', content: `💭 ${roleName(agent)} 正在思考: ${p.message || '...'}`, timestamp: evt.createdAt });
      break;
    }
    case 'message': {
      const agent = p.agent || 'unknown';
      thinkingAgent.value = null;
      setAgentState(agent, 'idle');
      addMsg({ sender: 'agent', agent, content: p.content || '', timestamp: evt.createdAt });
      break;
    }
    case 'routing': {
      setAgentState(p.from || '', 'idle');
      setAgentState(p.to || '', 'routing');
      addMsg({ sender: 'system', content: `🔀 ${roleName(p.from || '?')} → ${roleName(p.to || '?')}${p.reason ? `: ${p.reason}` : ''}`, timestamp: evt.createdAt });
      break;
    }
    case 'frame_pushed': {
      const target = p.target || '?';
      setAgentState(target, 'working');
      addMsg({ sender: 'system', content: `📋 ${roleName(target)} 开始工作${p.purpose ? `: ${p.purpose}` : ''}`, timestamp: evt.createdAt });
      break;
    }
    case 'frame_completed': resetAgentStates(); break;
    case 'awaiting_input': {
      sessionStatus.value = 'awaiting_input';
      thinkingAgent.value = null;
      addMsg({ sender: 'system', content: `⏳ ${roleName(p.agent || 'communicator')}: ${p.message || '等待您的输入...'}`, timestamp: evt.createdAt });
      break;
    }
    case 'resumed_by_user': sessionStatus.value = 'active'; break;
    case 'session_completed': {
      sessionStatus.value = 'completed';
      thinkingAgent.value = null;
      resetAgentStates();
      addMsg({ sender: 'system', content: '✅ 会话已完成', timestamp: evt.createdAt });
      break;
    }
    case 'error': {
      sessionStatus.value = 'error';
      thinkingAgent.value = null;
      resetAgentStates();
      addMsg({ sender: 'system', content: `❌ 错误: ${p.message || p.raw || '未知错误'}`, timestamp: evt.createdAt });
      break;
    }
  }
}

async function connectStream(sid: string) {
  streamSub?.dispose();
  streamSub = null;
  try {
    streamSub = await streamSession(sid, handleSessionEvent,
      () => { if (sessionStatus.value !== 'completed' && sessionStatus.value !== 'error') sessionStatus.value = 'completed'; },
      (err) => { console.error('Stream error:', err); sessionStatus.value = 'error'; },
    );
  } catch (err) { console.error('Failed to connect stream:', err); }
}

async function loadHistory(sid: string, skip = 0, take = 50) {
  try {
    const resp = await getChatMessagesApi(sid, skip, take);
    const list = resp?.messages ?? [];
    totalMessages.value = resp?.total ?? list.length;
    hasMore.value = skip + take < totalMessages.value;
    const parsed: ChatMessage[] = list.map((m) => ({
      id: m.id || `hist-${Math.random()}`,
      sender: m.role === 'user' ? 'user' as const : 'agent' as const,
      agent: m.agent || m.agentRole || undefined,
      content: m.content || m.text || '',
      timestamp: m.createdAt || m.timestamp || '',
    }));
    messages.value = skip === 0 ? parsed : [...parsed, ...messages.value];
  } catch (err) { console.error('Failed to load history:', err); }
}

async function loadOlderMessages() {
  if (!sessionId.value || !hasMore.value || loading.value) return;
  loading.value = true;
  try {
    await loadHistory(sessionId.value, messages.value.filter((m) => m.sender !== 'system').length, 50);
  } finally { loading.value = false; }
}

async function reconstructFromEvents(sid: string) {
  try {
    const events = await getSessionEventsApi(sid);
    if (Array.isArray(events)) events.forEach(handleSessionEvent);
  } catch (err) { console.error('Failed to reconstruct:', err); }
}

async function initPage() {
  loading.value = true;
  try {
    project.value = await getProjectApi(projectId.value);
    // 加载项目配置的智能体
    try {
      projectAgents.value = await getProjectAgentsApi(projectId.value);
      agentStatuses.value = projectAgents.value.map((pa) => ({
        role: pa.agentRole?.roleType || pa.agentRoleId,
        state: 'idle' as const,
      }));
    } catch { agentStatuses.value = []; }
    const mainSid = project.value?.mainSessionId || project.value?.sessionId;
    if (mainSid) {
      sessionId.value = mainSid;
      sessionStatus.value = 'active';
      await loadHistory(mainSid);
      await reconstructFromEvents(mainSid);
      await connectStream(mainSid);
      scrollToBottom(false);
    } else {
      sessionStatus.value = 'none';
    }
  } catch (err) { console.error('Init error:', err); }
  finally { loading.value = false; }
}

async function startSession(input: string) {
  sending.value = true;
  try {
    const resp = await createSessionApi({ projectId: projectId.value, input, contextStrategy: 'hybrid' });
    const sid = resp.sessionId;
    if (!sid) throw new Error('No sessionId returned');
    sessionId.value = sid;
    sessionStatus.value = 'active';
    addMsg({ sender: 'user', content: input, timestamp: new Date().toISOString() });
    await connectStream(sid);
  } catch (err) { console.error('Failed to create session:', err); sessionStatus.value = 'error'; }
  finally { sending.value = false; }
}

async function handleSend() {
  const text = inputMessage.value.trim();
  if (!text) return;
  inputMessage.value = '';
  if (!sessionId.value) { await startSession(text); return; }
  sending.value = true;
  addMsg({ sender: 'user', content: text, timestamp: new Date().toISOString() });
  try {
    sessionStatus.value = 'active';
    await sendSessionMessageApi(sessionId.value, text);
  } catch (err) { console.error('Send error:', err); }
  finally { sending.value = false; }
}

function handleKeydown(e: KeyboardEvent) {
  if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); handleSend(); }
}

async function handleCancel() {
  if (!sessionId.value) return;
  try {
    await cancelSessionApi(sessionId.value);
    sessionStatus.value = 'completed';
    thinkingAgent.value = null;
    resetAgentStates();
  } catch (err) { console.error('Cancel error:', err); }
}

function handleScroll(e: Event) {
  const el = e.target as HTMLElement;
  if (el.scrollTop < 60 && hasMore.value && !loading.value) loadOlderMessages();
}

onMounted(initPage);
onUnmounted(() => streamSub?.dispose());
watch(connected, (val) => {
  if (val && sessionId.value && sessionStatus.value === 'active') connectStream(sessionId.value);
});
</script>

<template>
  <Page :title="project?.name || $t('openstaff.agentChat.title')">
    <template #extra>
      <Space>
        <Tooltip title="智能体状态"><Button @click="agentDrawerOpen = true">🤖 智能体</Button></Tooltip>
        <Tooltip title="任务进度"><Button @click="taskDrawerOpen = true">📋 任务</Button></Tooltip>
        <Tooltip title="统计"><Button @click="statsDrawerOpen = true">📊 统计</Button></Tooltip>
        <Button v-if="sessionStatus === 'active' && sessionId" danger @click="handleCancel">取消会话</Button>
      </Space>
    </template>

    <div class="chat-wrapper">
      <div v-if="sessionStatus === 'awaiting_input'" class="awaiting-banner">⏳ 等待您的输入...</div>
      <div v-if="!connected" class="connection-bar">🔴 正在连接服务器...</div>

      <div ref="chatContainerRef" class="chat-messages" @scroll="handleScroll">
        <div v-if="hasMore" class="load-more">
          <Button size="small" :loading="loading" @click="loadOlderMessages">加载更多消息</Button>
        </div>
        <div v-if="sessionStatus === 'none' && !loading" class="empty-state">
          <Empty description="发送第一条消息以开始对话" />
        </div>
        <div v-if="loading && messages.length === 0" class="empty-state"><Spin tip="加载中..." /></div>

        <div v-for="msg in messages" :key="msg.id"
          :class="['chat-row', { 'chat-row--right': msg.sender === 'user', 'chat-row--center': msg.sender === 'system' }]">
          <!-- System -->
          <div v-if="msg.sender === 'system'" class="system-event">
            <span class="system-text">{{ msg.content }}</span>
            <span class="system-time">{{ formatTime(msg.timestamp) }}</span>
          </div>
          <!-- User -->
          <div v-else-if="msg.sender === 'user'" class="msg-wrapper msg-wrapper--user">
            <div class="msg-meta msg-meta--right">
              <span class="msg-time">{{ formatTime(msg.timestamp) }}</span>
              <span class="msg-role-name">我</span>
            </div>
            <div class="msg-bubble msg-bubble--user"><div class="msg-content">{{ msg.content }}</div></div>
          </div>
          <!-- Agent -->
          <div v-else class="msg-wrapper msg-wrapper--agent">
            <Avatar :style="{ backgroundColor: roleColor(msg.agent || ''), flexShrink: 0 }" :size="36">
              {{ roleName(msg.agent || '').charAt(0) }}
            </Avatar>
            <div class="msg-body">
              <div class="msg-meta">
                <Tag :color="roleColor(msg.agent || '')" size="small">{{ roleName(msg.agent || '') }}</Tag>
                <span class="msg-time">{{ formatTime(msg.timestamp) }}</span>
              </div>
              <div class="msg-bubble msg-bubble--agent" :style="{ borderLeftColor: roleColor(msg.agent || '') }">
                <div class="msg-content">{{ msg.content }}</div>
              </div>
            </div>
          </div>
        </div>

        <!-- Thinking -->
        <div v-if="thinkingAgent" class="chat-row">
          <div class="msg-wrapper msg-wrapper--agent">
            <Avatar :style="{ backgroundColor: roleColor(thinkingAgent), flexShrink: 0 }" :size="36">
              {{ roleName(thinkingAgent).charAt(0) }}
            </Avatar>
            <div class="msg-body">
              <Tag :color="roleColor(thinkingAgent)" size="small">{{ roleName(thinkingAgent) }}</Tag>
              <div class="thinking-indicator"><span class="dot" /><span class="dot" /><span class="dot" /></div>
            </div>
          </div>
        </div>
      </div>

      <div class="chat-input">
        <InputTextArea v-model:value="inputMessage"
          :disabled="!canInput && sessionStatus !== 'none'"
          :placeholder="sessionStatus === 'none' ? '输入您的需求以开始对话...' : sessionStatus === 'awaiting_input' ? '请输入您的回复...' : $t('openstaff.agentChat.inputPlaceholder')"
          :rows="2" auto-size class="chat-textarea" @keydown="handleKeydown" />
        <Button type="primary" :disabled="!inputMessage.trim() || sending" :loading="sending" class="send-btn" @click="handleSend">
          {{ sessionStatus === 'none' ? '开始对话' : $t('openstaff.agentChat.sendMessage') }}
        </Button>
      </div>
    </div>

    <!-- Agent status drawer -->
    <Drawer v-model:open="agentDrawerOpen" title="智能体状态" placement="right" :width="320">
      <Empty v-if="agentStatuses.length === 0" description="该项目尚未配置员工，请在项目设置中添加" />
      <div v-else class="agent-list">
        <div v-for="agent in agentStatuses" :key="agent.role" class="agent-item">
          <Avatar :style="{ backgroundColor: roleColor(agent.role) }" :size="32">{{ roleName(agent.role).charAt(0) }}</Avatar>
          <div class="agent-info">
            <span class="agent-name">{{ roleName(agent.role) }}</span>
            <span class="agent-role-type">{{ agent.role }}</span>
          </div>
          <Badge :status="agent.state === 'idle' ? 'default' : agent.state === 'thinking' ? 'processing' : 'success'"
            :text="AGENT_STATES[agent.state]" />
        </div>
      </div>
    </Drawer>

    <Drawer v-model:open="taskDrawerOpen" title="任务进度" placement="right" :width="360">
      <Empty description="任务信息将在架构者分解后显示" />
    </Drawer>

    <Drawer v-model:open="statsDrawerOpen" title="会话统计" placement="right" :width="320">
      <div class="stats-grid">
        <div class="stat-card"><span class="stat-label">消息数</span><span class="stat-value">{{ messages.length }}</span></div>
        <div class="stat-card"><span class="stat-label">事件数</span><span class="stat-value">{{ tokenUsage.events }}</span></div>
        <div class="stat-card"><span class="stat-label">会话状态</span>
          <Tag :color="sessionStatus === 'active' ? 'blue' : sessionStatus === 'completed' ? 'green' : sessionStatus === 'error' ? 'red' : 'default'">{{ sessionStatus }}</Tag>
        </div>
        <div class="stat-card"><span class="stat-label">连接状态</span>
          <Badge :status="connected ? 'success' : 'error'" :text="connected ? '已连接' : '未连接'" />
        </div>
      </div>
    </Drawer>
  </Page>
</template>

<style scoped>
.chat-wrapper { display: flex; flex-direction: column; height: calc(100vh - 160px); background: hsl(var(--background-deep)); border-radius: 8px; overflow: hidden; }
.awaiting-banner { padding: 8px 16px; text-align: center; background: hsl(var(--warning) / 0.15); color: hsl(var(--warning)); font-size: 14px; border-bottom: 1px solid hsl(var(--warning) / 0.3); }
.connection-bar { padding: 4px 16px; text-align: center; background: hsl(var(--destructive) / 0.1); color: hsl(var(--destructive)); font-size: 12px; }
.chat-messages { flex: 1; overflow-y: auto; padding: 16px; scroll-behavior: smooth; }
.load-more { text-align: center; margin-bottom: 12px; }
.empty-state { display: flex; justify-content: center; align-items: center; height: 100%; }

.chat-row { margin-bottom: 16px; }
.chat-row--right { display: flex; justify-content: flex-end; }
.chat-row--center { display: flex; justify-content: center; }

.system-event { display: inline-flex; flex-direction: column; align-items: center; padding: 4px 12px; background: hsl(var(--muted)); border-radius: 12px; max-width: 80%; }
.system-text { font-size: 12px; color: hsl(var(--muted-foreground)); }
.system-time { font-size: 10px; color: hsl(var(--muted-foreground)); margin-top: 2px; }

.msg-wrapper { display: flex; max-width: 75%; }
.msg-wrapper--user { flex-direction: column; align-items: flex-end; }
.msg-wrapper--agent { gap: 8px; align-items: flex-start; }
.msg-body { display: flex; flex-direction: column; gap: 4px; min-width: 0; }
.msg-meta { display: flex; align-items: center; gap: 6px; }
.msg-meta--right { justify-content: flex-end; margin-bottom: 4px; }
.msg-role-name { font-size: 12px; color: hsl(var(--foreground)); font-weight: 500; }
.msg-time { font-size: 11px; color: hsl(var(--muted-foreground)); }

.msg-bubble { padding: 10px 14px; border-radius: 12px; word-break: break-word; }
.msg-bubble--user { background: hsl(var(--primary)); color: hsl(var(--primary-foreground)); border-bottom-right-radius: 4px; }
.msg-bubble--agent { background: hsl(var(--card)); border: 1px solid hsl(var(--border)); border-left: 3px solid hsl(var(--muted-foreground)); border-bottom-left-radius: 4px; }
.msg-content { white-space: pre-wrap; font-size: 14px; line-height: 1.6; }

.thinking-indicator { display: inline-flex; gap: 4px; padding: 10px 14px; background: hsl(var(--card)); border-radius: 12px; border: 1px solid hsl(var(--border)); }
.thinking-indicator .dot { width: 8px; height: 8px; border-radius: 50%; background: hsl(var(--muted-foreground)); animation: blink 1.4s infinite both; }
.thinking-indicator .dot:nth-child(2) { animation-delay: 0.2s; }
.thinking-indicator .dot:nth-child(3) { animation-delay: 0.4s; }
@keyframes blink { 0%, 80%, 100% { opacity: 0.3; } 40% { opacity: 1; } }

.chat-input { display: flex; gap: 8px; padding: 12px 16px; background: hsl(var(--card)); border-top: 1px solid hsl(var(--border)); align-items: flex-end; }
.chat-textarea { flex: 1; }
.send-btn { flex-shrink: 0; height: 40px; }

.agent-list { display: flex; flex-direction: column; gap: 12px; }
.agent-item { display: flex; align-items: center; gap: 10px; padding: 8px; border-radius: 8px; background: hsl(var(--accent)); }
.agent-info { flex: 1; display: flex; flex-direction: column; }
.agent-name { font-size: 14px; font-weight: 500; }
.agent-role-type { font-size: 11px; color: hsl(var(--muted-foreground)); }

.stats-grid { display: flex; flex-direction: column; gap: 12px; }
.stat-card { display: flex; justify-content: space-between; align-items: center; padding: 12px; background: hsl(var(--accent)); border-radius: 8px; }
.stat-label { font-size: 13px; color: hsl(var(--muted-foreground)); }
.stat-value { font-size: 18px; font-weight: 600; color: hsl(var(--foreground)); }
</style>
