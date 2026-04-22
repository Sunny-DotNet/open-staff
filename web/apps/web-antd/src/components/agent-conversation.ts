export interface AgentConversationUsage {
  inputTokens?: number;
  outputTokens?: number;
  totalTokens?: number;
}

export interface AgentConversationTiming {
  totalMs?: number;
  firstTokenMs?: number;
}

export interface AgentConversationToolCall {
  toolCallId?: string;
  name: string;
  arguments?: Record<string, unknown>;
  result?: string;
  error?: string;
  status: 'calling' | 'done' | 'error';
}

export interface AgentConversationThinkingStep {
  id: string;
  kind: 'thinking';
  content: string;
  streaming?: boolean;
}

export interface AgentConversationToolStep extends AgentConversationToolCall {
  id: string;
  kind: 'tool_call';
}

export interface AgentConversationResponseStep {
  id: string;
  kind: 'response';
  content: string;
  streaming?: boolean;
  appearance?: 'default' | 'error';
}

export type AgentConversationStep =
  | AgentConversationThinkingStep
  | AgentConversationToolStep
  | AgentConversationResponseStep;

export interface AgentConversationMessage {
  id?: string;
  role: 'assistant' | 'user';
  content: string;
  streaming?: boolean;
  thinking?: string;
  thinkingStreaming?: boolean;
  usage?: AgentConversationUsage;
  timing?: AgentConversationTiming;
  model?: string;
  toolCalls?: AgentConversationToolCall[];
  steps?: AgentConversationStep[];
  displayName?: string;
  avatar?: null | string;
  timestamp?: string;
}
