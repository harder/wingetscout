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
    json: async () => ({ items: [{ upper: '2.6.0-develop.61', items: [
      { catalogEntry: { version: '2.5.1-develop.44', published: '2026-10-12T00:00:00Z' } },
      { catalogEntry: { version: '2.5.1-develop.45', published: '2026-10-13T00:00:00Z' } },
      { catalogEntry: { version: '2.6.0-develop.61', published: '2026-10-01T00:00:00Z' } },
    ] }] }),
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

test('develop selection follows publication time across backmerged version trains', () => {
  const entry = (version, published, listed = true) => ({ catalogEntry: { version, published, listed } });
  assert.equal(monitor.latestDevelop([
    entry('2.6.0-develop.61', '2026-10-01T00:00:00Z'),
    entry('2.5.1-develop.44', '2026-10-12T00:00:00Z'),
    entry('2.5.1-develop.81', '2026-10-14T00:00:00Z'),
    entry('2.5.1', '2026-10-15T00:00:00Z'),
    entry('2.5.1-develop.82', '2026-10-15T00:00:00Z', false),
    entry('2.5.1-develop.83', 'invalid'),
  ]).version, '2.5.1-develop.81');
});

for (const installed of ['2.5.1-develop.81', '2.6.0-develop.61']) {
  test(`paged registration follows publication order when ${installed} is installed`, async () => {
    const originalFetch = global.fetch;
    const urls = [];
    const entry = (version, published) => ({ catalogEntry: { version, published } });
    global.fetch = async url => {
      urls.push(url);
      const pages = {
        'https://example.invalid/old': { items: [entry('2.4.15', '2026-08-01T00:00:00Z')] },
        'https://example.invalid/lower': { items: [
          entry('2.5.1-develop.44', '2026-09-28T11:36:50Z'),
          entry('2.5.1-develop.81', '2026-10-13T21:40:06Z'),
        ] },
        'https://example.invalid/higher': { items: [entry('2.6.0-develop.61', '2026-09-03T16:07:46Z')] },
      };
      return { ok: true, json: async () => url.endsWith('/index.json') ? { items: [
        { upper: '2.4.15', '@id': 'https://example.invalid/old' },
        { upper: '2.5.1-develop.81', '@id': 'https://example.invalid/lower' },
        { upper: '2.6.0-develop.61', '@id': 'https://example.invalid/higher' },
      ] } : pages[url] };
    };
    const created = [];
    const github = { rest: {
      issues: {
        getLabel: async () => ({}),
        create: async issue => {
          created.push(issue);
          return { data: { html_url: 'https://example.invalid/issue' } };
        },
      },
      repos: { listReleases: async ({ repo }) => {
        return { data: repo === 'Terminal.Gui' ? []
          : [{ tag_name: 'v1.29.380', draft: false, prerelease: false }] };
      } },
    }, paginate: async () => [{ title: 'WinGet stable release: v1.29.380' }] };
    try {
      await monitor({ github, context: { repo: { owner: 'harder', repo: 'wingetscout' } },
        core: { info: () => {} },
        projectText: `<PackageReference Include="Terminal.Gui" Version="${installed}" />` });
      assert.deepEqual(urls.slice(1), ['https://example.invalid/old',
        'https://example.invalid/lower', 'https://example.invalid/higher']);
      if (installed === '2.5.1-develop.81') {
        assert.equal(created.length, 0);
      } else {
        assert.equal(created.length, 1);
        assert.equal(created[0].title, 'Terminal.Gui develop update: 2.5.1-develop.81');
      }
    } finally { global.fetch = originalFetch; }
  });
}
