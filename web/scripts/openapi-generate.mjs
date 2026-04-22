import { mkdir, readFile, rm, writeFile, cp } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { spawn } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { createClient } from '@hey-api/openapi-ts';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const webRoot = path.resolve(__dirname, '..');
const repoRoot = path.resolve(webRoot, '..');
const packageRoot = path.join(webRoot, 'packages', 'openstaff-api');
const srcRoot = path.join(packageRoot, 'src');
const dtosRoot = path.join(srcRoot, 'dtos');
const servicesRoot = path.join(srcRoot, 'services');
const tempRoot = path.join(packageRoot, '.openapi-temp');
const tempSpecPath = path.join(tempRoot, 'openapi.json');
const tempOutputPath = path.join(tempRoot, 'client');
const hostProjectPath = path.join(repoRoot, 'src', 'hosts', 'OpenStaff.HttpApi.Host');
const openApiBaseUrl = 'http://127.0.0.1:5079';
const openApiUrl = `${openApiBaseUrl}/openapi/v1.json`;

async function main() {
  await mkdir(tempRoot, { recursive: true });

  let hostProcess;
  try {
    const spec = await loadOpenApiSpec();
    await writeSpec(spec);
    await createClient({
      input: tempSpecPath,
      output: tempOutputPath,
    });
    await materializeGeneratedPackage();
  } finally {
    if (hostProcess) {
      await stopHost(hostProcess);
    }
    await rm(tempRoot, { force: true, recursive: true });
  }

  async function loadOpenApiSpec() {
    const existing = await tryFetchSpec();
    if (existing) {
      return existing;
    }

    hostProcess = startHost();
    return await waitForSpec(hostProcess);
  }
}

function startHost() {
  let output = '';
  const child = spawn(
    'dotnet',
    [
      'run',
      '--project',
      hostProjectPath,
      '--urls',
      openApiBaseUrl,
    ],
    {
      cwd: repoRoot,
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: 'Development',
        DOTNET_ENVIRONMENT: 'Development',
      },
      windowsHide: true,
      stdio: ['ignore', 'pipe', 'pipe'],
    },
  );

  child.stdout?.on('data', (chunk) => {
    output += chunk.toString();
  });
  child.stderr?.on('data', (chunk) => {
    output += chunk.toString();
  });

  child.getBufferedOutput = () => output;
  return child;
}

async function stopHost(hostProcess) {
  hostProcess.kill('SIGINT');
  try {
    await new Promise((resolve) => {
      if (hostProcess.exitCode !== null) {
        resolve();
        return;
      }

      const timeout = setTimeout(() => {
        hostProcess.kill('SIGTERM');
      }, 5000);

      hostProcess.once('exit', () => {
        clearTimeout(timeout);
        resolve();
      });
    });
  } catch {
    // Ignore shutdown failures from the temporary codegen host.
  }
}

async function waitForSpec(hostProcess) {
  for (let attempt = 0; attempt < 60; attempt += 1) {
    const spec = await tryFetchSpec();
    if (spec) {
      return spec;
    }

    if (hostProcess.exitCode !== null) {
      throw new Error(
        [
          'Failed to start OpenStaff.HttpApi.Host for OpenAPI generation.',
          hostProcess.getBufferedOutput?.() ?? '',
        ].join('\n'),
      );
    }

    await sleep(1000);
  }

  throw new Error('Timed out waiting for the OpenAPI document.');
}

async function tryFetchSpec() {
  try {
    const response = await fetch(openApiUrl);
    if (!response.ok) {
      return null;
    }

    const spec = await response.json();
    if (!spec || typeof spec !== 'object' || !spec.paths) {
      return null;
    }

    return spec;
  } catch {
    return null;
  }
}

async function writeSpec(spec) {
  const normalized = {
    ...spec,
    servers: [{ url: '/' }],
  };

  await writeFile(tempSpecPath, `${JSON.stringify(normalized, null, 2)}\n`, 'utf8');
}

async function materializeGeneratedPackage() {
  await mkdir(dtosRoot, { recursive: true });
  await mkdir(servicesRoot, { recursive: true });

  await cleanupGeneratedTargets();
  await copyFile(
    path.join(tempOutputPath, 'types.gen.ts'),
    path.join(dtosRoot, 'types.gen.ts'),
  );
  await copyFile(
    path.join(tempOutputPath, 'client.gen.ts'),
    path.join(servicesRoot, 'client.gen.ts'),
    rewriteRootGeneratedImports,
  );
  await copyFile(
    path.join(tempOutputPath, 'sdk.gen.ts'),
    path.join(servicesRoot, 'sdk.gen.ts'),
    rewriteRootGeneratedImports,
  );

  await cp(
    path.join(tempOutputPath, 'client'),
    path.join(servicesRoot, 'client'),
    { force: true, recursive: true },
  );
  await cp(
    path.join(tempOutputPath, 'core'),
    path.join(servicesRoot, 'core'),
    { force: true, recursive: true },
  );
}

async function cleanupGeneratedTargets() {
  const targets = [
    path.join(dtosRoot, 'types.gen.ts'),
    path.join(servicesRoot, 'client.gen.ts'),
    path.join(servicesRoot, 'sdk.gen.ts'),
    path.join(servicesRoot, 'client'),
    path.join(servicesRoot, 'core'),
  ];

  await Promise.all(targets.map((target) => rm(target, { force: true, recursive: true })));
}

async function copyFile(sourcePath, targetPath, transform) {
  let content = await readFile(sourcePath, 'utf8');
  if (transform) {
    content = transform(content);
  }

  await writeFile(targetPath, content, 'utf8');
}

function rewriteRootGeneratedImports(content) {
  return content.replaceAll("from './types.gen'", "from '../dtos/types.gen'");
}

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

main().catch(async (error) => {
  const message =
    error instanceof Error
      ? error.stack ?? error.message
      : String(error);
  console.error(message);

  if (existsSync(tempRoot)) {
    await rm(tempRoot, { force: true, recursive: true });
  }

  process.exitCode = 1;
});
