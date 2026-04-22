import { t } from '@/i18n';
import jobTitleKeys from '@/pages/agent-roles/jobTitle.json';

const aliasToKey = new Map<string, string>([
  ['secretary', 'secretary'],
  ['项目秘书', 'secretary'],
  ['秘书', 'secretary'],
  ['Secretary', 'secretary'],
  ['architect', 'architect'],
  ['架构师', 'architect'],
  ['Architect', 'architect'],
  ['producer', 'producer'],
  ['开发工程师', 'producer'],
  ['Producer', 'producer'],
  ['builder', 'builder'],
  ['构建工程师', 'builder'],
  ['Builder', 'builder'],
  ['software_engineer', 'software_engineer'],
  ['软件工程师', 'software_engineer'],
  ['Software Engineer', 'software_engineer'],
  ['backend_engineer', 'backend_engineer'],
  ['后端工程师', 'backend_engineer'],
  ['Backend Engineer', 'backend_engineer'],
  ['code_reviewer', 'code_reviewer'],
  ['代码审查员', 'code_reviewer'],
  ['Code Reviewer', 'code_reviewer'],
  ['designer', 'designer'],
  ['美工', 'designer'],
  ['Designer', 'designer'],
]);

export function normalizeJobTitleKey(value?: null | string) {
  if (!value) {
    return undefined;
  }

  const trimmed = value.trim();
  if (!trimmed) {
    return undefined;
  }

  const aliased = aliasToKey.get(trimmed);
  if (aliased) {
    return aliased;
  }

  const normalized = trimmed.replaceAll('-', '_').replaceAll(' ', '_').toLowerCase();
  return aliasToKey.get(normalized) || normalized;
}

export function localizeJobTitle(value?: null | string, fallback?: null | string) {
  const normalized = normalizeJobTitleKey(value);
  if (!normalized) {
    return fallback?.trim() || undefined;
  }

  if (!/^[a-z0-9_]+$/.test(normalized)) {
    return fallback?.trim() || normalized;
  }

  const translationKey = `role.jobTitleCatalog.${normalized}`;
  const translated = t(translationKey);
  if (translated !== translationKey) {
    return translated;
  }

  return fallback?.trim() || humanizeJobTitle(normalized);
}

export function getJobTitleOptions(currentValue?: null | string) {
  const values = [...jobTitleKeys];
  const normalizedCurrent = normalizeJobTitleKey(currentValue);
  if (normalizedCurrent && !values.includes(normalizedCurrent)) {
    values.push(normalizedCurrent);
  }

  return values.map((value) => ({
    label: localizeJobTitle(value, value) || value,
    value,
  }));
}

function humanizeJobTitle(value: string) {
  return value
    .split('_')
    .filter(Boolean)
    .map((segment) => segment.charAt(0).toUpperCase() + segment.slice(1))
    .join(' ');
}
