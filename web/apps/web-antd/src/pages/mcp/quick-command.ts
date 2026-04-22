export type McpQuickCommandParseResult = {
  args: string[];
  command: string;
  npmPackage?: string;
  pypiPackage?: string;
  suggestedName: string;
  templateJson: string;
};

export function parseMcpQuickCommand(input: string): McpQuickCommandParseResult {
  const tokens = tokenizeCommand(input.trim());
  if (tokens.length === 0) {
    throw new Error('EMPTY_COMMAND');
  }

  const command = tokens[0] || '';
  const args = tokens.slice(1);
  const normalizedCommand = normalizeExecutable(command);
  const packageToken = findPrimaryPackageToken(normalizedCommand, args);
  const npmPackage = normalizedCommand === 'npx' ? normalizeNpmPackage(packageToken) : undefined;
  const pypiPackage = normalizedCommand === 'uvx' ? normalizePypiPackage(packageToken) : undefined;
  const suggestedIdentity = npmPackage || pypiPackage || packageToken || command;

  return {
    args,
    command,
    npmPackage,
    pypiPackage,
    suggestedName: buildSuggestedName(suggestedIdentity),
    templateJson: JSON.stringify({
      schema: 'openstaff.mcp-template.v1',
      template_id: `custom.${buildSuggestedName(suggestedIdentity)}`,
      key: buildSuggestedName(suggestedIdentity),
      display_name: buildSuggestedName(suggestedIdentity),
      category: 'general',
      source: 'custom-quick-command',
      match_hints: {
        name: buildSuggestedName(suggestedIdentity),
        npm_package: npmPackage ?? null,
        pypi_package: pypiPackage ?? null,
      },
      profiles: [
        {
          id: normalizedCommand === 'npx' ? 'package-npm' : 'package-python',
          profile_type: 'package',
          transport_type: 'stdio',
          runner_kind: 'package',
          runner: command,
          ecosystem: normalizedCommand === 'npx' ? 'npm' : 'python',
          package_name: npmPackage ?? pypiPackage ?? packageToken ?? '',
          package_version: 'latest',
          command,
          args_template: args,
          env_template: {},
        },
      ],
      parameter_schema: [],
    }, null, 2),
  };
}

function tokenizeCommand(input: string) {
  const tokens: string[] = [];
  let current = '';
  let quote: '"' | "'" | null = null;
  let escaping = false;

  for (const character of input) {
    if (escaping) {
      current += character;
      escaping = false;
      continue;
    }

    if (character === '\\') {
      escaping = true;
      continue;
    }

    if (quote) {
      if (character === quote) {
        quote = null;
      } else {
        current += character;
      }
      continue;
    }

    if (character === '"' || character === "'") {
      quote = character;
      continue;
    }

    if (/\s/.test(character)) {
      if (current) {
        tokens.push(current);
        current = '';
      }
      continue;
    }

    current += character;
  }

  if (escaping) {
    current += '\\';
  }

  if (current) {
    tokens.push(current);
  }

  return tokens;
}

function normalizeExecutable(command: string) {
  return command.trim().toLowerCase().replace(/\.cmd$/u, '');
}

function findPrimaryPackageToken(command: string, args: string[]) {
  if (args.length === 0) {
    return undefined;
  }

  const tokens = [...args];
  for (let index = 0; index < tokens.length; index += 1) {
    const token = tokens[index];
    if (!token) {
      continue;
    }

    if (!token.startsWith('-')) {
      return token;
    }

    if (
      (command === 'npx' && (token === '--package' || token === '-p'))
      || (command === 'uvx' && (token === '--from' || token === '--python'))
    ) {
      index += 1;
    }
  }

  return undefined;
}

function normalizeNpmPackage(token?: string) {
  if (!token) {
    return undefined;
  }

  const trimmed = token.trim();
  if (!trimmed) {
    return undefined;
  }

  const lastSlashIndex = trimmed.lastIndexOf('/');
  const lastAtIndex = trimmed.lastIndexOf('@');

  if (trimmed.startsWith('@')) {
    return lastAtIndex > lastSlashIndex ? trimmed.slice(0, lastAtIndex) : trimmed;
  }

  return lastAtIndex > 0 ? trimmed.slice(0, lastAtIndex) : trimmed;
}

function normalizePypiPackage(token?: string) {
  if (!token) {
    return undefined;
  }

  const trimmed = token.trim();
  if (!trimmed) {
    return undefined;
  }

  return trimmed
    .replace(/\[.*$/u, '')
    .split(/[<>=!~]/u)[0]
    ?.trim();
}

function buildSuggestedName(identity: string) {
  return identity
    .replace(/^@/u, '')
    .replace(/[/:]/gu, '-')
    .replace(/[^a-zA-Z0-9._-]+/gu, '-')
    .replace(/-+/gu, '-')
    .replace(/^-|-$/gu, '');
}
