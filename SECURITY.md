# Security Policy

## Reporting a Vulnerability

**Do NOT create public GitHub issues for security vulnerabilities.**

If you discover a security vulnerability in the Expense Tracker API or UI, please report it responsibly by emailing:

**Email:** [Create a GitHub Security Advisory](https://github.com/avikchowdhury/Expense-Tracker-Api/security/advisories/new)

**Or contact:** avikchowdhury@gmail.com with subject: **[SECURITY] Expense Tracker Vulnerability**

Please include:
- Description of the vulnerability
- Affected versions (if applicable)
- Steps to reproduce
- Potential impact assessment
- Suggested fix (if available)

We will acknowledge your report within 48 hours and work with you to resolve the issue before public disclosure.

---

## Supported Versions

| Version | Status | Security Support |
|---------|--------|------------------|
| 1.x.x   | Current | ✅ Active        |

---

## Security Best Practices

### For Developers

1. **Secrets Management**
   - Never commit API keys, passwords, or connection strings
   - Use `.NET User Secrets` for local development
   - Use environment variables or GitHub Secrets for CI/CD

2. **Dependencies**
   - Keep NuGet packages updated
   - Run `dotnet list package --vulnerable` regularly
   - Review and audit third-party packages before adding

3. **Code Review**
   - All PRs require review before merging
   - Security-sensitive code requires at least 2 approvals
   - Use static analysis tools (SonarQube, Roslyn analyzers)

4. **Input Validation**
   - Validate all user input server-side
   - Never trust client-side validation alone
   - Sanitize any output rendered to clients

5. **SQL Injection Prevention**
   - Always use parameterized queries (EF Core handles this)
   - Never concatenate user input into SQL strings
   - Use stored procedures only with parameterized inputs

### For Users

1. **Authentication**
   - Use a strong, unique password
   - Enable two-factor authentication (2FA) when available
   - Never share your authentication token

2. **API Usage**
   - Store API credentials securely
   - Rotate API keys periodically
   - Use HTTPS for all API calls

3. **Data Protection**
   - Review data permissions
   - Regularly review connected applications
   - Disable unused integrations

---

## Branch Protection Rules

The `master` branch is protected with the following rules:

- ✅ Require pull request reviews (1+ approval)
- ✅ Dismiss stale reviews when new commits are pushed
- ✅ Require status checks to pass (CI/CD)
- ✅ Require branches to be up to date before merging
- ✅ Require conversation resolution
- ✅ Enforce admins to follow these rules

---

## Security Headers & Configurations

### API Response Headers

All API responses include security headers:
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `X-XSS-Protection: 1; mode=block`
- `Strict-Transport-Security: max-age=31536000; includeSubDomains`

### CORS Policy

CORS is configured with explicit allowed origins:
- Development: `http://localhost:3000`, `http://localhost:5173`
- Production: Your actual frontend domain

Never use `AllowAnyOrigin()`.

### JWT Authentication

- Algorithm: HS256
- Secret: Minimum 32 characters
- Expiry: Configurable (default: 24 hours)
- Refresh tokens supported via `/api/auth/refresh`

---

## Incident Response

In case of a security incident:

1. **Immediate Actions**
   - Disable affected services if necessary
   - Notify all administrators
   - Begin investigation

2. **Documentation**
   - Log all actions taken
   - Document timeline of incident
   - Identify root cause

3. **Communication**
   - Notify affected users within 24 hours
   - Provide mitigation steps
   - Publish security advisory (if needed)

4. **Remediation**
   - Implement fixes
   - Deploy patched version
   - Conduct post-mortem

---

## Compliance & Standards

This project follows:
- **OWASP Top 10** application security risks
- **CWE** (Common Weakness Enumeration) best practices
- **.NET Security Guidelines** from Microsoft
- **Data Protection Regulations** (GDPR, CCPA)

---

## Security Tools & Scans

### Automated Scanning

- **Dependabot** - Monitors NuGet package vulnerabilities
- **GitHub Secret Scanning** - Prevents accidental credential commits
- **SonarQube** - Code quality and security analysis
- **OWASP ZAP** - Dynamic security testing (future)

### Manual Reviews

- Quarterly security audits
- Penetration testing (as needed)
- Code review with security focus

---

## Questions & Support

For security questions not related to vulnerabilities:
- Open a GitHub Discussion
- Check existing issues and documentation
- Contact maintainers via Issues (non-sensitive questions only)

---

**Last Updated:** May 9, 2026

Thank you for helping us keep Expense Tracker secure! 🔒
