# Changelog

All notable changes to the Expense Tracker API will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-05-09

### Added
- Initial release of Expense Tracker API
- User authentication with JWT and OTP verification
- Receipt upload and AI-powered parsing
- Expense categorization and management
- Budget creation and tracking
- Monthly analytics and spending insights
- AI-powered financial advice and anomaly detection
- Subscription/recurring expense detection
- Vendor-based automatic categorization
- Export functionality (Excel and PDF reports)
- Admin dashboard and user management
- Comprehensive API documentation with Swagger UI
- Docker support for containerized deployment
- Entity Framework Core with SQL Server database
- Structured logging and error handling

### Security
- JWT bearer token authentication
- Role-based authorization (User, Admin)
- Input validation and sanitization
- SQL injection prevention (parameterized queries)
- CORS configuration for frontend integration
- Security headers in API responses
- Secrets management via User Secrets

### Performance
- Async/await throughout codebase
- Database query optimization
- Pagination support for large datasets
- Response compression
- Caching strategies for frequently accessed data

---

## Guidelines for Updates

### When to Create a New Release

- New features (Minor version bump)
- Bug fixes (Patch version bump)
- Breaking changes (Major version bump)
- Security vulnerabilities (Patch or Minor depending on severity)

### Commit Message Format

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types**: `feat`, `fix`, `docs`, `style`, `refactor`, `perf`, `test`, `chore`, `ci`, `sec`

**Examples**:

```
feat(receipts): add OCR enhancement for low-quality images

- Improved image preprocessing pipeline
- Added rotation detection and correction
- Better handling of tilted receipts

Closes #156
```

```
fix(budget): correct monthly calculation for partial months

Previously the system calculated budgets incorrectly when
the month had fewer than 30 days. Now properly accounts for
actual days in the month.

Fixes #203
```

```
sec(auth): update JWT secret rotation schedule

Implemented automatic JWT secret rotation every 90 days
to enhance security. Old tokens remain valid until expiry.

Relates to #145
```

### Release Checklist

- [ ] All tests pass
- [ ] Documentation updated
- [ ] Version bumped in project files
- [ ] Changelog updated
- [ ] Security vulnerabilities addressed
- [ ] Dependencies updated (if needed)
- [ ] Performance reviewed
- [ ] Code reviewed and approved
- [ ] Tag created: `git tag -a v1.0.0 -m "Release version 1.0.0"`
- [ ] Release notes published on GitHub

---

## Unreleased

### Planned Features
- Advanced spending predictions using ML
- Bill reminders and upcoming payment notifications
- Team/family expense sharing
- Custom report generation
- Mobile app integration
- Two-factor authentication (2FA) enhancements
- Integration with banking APIs
- Multi-currency support

---

**For more details, see [CONTRIBUTING.md](CONTRIBUTING.md)**
