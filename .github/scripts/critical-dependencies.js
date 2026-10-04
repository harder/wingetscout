const fs = require('node:fs');

const LABEL = 'critical-dependency';

function developVersion(version) {
  const match = /^(\d+)\.(\d+)\.(\d+)-develop\.(\d+)$/i.exec(version);
  return match ? match.slice(1).map(Number) : null;
}

function compareParts(left, right) {
  for (let index = 0; index < left.length; index++) {
    if (left[index] !== right[index]) return left[index] - right[index];
  }
  return 0;
}

function suggestedChecks(notes) {
  const text = (notes || '').toLowerCase();
  const checks = [];
  if (/\b(pin|pinned|pinning)\b/.test(text)) checks.push('pin list parsing, pin/unpin behavior, and pinned upgrades');
  if (/\b(source|catalog|repository)\b/.test(text)) checks.push('source enumeration, agreements, and source-aware package selection');
  if (/\b(com|winrt|projection|activation)\b/.test(text)) checks.push('Native AOT COM activation and read-only `--comdiag`/`--comsmoke`');
  if (/\b(progress|cancel|async)\b/.test(text)) checks.push('progress callbacks, cancellation, and UI thread dispatch');
  if (/\b(installer|install|upgrade|uninstall)\b/.test(text)) checks.push('installer preview and install/upgrade/uninstall plans');
  if (/\b(table|output|format|json)\b/.test(text)) checks.push('captured CLI output fixtures and table parsing');
  return checks.length ? checks.map(check => `- ${check}`).join('\n') : '- Review upstream API and output changes; add a focused test for any behavior Scout uses.';
}

async function ensureLabel(github, owner, repo) {
  try {
    await github.rest.issues.getLabel({ owner, repo, name: LABEL });
  } catch (error) {
    if (error.status !== 404) throw error;
    await github.rest.issues.createLabel({
      owner, repo, name: LABEL, color: 'B60205',
      description: 'Upstream change requiring WinGet Scout compatibility review',
    });
  }
}

async function createOnce(github, core, owner, repo, title, body) {
  const existing = await github.paginate(github.rest.issues.listForRepo, {
    owner, repo, labels: LABEL, state: 'all', per_page: 100,
  });
  if (existing.some(issue => !issue.pull_request && issue.title === title)) {
    core.info(`Already tracked: ${title}`);
    return;
  }
  const { data: issue } = await github.rest.issues.create({
    owner, repo, title, body, labels: [LABEL], assignees: [owner],
  });
  core.info(`Created ${issue.html_url}`);
}

async function checkTerminalGui(github, core, owner, repo, project) {
  const match = /<PackageReference Include="Terminal\.Gui" Version="([^"]+)"/.exec(project);
  if (!match) throw new Error('Terminal.Gui PackageReference was not found');
  const current = developVersion(match[1]);
  if (!current) throw new Error(`Expected a Terminal.Gui develop prerelease, got ${match[1]}`);

  const response = await fetch('https://api.nuget.org/v3-flatcontainer/terminal.gui/index.json', {
    signal: AbortSignal.timeout(15000),
  });
  if (!response.ok) throw new Error(`NuGet returned HTTP ${response.status}`);
  const { versions } = await response.json();
  const candidates = versions
    .map(version => ({ version, parts: developVersion(version) }))
    .filter(candidate => candidate.parts)
    .sort((a, b) => compareParts(b.parts, a.parts));
  if (candidates.length === 0) throw new Error('No Terminal.Gui develop versions found');
  const latest = candidates[0];
  if (compareParts(latest.parts, current) <= 0) {
    core.info(`Terminal.Gui develop is current: ${match[1]}`);
    return;
  }
  const { data: releases } = await github.rest.repos.listReleases({
    owner: 'gui-cs', repo: 'Terminal.Gui', per_page: 30,
  });
  const upstream = releases.find(release => release.tag_name.toLowerCase().includes(latest.version.toLowerCase()));
  await createOnce(github, core, owner, repo, `Terminal.Gui develop update: ${latest.version}`, `
Scout currently references **${match[1]}**; NuGet has **${latest.version}** on the develop channel.

- [NuGet package and version history](https://www.nuget.org/packages/Terminal.Gui/${latest.version})
- [Upstream changes](${upstream ? upstream.html_url : 'https://github.com/gui-cs/Terminal.Gui/releases'})
- Check whether Dependabot opened an update PR. Its version PR may target a stable release instead of the develop channel.
- Compare APIs used by Scout, especially UI thread dispatch, shutdown, layout, input, and progress callbacks.
- Run unit tests, Windows x64 and ARM64 AOT publish, and mock smoke; record any live COM/TUI checks that require a Windows host.

Suggested checks from the upstream release notes:
${suggestedChecks(upstream?.body)}

Ask Copilot to review the update PR using the repository instructions and suggest concrete compatibility fixes. Keep the dependency bump under human review.
`.trim());
}

async function checkWinget(github, core, owner, repo) {
  const { data: releases } = await github.rest.repos.listReleases({
    owner: 'microsoft', repo: 'winget-cli', per_page: 30,
  });
  const newest = [
    releases.find(release => !release.draft && !release.prerelease),
    releases.find(release => !release.draft && release.prerelease),
  ].filter(Boolean);
  if (newest.length === 0) throw new Error('No WinGet CLI releases found');
  for (const release of newest) {
    const channel = release.prerelease ? 'preview' : 'stable';
    await createOnce(github, core, owner, repo,
      `WinGet ${channel} release: ${release.tag_name}`, `
Microsoft published [WinGet ${release.tag_name}](${release.html_url}) (${channel}).

- Compare the release notes with Scout's src/CliBackend.cs parser, subprocess flags, and src/ComBackend.cs API usage.
- Check whether both Microsoft.WindowsPackageManager.ComInterop and Microsoft.WindowsPackageManager.InProcCom have matching NuGet updates; Dependabot should update them in one PR.
- Refresh CLI output fixtures if tables, pins, progress, or source output changed.
- Run tests and Windows x64/ARM64 AOT smoke. On a real Windows host, run read-only --comdiag and --comsmoke; record the installed WinGet version and any fallback.
- Ask Copilot to propose focused tests or compatibility fixes from the release notes. Review changes before merging.

Suggested checks from the upstream release notes:
${suggestedChecks(release.body)}
`.trim());
  }
}

module.exports = async ({ github, context, core, projectText = fs.readFileSync('WinGetScout.csproj', 'utf8') }) => {
  const { owner, repo } = context.repo;
  await ensureLabel(github, owner, repo);
  await checkTerminalGui(github, core, owner, repo, projectText);
  await checkWinget(github, core, owner, repo);
};

module.exports.developVersion = developVersion;
module.exports.compareParts = compareParts;
module.exports.suggestedChecks = suggestedChecks;
