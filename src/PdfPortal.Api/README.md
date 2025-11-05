# PDF Portal API Configuration

## Setup Instructions (Recommended: .env file)

### 1. Create your .env file

Copy the `.env.template` file in the backend directory and add your actual credentials:

```bash
cd backend
cp .env.template .env
```

### 2. Configure Required Settings

Edit `backend/.env` and replace the placeholder values:

```bash
# Database Configuration
DATABASE_HOST=localhost
DATABASE_PORT=5432
DATABASE_NAME=pdfportal
DATABASE_USER=postgres
DATABASE_PASSWORD=123

# JWT Configuration
JWT_SECRET_KEY=your_jwt_secret_key_at_least_32_characters_long
JWT_ISSUER=PdfPortal
JWT_AUDIENCE=PdfPortalUsers
JWT_EXPIRY_MINUTES=60

# OpenAI Configuration
OPENAI_API_KEY=your_openai_api_key_here
OPENAI_MODEL=gpt-4-turbo-preview

# Cloudflare R2 (S3-compatible)
# Provide either R2_ACCOUNT_ID and leave R2_ENDPOINT empty (it will be built),
# or set R2_ENDPOINT explicitly (e.g. https://<account-id>.r2.cloudflarestorage.com)
R2_ACCOUNT_ID=
R2_ENDPOINT=
R2_ACCESS_KEY_ID=
R2_SECRET_ACCESS_KEY=
R2_ORIGINAL_BUCKET=pdf-uploaded
R2_PROCESSED_BUCKET=pdf-processed

# Application Configuration
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=http://localhost:5000
```

### 3. Security Notes

⚠️ **IMPORTANT**: Never commit `.env` to Git as it contains sensitive information!

- ✅ `.env` is already in `.gitignore`
- ✅ Share `.env.template` with your team
- ✅ Keep your actual `.env` file local only
- ✅ Configuration priority: `.env` > `appsettings.json`

### 4. Getting API Keys

#### OpenAI API Key
1. Go to https://platform.openai.com/api-keys
2. Create a new API key
3. Copy and paste it into `backend/.env` as `OPENAI_API_KEY`

#### JWT Secret Key
Generate a secure random key (at least 32 characters):
```bash
# PowerShell
[System.Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 }))
```

### 5. Run the Application

```bash
cd backend
dotnet run --project src/PdfPortal.Api
```

## Alternative: appsettings.json (Not Recommended for Sensitive Data)

You can still use `appsettings.json` if you prefer, but `.env` is more secure and follows best practices.

```bash
cp appsettings.json.template appsettings.json
# Edit appsettings.json with your credentials
```

## Configuration Priority

The application loads configuration in this order:
1. **Environment variables from `.env` file** (highest priority)
2. `appsettings.json`
3. Default fallback values

## Troubleshooting

- **"OpenAI API key not configured"**: Make sure `OPENAI_API_KEY` is set in `backend/.env`
- **"No .env file found"**: The app will fall back to `appsettings.json` - this is OK for development
- **Database connection errors**: Check your `DATABASE_*` environment variables in `.env`
- **Database connection failed**: Check your PostgreSQL connection string
- **JWT authentication failed**: Verify your JWT Key is at least 32 characters long

