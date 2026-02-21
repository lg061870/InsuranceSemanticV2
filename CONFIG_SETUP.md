# Configuration Setup Guide

## ?? Important: Configuration Files with Secrets

The following configuration files contain **sensitive information** (passwords, connection strings, API keys, JWT secrets) and are **excluded from Git** via `.gitignore`:

- `**/appsettings.Production.json`
- `**/appsettings.Staging.json`
- `**/appsettings.Development.json`
- `InsuranceAgent/appsettings.json`
- `InsuranceSemanticV2.Api/appsettings.json`

## ?? Setting Up Your Configuration

### Step 1: Copy Template Files

Template files are provided with `.template` extension. Copy them and remove the `.template`:

```bash
# API Configuration
cp InsuranceSemanticV2.Api/appsettings.json.template InsuranceSemanticV2.Api/appsettings.json
cp InsuranceSemanticV2.Api/appsettings.Production.json.template InsuranceSemanticV2.Api/appsettings.Production.json

# InsuranceAgent Configuration
cp InsuranceAgent/appsettings.Production.json.template InsuranceAgent/appsettings.Production.json
```

### Step 2: Update Secrets

**For `InsuranceSemanticV2.Api/appsettings.Production.json`:**
1. Replace `YOUR_PRODUCTION_CONNECTION_STRING_HERE` with your actual database connection string
2. Replace `YOUR_PRODUCTION_JWT_SECRET_KEY_MIN_32_CHARS` with a strong random key (min 32 chars)

**Generate a secure JWT secret:**
```powershell
[Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(64))
```

**For `InsuranceAgent/appsettings.Production.json`:**
1. Replace `YOUR_PRODUCTION_URL_HERE` with your actual production URL

### Step 3: User Secrets (Recommended for Development)

For local development, use User Secrets instead of committing secrets:

```bash
cd InsuranceSemanticV2.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "YOUR_CONNECTION_STRING"
dotnet user-secrets set "Jwt:SecretKey" "YOUR_JWT_SECRET"
```

## ?? Security Best Practices

1. **Never commit production configs** to Git
2. **Use different secrets** for each environment (dev/staging/production)
3. **Rotate JWT secrets** periodically
4. **Use Azure Key Vault** or similar for production deployments
5. **Set strong database passwords** (min 12 chars, mixed case, numbers, symbols)

## ?? Files Already Excluded from Git

The `.gitignore` file already excludes:
- All production/staging config files
- Publish profiles (contain deployment credentials)
- User secrets
- Development configs

## ? What NOT to Do

- ? Don't commit files with real passwords
- ? Don't share JWT secrets publicly
- ? Don't use demo keys in production
- ? Don't commit publish profiles with credentials
