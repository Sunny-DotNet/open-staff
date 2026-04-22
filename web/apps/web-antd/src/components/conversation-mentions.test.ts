import { describe, expect, it } from 'vitest';

import {
  appendMentionShortcut,
  filterMentionOptions,
  insertMentionValue,
  resolveMentionRange,
} from './conversation-mentions';

describe('conversation-mentions', () => {
  it('matches project members by the text after @', () => {
    const options = [
      {
        key: 'monica',
        label: 'Monica',
        value: 'Monica',
      },
      {
        key: 'jennifer',
        keywords: ['软件工程师'],
        label: 'Jennifer',
        value: 'Jennifer',
      },
    ];

    expect(filterMentionOptions(options, 'jen')).toEqual([options[1]]);
    expect(filterMentionOptions(options, '软件')).toEqual([options[1]]);
    expect(filterMentionOptions(options, '')).toEqual(options);
  });

  it('detects the active mention around the caret', () => {
    expect(resolveMentionRange('@Jen 开工', 4)).toEqual({
      end: 4,
      query: 'Jen',
      start: 0,
    });

    expect(resolveMentionRange('先看一下 @Monica', 9)).toEqual({
      end: 9,
      query: 'Mon',
      start: 5,
    });

    expect(resolveMentionRange('邮箱a@b.com', 9)).toBeNull();
    expect(resolveMentionRange('@Monica 开工', 8)).toBeNull();
  });

  it('replaces the current @ token with the selected member', () => {
    const range = resolveMentionRange('@Jen 开工', 4);
    expect(range).not.toBeNull();

    const inserted = insertMentionValue('@Jen 开工', range!, {
      key: 'jennifer',
      label: 'Jennifer',
      value: 'Jennifer',
    });

    expect(inserted).toEqual({
      cursorPosition: 9,
      value: '@Jennifer 开工',
    });
  });

  it('appends a mention shortcut from avatar clicks', () => {
    expect(appendMentionShortcut('', 'Monica')).toEqual({
      cursorPosition: 8,
      value: '@Monica ',
    });

    expect(appendMentionShortcut('先看看', 'Jennifer')).toEqual({
      cursorPosition: 14,
      value: '先看看 @Jennifer ',
    });
  });
});
