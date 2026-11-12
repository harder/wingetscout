// Keep a small diagnostic tail without changing the command's exit status.
const { spawn } = require('node:child_process');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const { stripVTControlCharacters } = require('node:util');

const LIMIT = 32 * 1024;

function sanitize(text, env = process.env) {
  // Redact known credential forms; this is not a filter for arbitrary private data.
  text = stripVTControlCharacters(text);
  for (const [name, value] of Object.entries(env)) {
    if (/token|password|secret|credential|connection.?string/i.test(name) && value?.length >= 4) {
      text = text.split(value).join('[REDACTED]');
    }
  }
  return text
    .replace(/(\bhttps?:\/\/)[^/\s@]+@/gi, '$1[REDACTED]@')
    .replace(/(["']?\b(?:authorization|access_token|token|password|secret)\b["']?\s*[:=]\s*)("(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|(?:bearer|basic)\s+[^\s,;&]+|[^\s,;&]+)/gi, '$1[REDACTED]')
    .replace(/\b(?:gh[pousr]_[A-Za-z0-9_]+|github_pat_[A-Za-z0-9_]+)\b/g, '[REDACTED]')
    .replace(/[\x00-\x08\x0b-\x1f\x7f]/g, '');
}

// Prefer complete final lines, but retain a bounded fragment when a tool emits
// one long line (including carriage-return progress output).
function tail(text) {
  if (Buffer.byteLength(text) <= LIMIT) return text;
  text = Buffer.from(text).subarray(-LIMIT).toString('utf8');
  const newline = text.indexOf('\n');
  const completeLines = newline >= 0 ? text.slice(newline + 1) : '';
  return '[earlier output omitted]\n' + (completeLines.trim() ? completeLines : text);
}

async function run(phase, command, args) {
  if (!/^[a-z][a-z0-9-]*$/.test(phase || '') || !command) throw new Error('Usage: ci-command.js <phase> <command> [args...]');
  let stdout = '', stderr = '';
  const child = spawn(command, args, { stdio: ['ignore', 'pipe', 'pipe'], windowsHide: true });
  child.stdout.setEncoding('utf8');
  child.stderr.setEncoding('utf8');
  child.stdout.on('data', data => { process.stdout.write(data); stdout = tail(stdout + data); });
  child.stderr.on('data', data => { process.stderr.write(data); stderr = tail(stderr + data); });
  child.on('error', error => { stderr += `Could not start command: ${error.code}\n`; });
  const code = await new Promise(resolve => child.on('close', (status, signal) => {
    resolve(status !== null && status >= 0 ? status : signal ? 128 + (os.constants.signals[signal] || 0) : 127);
  }));
  try {
    const directory = path.join('obj', 'ci-diagnostics');
    fs.mkdirSync(directory, { recursive: true });
    let sdk = 'unavailable';
    try { sdk = JSON.parse(fs.readFileSync('global.json', 'utf8')).sdk?.version ?? sdk; } catch { /* Still retain the command output. */ }
    const metadata = `Phase: ${phase}\nExit code: ${code}\nOS/architecture: ${os.platform()}/${os.arch()}\nSDK pin: ${sdk}\n`;
    const output = sanitize(metadata) + `\nstdout (tail):\n${tail(sanitize(stdout))}\nstderr (tail):\n${tail(sanitize(stderr))}`;
    fs.writeFileSync(path.join(directory, `${phase}.log`), output);
    if (code !== 0 && process.env.GITHUB_STEP_SUMMARY) {
      fs.appendFileSync(process.env.GITHUB_STEP_SUMMARY,
        `### Failed ${phase}\n\n\`\`\`text\n${sanitize(metadata).replace(/`/g, "'")}\`\`\`\n\nSee the failure-diagnostics artifact for bounded output.\n`);
    }
  } catch (error) {
    // Reporting failure must never replace the test/smoke command's result.
    process.stderr.write(`Diagnostic capture failed (${error.code || error.name}); original exit code: ${code}\n`);
  }
  return code;
}

module.exports = { run, sanitize, tail, LIMIT };
if (require.main === module) {
  run(...process.argv.slice(2, 4), process.argv.slice(4)).then(code => { process.exitCode = code; })
    .catch(error => { console.error(error.message); process.exitCode = 1; });
}
