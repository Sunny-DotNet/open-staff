/** 协议颜色映射 */
export const PROTOCOL_COLORS: Record<string, string> = {
  openai: '#10a37f',
  anthropic: '#d97706',
  google: '#4285f4',
  'github-copilot': '#6e40c9',
  newapi: '#1890ff',
};

export function getProtocolColor(key: string): string {
  return PROTOCOL_COLORS[key] ?? '#8c8c8c';
}

/** 将后端 Logo 字段转为 @lobehub/icons-static-svg CDN URL */
export function getLogoUrl(logo: string): string {
  if (!logo) return '';
  // PascalCase.Variant → lowercase-variant, e.g. "Claude.Color" → "claude-color"
  const slug = logo
    .replace(/\.([A-Za-z])/g, '-$1')
    .replace(/([a-z])([A-Z])/g, '$1$2')
    .toLowerCase();
  return `https://unpkg.com/@lobehub/icons-static-svg@latest/icons/${slug}.svg`;
}
