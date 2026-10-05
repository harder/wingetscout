# WinGet Scout website

The website lives in `site/` and is deployed by [Pages](../.github/workflows/pages.yml)
when site files reach `main`. It uses plain HTML, CSS, JavaScript, and Python's standard
library. [SkillView](https://github.com/harder/gh-skillview) uses the same build/deploy
shape, but this site has its own Windows-inspired design.

Run `python site/build.py` from the repository root to copy published files to
`site/_build/` and check local links. The Pages workflow runs the same check on PRs.
The real application captures in `site/media/` were recorded with
[tuirec](https://github.com/tui-cs/tuirec) in `--mock` mode. They show demo data and do not
change installed packages.

## Set up wingetscout.com

The workflow can deploy after the site is merged. GitHub Pages needs **GitHub Actions**
as its publishing source and `wingetscout.com` saved as its custom domain in
**harder/wingetscout → Settings → Pages**. The included `CNAME` is a copy of the intended
domain for the built output; GitHub ignores that file for custom-workflow publishing,
so the Pages setting is required.

At the domain's DNS provider, create these records (or an equivalent apex ALIAS/ANAME):

| Type | Name | Value |
| --- | --- | --- |
| A | `@` | `185.199.108.153` |
| A | `@` | `185.199.109.153` |
| A | `@` | `185.199.110.153` |
| A | `@` | `185.199.111.153` |
| CNAME | `www` | `harder.github.io` |

Remove conflicting apex A/AAAA/ALIAS/ANAME records and conflicting `www` records.
An optional IPv6 setup uses GitHub's published AAAA addresses; see the
[GitHub DNS guidance](https://docs.github.com/en/pages/configuring-a-custom-domain-for-your-github-pages-site/managing-a-custom-domain-for-your-github-pages-site).
The `www` record lets GitHub redirect `www.wingetscout.com` to the apex domain.
Avoid wildcard DNS records for this domain.

Verify the domain under the GitHub account's **Settings → Pages → Verified domains**;
GitHub supplies a unique TXT record for this step. After DNS and the certificate are
ready, enable **Enforce HTTPS** in the repository's Pages settings. DNS propagation
and certificate issuance can take time. Confirm with:

```powershell
Resolve-DnsName wingetscout.com -Type A
Resolve-DnsName www.wingetscout.com -Type CNAME
Invoke-WebRequest https://wingetscout.com/ -UseBasicParsing
```

See [GitHub's custom-domain instructions](https://docs.github.com/en/pages/configuring-a-custom-domain-for-your-github-pages-site/managing-a-custom-domain-for-your-github-pages-site)
for current DNS records and setup details.
