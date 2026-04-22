export interface ConversationMentionOption {
  avatar?: string;
  description?: string;
  key: string;
  keywords?: string[];
  label: string;
  value?: string;
}

export interface ConversationMentionRange {
  end: number;
  query: string;
  start: number;
}

export function normalizeMentionSearchKey(value?: null | string) {
  return value?.trim().toLowerCase() ?? '';
}

export function filterMentionOptions(
  options: ConversationMentionOption[],
  query: string,
) {
  const normalizedQuery = normalizeMentionSearchKey(query);
  if (!normalizedQuery) {
    return options;
  }

  return options.filter((option) =>
    [
      option.label,
      option.value,
      ...(option.keywords ?? []),
    ]
      .map((value) => normalizeMentionSearchKey(value))
      .filter(Boolean)
      .some((value) => value.includes(normalizedQuery)),
  );
}

export function resolveMentionRange(value: string, caret: number): ConversationMentionRange | null {
  if (!value || caret <= 0) {
    return null;
  }

  let triggerIndex = value.lastIndexOf('@', Math.max(caret - 1, 0));
  while (triggerIndex >= 0) {
    const prefixCharacter = triggerIndex > 0 ? value[triggerIndex - 1] : '';
    if (!prefixCharacter || /\s|[({\[<"'“‘，。,！!？?；;：:]/.test(prefixCharacter)) {
      break;
    }

    triggerIndex = value.lastIndexOf('@', triggerIndex - 1);
  }

  if (triggerIndex < 0) {
    return null;
  }

  const query = value.slice(triggerIndex + 1, caret);
  if (/[\s@]/.test(query)) {
    return null;
  }

  return {
    end: caret,
    query,
    start: triggerIndex,
  };
}

export function insertMentionValue(
  value: string,
  range: ConversationMentionRange,
  option: ConversationMentionOption,
) {
  const mentionText = `@${option.value || option.label}`;
  const before = value.slice(0, range.start);
  const after = value.slice(range.end);
  const spacer = after.length === 0 ? ' ' : (/^\s/.test(after) ? '' : ' ');

  return {
    cursorPosition: (before + mentionText + spacer).length,
    value: `${before}${mentionText}${spacer}${after}`,
  };
}

export function appendMentionShortcut(value: string, label: string) {
  const trimmedLabel = label.trim();
  if (!trimmedLabel) {
    return {
      cursorPosition: value.length,
      value,
    };
  }

  const mentionText = `@${trimmedLabel}`;
  const spacer = value.length === 0 || /\s$/.test(value) ? '' : ' ';
  const nextValue = `${value}${spacer}${mentionText} `;

  return {
    cursorPosition: nextValue.length,
    value: nextValue,
  };
}
