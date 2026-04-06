/** 格式化日期为中文日期字符串 */
export function formatDate(
  dateStr: string,
  options: Intl.DateTimeFormatOptions = {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  },
): string {
  if (!dateStr) return '';
  return new Date(dateStr).toLocaleDateString('zh-CN', options);
}

/** 格式化日期时间为中文时间字符串 */
export function formatDateTime(dateStr: string): string {
  if (!dateStr) return '';
  return new Date(dateStr).toLocaleString('zh-CN', { hour12: false });
}

/** 格式化为时间（时:分:秒） */
export function formatTime(dateStr: string): string {
  if (!dateStr) return '';
  return new Date(dateStr).toLocaleTimeString('zh-CN', { hour12: false });
}
