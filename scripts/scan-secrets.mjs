#!/usr/bin/env node
// Lightweight, dependency-free secret scan over the staged diff. Not a replacement for a
// dedicated tool (gitleaks/trufflehog) if the project later wants deeper scanning - this is
// the zero-install baseline that catches the common, expensive mistakes.

import { execSync } from 'node:child_process';

const patterns = [
  { name: 'AWS Access Key ID', re: /AKIA[0-9A-Z]{16}/ },
  { name: 'AWS Secret Access Key (assignment)', re: /aws_secret_access_key\s*=\s*['"][A-Za-z0-9/+=]{40}['"]/i },
  { name: 'Private key block', re: /-----BEGIN (RSA |EC |OPENSSH |DSA |PGP )?PRIVATE KEY-----/ },
  { name: 'GitHub token', re: /gh[pousr]_[A-Za-z0-9]{36,}/ },
  { name: 'Slack token', re: /xox[baprs]-[A-Za-z0-9-]{10,}/ },
  { name: 'Stripe key', re: /sk_(live|test)_[A-Za-z0-9]{16,}/ },
  // (?!test-secret) excludes this repo's established fake-signing-secret test fixture literal
  // (e.g. "test-secret-at-least-32-bytes-long-for-hmac-sha256", reused verbatim across test
  // files) - self-documenting by name, not a real credential, but every new test file that
  // uses it would otherwise re-trigger this scanner since it only checks added diff lines.
  { name: 'Generic API key / secret assignment', re: /\b(api[_-]?key|secret|password|passwd|token)\b\s*[:=]\s*['"](?!test-secret)[A-Za-z0-9_\-/+=]{16,}['"]/i },
  { name: 'JWT-looking literal', re: /eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}/ },
];

const allowlistPathFragments = [
  '.env.example',
  'frontend/.env.development', // no secret - just a localhost API URL, mirrors .env.example's role
  'frontend/.env.tauri', // no secret - just the production API's public URL for the desktop build
  'scan-secrets.mjs', // this file necessarily contains the pattern text above
];

function stagedFiles() {
  const out = execSync('git diff --cached --name-only --diff-filter=ACM', { encoding: 'utf8' });
  return out.split('\n').filter(Boolean);
}

function diffForFile(path) {
  // Only scan *added* lines so pre-existing (already-reviewed) content already in history
  // doesn't re-trigger on unrelated edits to the same file.
  return execSync(`git diff --cached -U0 -- "${path}"`, { encoding: 'utf8' });
}

let found = [];

for (const file of stagedFiles()) {
  if (allowlistPathFragments.some((f) => file.endsWith(f))) continue;
  if (file === '.env' || file.match(/(^|\/)\.env(\.[a-z]+)?$/i)) {
    if (!file.endsWith('.env.example')) {
      found.push({ file, name: 'Committing a .env file', line: '(whole file)' });
      continue;
    }
  }

  let diff;
  try {
    diff = diffForFile(file);
  } catch {
    continue; // binary or unreadable diff - skip rather than false-positive
  }

  const addedLines = diff.split('\n').filter((l) => l.startsWith('+') && !l.startsWith('+++'));
  for (const line of addedLines) {
    for (const { name, re } of patterns) {
      if (re.test(line)) {
        found.push({ file, name, line: line.slice(0, 120) });
      }
    }
  }
}

if (found.length > 0) {
  console.error('\n[31m✖ Possible secret(s) detected in staged changes:[0m\n');
  for (const f of found) {
    console.error(`  ${f.file}\n    ${f.name}\n    ${f.line}\n`);
  }
  console.error(
    'If this is a false positive, adjust scripts/scan-secrets.mjs. Otherwise remove the secret,\n' +
      'rotate it if it was ever pushed, and use an environment variable / .env (git-ignored) instead.\n',
  );
  process.exit(1);
}

process.exit(0);
