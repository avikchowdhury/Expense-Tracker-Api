# Deployment Guide

This guide covers deploying the Expense Tracker API to production environments.

## 📋 Pre-Deployment Checklist

- [ ] All tests passing locally
- [ ] Code reviewed and approved
- [ ] Security audit completed
- [ ] Dependencies up to date
- [ ] Environment variables configured
- [ ] Database backups scheduled
- [ ] Monitoring and logging configured
- [ ] API documentation up to date

---

## 🌐 Deployment Environments

### Development
- **URL**: `http://localhost:5000`
- **Database**: Local SQL Server
- **Logging**: Console + Debug output
- **Secrets**: .NET User Secrets

### Staging
- **URL**: `https://staging-api.yourdomain.com`
- **Database**: Staging SQL Server
- **SSL**: Required
- **Logging**: File + Application Insights

### Production
- **URL**: `https://api.yourdomain.com`
- **Database**: Production SQL Server (encrypted)
- **SSL**: Required + HSTS enabled
- **Logging**: Application Insights + Sentry
- **Monitoring**: Azure Monitor

---

## 🚀 Azure App Service Deployment

### 1. Prerequisites
- Azure subscription
- Azure CLI installed
- App Service Plan created
- SQL Database created

### 2. Publish the Application

```bash
# Using Azure CLI
az webapp up --name expense-tracker-api \
  --resource-group my-resource-group \
  --location eastus \
  --sku B2

# Using Visual Studio
# Right-click project → Publish → Azure App Service
```

### 3. Configure Environment Variables

```bash
# Via Azure CLI
az webapp config appsettings set \
  --name expense-tracker-api \
  --resource-group my-resource-group \
  --settings \
    "ConnectionStrings__DefaultConnection=Server=tcp:your-server.database.windows.net;Database=ExpenseTrackerDb;..." \
    "Jwt__Secret=your-production-secret" \
    "OpenAI__ApiKey=gsk_..." \
    "AzureBlob__ConnectionString=..." \
    "Email__SenderPassword=..."
```

### 4. Apply Database Migrations

```bash
# SSH into the App Service
az webapp remote-connection create \
  --subscription your-subscription \
  --resource-group my-resource-group \
  --name expense-tracker-api

# Run migrations
dotnet ef database update --context AppDbContext
```

### 5. Enable HTTPS & Security Headers

```bash
# Enable HTTPS only
az webapp update \
  --resource-group my-resource-group \
  --name expense-tracker-api \
  --https-only true

# Configure security headers in web.config or Startup
```

---

## 🐳 Docker Deployment

### 1. Build Docker Image

```bash
# Build the image
docker build -t expense-tracker-api:latest .

# Tag for registry
docker tag expense-tracker-api:latest myregistry.azurecr.io/expense-tracker-api:latest

# Push to Azure Container Registry
docker push myregistry.azurecr.io/expense-tracker-api:latest
```

### 2. Deploy to Docker

```bash
# Run locally first
docker run -p 5001:5001 \
  -e "ConnectionStrings__DefaultConnection=..." \
  -e "Jwt__Secret=..." \
  -e "OpenAI__ApiKey=..." \
  expense-tracker-api:latest

# Deploy to production
az container create \
  --resource-group my-resource-group \
  --name expense-tracker-api \
  --image myregistry.azurecr.io/expense-tracker-api:latest \
  --ports 443 \
  --environment-variables \
    "ConnectionStrings__DefaultConnection=..." \
    "Jwt__Secret=..."
```

---

## 📊 Monitoring & Logging

### Application Insights Setup

```csharp
// In Program.cs
builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();
app.UseApplicationInsights();
```

### Logging Configuration

```json
{
  "ApplicationInsights": {
    "InstrumentationKey": "your-instrumentation-key"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    },
    "ApplicationInsights": {
      "LogLevel": {
        "Default": "Information"
      }
    }
  }
}
```

### Alerts & Health Checks

```csharp
// Add health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>()
    .AddAzureBlobStorage(connectionString)
    .AddUrlGroup(new Uri("https://api.groq.com"), "Groq API");

// Map health check endpoint
app.MapHealthChecks("/health");
```

---

## 🔐 Security in Production

### 1. Database Security
- ✅ Enable transparent data encryption (TDE)
- ✅ Use Azure Key Vault for connection strings
- ✅ Configure firewall rules (allow only app IP)
- ✅ Regular automated backups
- ✅ Point-in-time restore enabled

### 2. API Security
- ✅ Enforce HTTPS only
- ✅ Enable HSTS headers
- ✅ Configure CORS with specific origins only
- ✅ Rate limiting implemented
- ✅ DDoS protection enabled

### 3. Secrets Management
- ✅ Never commit secrets to Git
- ✅ Use Azure Key Vault
- ✅ Rotate credentials regularly
- ✅ Enable secret scanning in GitHub

### 4. Authentication
- ✅ JWT tokens with short expiry (24 hours)
- ✅ Refresh token rotation
- ✅ Secure token storage (HttpOnly cookies)
- ✅ Password hashing with bcrypt

---

## 📈 Scaling & Performance

### Horizontal Scaling
```bash
# Scale App Service plan
az appservice plan update \
  --name your-app-service-plan \
  --resource-group my-resource-group \
  --sku P1V2 \
  --number-of-workers 3
```

### Database Optimization
- Enable query performance insights
- Add indexes on frequently queried columns
- Use read replicas for analytics
- Implement query result caching

### Caching Strategy
```csharp
// In-memory cache for frequent queries
builder.Services.AddStackExchangeRedisCache(options =>
    options.Configuration = builder.Configuration.GetConnectionString("Redis"));
```

---

## 🚨 Incident Response

### Deployment Rollback

```bash
# Rollback to previous version
az webapp deployment slot swap \
  --name expense-tracker-api \
  --resource-group my-resource-group \
  --slot staging
```

### Emergency Procedures

1. **Database Corruption**
   - Restore from automated backup
   - Verify data integrity
   - Notify users if needed

2. **Security Breach**
   - Immediately rotate all secrets
   - Review access logs
   - Contact security team
   - Follow incident response plan

3. **Service Outage**
   - Check Azure status dashboard
   - Review application logs
   - Failover to backup region (if configured)
   - Communicate status to users

---

## ✅ Post-Deployment

- [ ] Run smoke tests
- [ ] Verify all endpoints responding
- [ ] Check database connectivity
- [ ] Confirm monitoring/logging working
- [ ] Test authentication flow
- [ ] Verify file uploads to Azure Blob
- [ ] Review security headers
- [ ] Load test the API
- [ ] Update DNS records
- [ ] Announce deployment

---

## 📞 Support & Troubleshooting

For deployment issues:
1. Check Azure portal diagnostics
2. Review application logs in Application Insights
3. Verify environment variables are set
4. Check database migration status
5. Review CORS and firewall settings

---

**Last Updated**: May 2026

For questions, open a GitHub Issue or contact the maintainers.
