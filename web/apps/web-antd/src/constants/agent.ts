/** 角色类型 → 颜色映射 */
export const ROLE_COLORS: Record<string, string> = {
  communicator: '#1890ff',
  decision_maker: '#722ed1',
  architect: '#13c2c2',
  producer: '#52c41a',
  debugger: '#fa8c16',
  orchestrator: '#faad14',
  image_creator: '#eb2f96',
  video_creator: '#f5222d',
};

/** 角色类型 → 图标 emoji 映射 */
export const ROLE_ICONS: Record<string, string> = {
  orchestrator: '🎯',
  communicator: '💬',
  decision_maker: '🧠',
  architect: '📐',
  producer: '⚙️',
  debugger: '🔍',
  image_creator: '🎨',
  video_creator: '🎬',
};

/** 角色类型 → 中文名称映射 */
export const ROLE_NAMES: Record<string, string> = {
  communicator: '对话者',
  decision_maker: '决策者',
  architect: '架构者',
  producer: '生产者',
  debugger: '调试者',
  image_creator: '图片创造者',
  video_creator: '视频创造者',
  orchestrator: '调度者',
};

/** 智能体状态 → 中文标签 */
export const AGENT_STATES: Record<string, string> = {
  idle: '空闲',
  thinking: '思考中',
  routing: '路由中',
  working: '工作中',
};

/** 灵魂配置可选值 */
export const TRAIT_OPTIONS = [
  '严谨',
  '幽默',
  '友善',
  '直率',
  '耐心',
  '果断',
  '细腻',
  '冷静',
];

export const STYLE_OPTIONS = ['正式', '轻松', '技术流', '导师型', '鼓励型'];

export const ATTITUDE_OPTIONS = [
  '追求完美',
  '高效优先',
  '注重细节',
  '创新思维',
  '团队协作',
];

export function getRoleIcon(roleType: string): string {
  return ROLE_ICONS[roleType] || '🤖';
}

export function getRoleColor(roleType: string): string {
  return ROLE_COLORS[roleType] || '#8c8c8c';
}
