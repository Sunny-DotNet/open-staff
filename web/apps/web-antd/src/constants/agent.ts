/** 智能体来源 */
export enum AgentSource {
  Custom = 0,
  Builtin = 1,
  Remote = 2,
  Vendor = 3,
}

/** 来源标签 */
export const SOURCE_LABELS: Record<number, string> = {
  [AgentSource.Custom]: '自定义',
  [AgentSource.Builtin]: '内置',
  [AgentSource.Remote]: '远程',
  [AgentSource.Vendor]: '厂商',
};

/** 角色类型 → 颜色映射 */
export const ROLE_COLORS: Record<string, string> = {
  secretary: '#1890ff',
};

/** 角色类型 → 图标 emoji 映射 */
export const ROLE_ICONS: Record<string, string> = {
  secretary: '📋',
};

/** 角色类型 → 中文名称映射 */
export const ROLE_NAMES: Record<string, string> = {
  secretary: '秘书',
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
