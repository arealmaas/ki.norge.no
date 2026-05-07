# Security Policy

## Reporting a Vulnerability

For security issues affecting ki.norge.no, please report privately to the Digitaliseringsdirektoratet team rather than opening a public issue.

Email: drift@digdir.no

Please include:
- A description of the issue
- Steps to reproduce
- Affected URL or endpoint
- Your contact info if you'd like a response

We aim to acknowledge within 5 business days. We won't publicly disclose details until a fix is deployed and (if applicable) Digdir's incident response process is complete.

## Supported Versions

Only the deployed version at https://ki.norge.no is in scope. Older builds, forks, and experiments aren't supported.

## What's in scope

- ki.norge.no (frontend)
- cms.ki.norge.no (Umbraco backoffice — but please do not attempt to log in or modify content as part of testing)
- Delivery API (read-only public endpoints)

## What's out of scope

- DOS / volumetric attacks against the production site
- Social engineering of staff
- Issues in third-party services (Azure, Cloudflare, Umbraco core) — please report those upstream
- Self-inflicted issues from logged-in admin sessions

## Hall of fame

We don't currently run a bug bounty, but we'll credit responsible disclosures here if the reporter wants.
