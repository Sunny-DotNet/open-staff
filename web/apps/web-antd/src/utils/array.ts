/** 切换数组中的元素（存在则移除，不存在则添加），返回新数组 */
export function toggleArrayItem<T>(arr: T[] | undefined, item: T): T[] {
  const list = [...(arr ?? [])];
  const idx = list.indexOf(item);
  if (idx >= 0) {
    list.splice(idx, 1);
  } else {
    list.push(item);
  }
  return list;
}
