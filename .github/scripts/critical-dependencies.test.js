const test = require('node:test');
const assert = require('node:assert/strict');
const monitor = require('./critical-dependencies.js');

test('critical releases create one assigned issue each and stay quiet on later runs', async () => {
  const originalFetch = global.fetch;
  const issues = [];
  const releaseQueries = [];
  let labelExists = false;
  global.fetch = async () => ({
    ok: true,
    json: async () => ({ versions: ['2.5.1-develop.44', '2.5.1-develop.45'] }),
  });
  const github = {
    paginate: async () => issues,
    rest: {
      issues: {
        getLabel: async () => {
          if (!labelExists) throw Object.assign(new Error('missing label'), { status: 404 });
        },
        createLabel: async () => { labelExists = true; },
        listForRepo: async () => ({ data: issues }),
        create: async ({ title, body, assignees, labels }) => {
          const issue = { title, body, assignees, labels, html_url: `https://example.invalid/${issues.length + 1}` };
          issues.push(issue);
          return { data: issue };
        },
      },
      repos: {
        listReleases: async ({ owner, repo }) => {
          releaseQueries.push({ owner, repo });
          return { data: repo === 'Terminal.Gui'
          ? [{ tag_name: 'v2.5.1-develop.45', html_url: 'https://example.invalid/terminal', body: 'Fix progress callbacks' }]
          : [
            { tag_name: 'v1.29.0', html_url: 'https://example.invalid/stable', body: 'Source changes', draft: false, prerelease: false },
            { tag_name: 'v1.30.0-preview', html_url: 'https://example.invalid/preview', body: 'COM updates', draft: false, prerelease: true },
          ] };
        },
      },
    },
  };
  try {
    const args = {
      github,
      context: { repo: { owner: 'harder', repo: 'wingetscout' } },
      core: { info: () => {} },
      projectText: '<PackageReference Include="Terminal.Gui" Version="2.5.1-develop.44" />',
    };
    await monitor(args);
    assert.deepEqual(releaseQueries, [
      { owner: 'tui-cs', repo: 'Terminal.Gui' },
      { owner: 'microsoft', repo: 'winget-cli' },
    ]);
    assert.equal(issues.length, 3);
    assert.ok(issues.every(issue => issue.assignees[0] === 'harder' && issue.labels[0] === 'critical-dependency'));
    assert.match(issues[0].body, /progress callbacks/);
    assert.match(issues[2].body, /COM activation/);
    await monitor(args);
    assert.equal(issues.length, 3);
  } finally {
    global.fetch = originalFetch;
  }
});
