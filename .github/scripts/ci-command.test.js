const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { spawnSync } = require('node:child_process');
const { sanitize, tail, LIMIT } = require('./ci-command.js');
const runner = path.join(__dirname, 'ci-command.js');

test('diagnostic output removes control sequences and credentials', () => {
  const clean = sanitize('\x1b[31mfailure\x1b[0m\x00\nAuthorization: Bearer abc123\n?token=private&x=1\nknown-value\nghp_example',
    { SOME_SECRET: 'known-value' });
  assert.match(clean, /failure/);
  for (const value of ['\x1b', '\x00', 'abc123', 'private', 'known-value', 'ghp_example']) assert.ok(!clean.includes(value));
});

test('bounded capture drops partial first lines and retains final evidence', () => {
  const output = tail('x'.repeat(LIMIT * 2) + '\nfinal failure\n');
  assert.match(output, /earlier output omitted/);
  assert.match(output, /final failure/);
  assert.ok(Buffer.byteLength(output) <= LIMIT + 32);
  assert.ok(!output.includes('xxx'));
});

for (const code of [0, 7]) {
  test(`command exit ${code} is preserved and both output streams are retained`, () => {
    const cwd = fs.mkdtempSync(path.join(os.tmpdir(), 'scout-ci-command-'));
    try {
      fs.writeFileSync(path.join(cwd, 'global.json'), JSON.stringify({ sdk: { version: '10.0.403' } }));
      const summary = path.join(cwd, 'summary.md');
      const result = spawnSync(process.execPath, [runner, 'managed-tests', process.execPath, '-e',
        `console.log('test output'); console.error('test failure fixture-secret'); process.exit(${code});`],
      { cwd, env: { ...process.env, FIXTURE_SECRET: 'fixture-secret', GITHUB_STEP_SUMMARY: summary }, encoding: 'utf8' });
      assert.equal(result.status, code, result.stderr);
      const log = fs.readFileSync(path.join(cwd, 'obj/ci-diagnostics/managed-tests.log'), 'utf8');
      assert.match(log, /test output/);
      assert.match(log, /test failure \[REDACTED\]/);
      assert.match(log, new RegExp(`Exit code: ${code}`));
      assert.equal(fs.existsSync(summary), code !== 0);
    } finally { fs.rmSync(cwd, { recursive: true, force: true }); }
  });
}

test('reporting failure cannot replace the original failing exit code', () => {
  const cwd = fs.mkdtempSync(path.join(os.tmpdir(), 'scout-ci-report-failure-'));
  try {
    fs.writeFileSync(path.join(cwd, 'obj'), 'not a directory');
    const result = spawnSync(process.execPath, [runner, 'native-smoke', process.execPath, '-e', 'process.exit(19)'],
      { cwd, encoding: 'utf8' });
    assert.equal(result.status, 19);
    assert.match(result.stderr, /Diagnostic capture failed/);
  } finally { fs.rmSync(cwd, { recursive: true, force: true }); }
});

test('missing executable fails and retains prerequisite evidence', () => {
  const cwd = fs.mkdtempSync(path.join(os.tmpdir(), 'scout-ci-missing-'));
  try {
    fs.writeFileSync(path.join(cwd, 'global.json'), JSON.stringify({ sdk: { version: '10.0.403' } }));
    const result = spawnSync(process.execPath, [runner, 'managed-tests', 'scout-command-that-does-not-exist'], { cwd, encoding: 'utf8' });
    assert.equal(result.status, 127);
    assert.match(fs.readFileSync(path.join(cwd, 'obj/ci-diagnostics/managed-tests.log'), 'utf8'), /ENOENT/);
  } finally { fs.rmSync(cwd, { recursive: true, force: true }); }
});
