# Hanabi Game Analytics

.NET 8 API with SQLite, React 19 + Vite frontend.

## Commands

Backend: `cd MyWebApi && dotnet run`

Frontend: `cd MyWebApi/frontend && npm run dev`

## Rules Reference

For any Hanabi rule clarifications, use: https://hanabi.github.io/

---

## Deployment

### URLs
- **Frontend**: https://hanab-frontend.vercel.app
- **Backend**: https://hanab-analytics-api.azurewebsites.net
- **API Health Check**: https://hanab-analytics-api.azurewebsites.net/weatherforecast

### Azure Resources
- **Resource Group**: `hanab-rg`
- **Web App**: `hanab-analytics-api` (Linux, .NET 8, Free tier F1)
- **Location**: Canada Central
- **App Service Plan**: `nckbrln_asp_6380`

### Turso Database
- **Database**: `hanab-analytics`
- **URL**: `libsql://hanab-analytics-nberlin.aws-eu-west-1.turso.io`
- **To get new token**: `turso db tokens create hanab-analytics`

### GitHub Actions (Backend)
Automatically deploys backend to Azure on push to main.

**Secrets configured** (in GitHub repo):
- `AZURE_CREDENTIALS` - Service principal JSON for Azure login
- `AZURE_WEBAPP_NAME` - `hanab-analytics-api` (legacy, not used in current workflow)
- `AZURE_WEBAPP_PUBLISH_PROFILE` - (legacy, not used - publish profiles don't work for Linux)
- `VERCEL_ORG_ID`, `VERCEL_PROJECT_ID` - (not used, Vercel uses GitHub integration)

### Vercel (Frontend) - SETUP NEEDED BY JAHOLL

**Jaholl (repo owner) needs to:**
1. Install Vercel GitHub App: https://github.com/apps/vercel
2. Grant access to `hanab-game-analytics` repository
3. Go to https://vercel.com/new and import the repo
4. **Set Root Directory**: `MyWebApi/frontend`
5. **Add Environment Variable**: `VITE_API_URL` = `https://hanab-analytics-api.azurewebsites.net`

After setup, Vercel will auto-deploy frontend on every push to main.

### Environment Variables

**Azure Web App** (already configured):
```
TURSO_URL=libsql://hanab-analytics-nberlin.aws-eu-west-1.turso.io
TURSO_AUTH_TOKEN=<token from turso db tokens create>
SCM_DO_BUILD_DURING_DEPLOYMENT=false
```

**Vercel** (Jaholl needs to add after setup):
```
VITE_API_URL=https://hanab-analytics-api.azurewebsites.net
```

**Local Development** (`MyWebApi/frontend/.env.local`):
```
VITE_API_URL=http://localhost:5191
```

### CORS
Backend allows these origins (in `Program.cs`):
- `http://localhost:5173-5176` (local dev)
- `https://hanab-frontend.vercel.app`
- `https://hanab-frontend-*.vercel.app` (preview deploys)

If Vercel URL changes, update CORS in `Program.cs` and redeploy backend.

### Manual Deployment Commands

**Backend to Azure**:
```bash
cd MyWebApi
dotnet publish -c Release -o ./publish
cd publish && zip -r ../deploy.zip .
az webapp deployment source config-zip --name hanab-analytics-api --resource-group hanab-rg --src ../deploy.zip
```

**Frontend to Vercel** (if needed):
```bash
cd MyWebApi/frontend
vercel --prod
```

### Rotate Turso Token
If token expires or is compromised:
```bash
# Generate new token
turso db tokens create hanab-analytics

# Update Azure
az webapp config appsettings set --name hanab-analytics-api --resource-group hanab-rg --settings TURSO_AUTH_TOKEN="<new-token>"

# Update local .env if needed
```

### Docker Local Dev (optional)
```bash
docker compose up    # Start API + local LibSQL
docker compose down  # Stop
docker compose down -v  # Stop and wipe DB
```
