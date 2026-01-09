# Calendar App Deployment Guide

This guide will help you deploy the Calendar App to your Ubuntu webserver at sebaslive.xyz/calendar

## Prerequisites

- Ubuntu server with Apache installed
- .NET 8 Runtime installed on the server
- SSH access to your server
- Domain sebaslive.xyz already configured

## Step 1: Prepare Files for Transfer

The following files have been built and are ready for deployment:

**Frontend (built):**
- Location: `C:\Users\sebastiaan\source\repos\illAdvisedCalendarApp\AgendaFrontend\dist`

**Backend (published):**
- Location: `C:\Users\sebastiaan\source\repos\illAdvisedCalendarApp\AgendaApi\AgendaApi\publish`

## Step 2: Transfer Files to Server

From your Windows machine, use SCP or WinSCP to transfer files:

```bash
# Transfer frontend files
scp -r C:\Users\sebastiaan\source\repos\illAdvisedCalendarApp\AgendaFrontend\dist your-user@your-server-ip:/tmp/calendar-frontend

# Transfer backend files
scp -r C:\Users\sebastiaan\source\repos\illAdvisedCalendarApp\AgendaApi\AgendaApi\publish your-user@your-server-ip:/tmp/calendar-api

# Transfer systemd service file
scp C:\Users\sebastiaan\source\repos\illAdvisedCalendarApp\AgendaApi\calendar-api.service your-user@your-server-ip:/tmp/
```

## Step 3: SSH into Your Ubuntu Server

```bash
ssh your-user@your-server-ip
```

## Step 4: Set Up the API

```bash
# Create directory for API
sudo mkdir -p /var/www/calendar-api

# Move published files to the directory
sudo mv /tmp/calendar-api/* /var/www/calendar-api/

# Set proper permissions
sudo chown -R www-data:www-data /var/www/calendar-api
sudo chmod -R 755 /var/www/calendar-api

# Install the systemd service
sudo mv /tmp/calendar-api.service /etc/systemd/system/

# Reload systemd and start the service
sudo systemctl daemon-reload
sudo systemctl enable calendar-api.service
sudo systemctl start calendar-api.service

# Check service status
sudo systemctl status calendar-api.service
```

## Step 5: Configure the Database

The API uses SQL Server. You need to create an `appsettings.Production.json` file with your connection string:

```bash
sudo nano /var/www/calendar-api/appsettings.Production.json
```

Add the following content (adjust connection string to match your database):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=CalendarDb;User Id=your_user;Password=your_password;TrustServerCertificate=True;"
  },
  "Jwt": {
    "SecretKey": "your-secret-key-here-minimum-32-characters-long",
    "Issuer": "https://sebaslive.xyz",
    "Audience": "https://sebaslive.xyz"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

**Important:** Make sure to:
1. Set a strong JWT secret key (at least 32 characters)
2. Configure your actual database connection string
3. Set proper file permissions: `sudo chmod 600 /var/www/calendar-api/appsettings.Production.json`

After creating the configuration, restart the service:

```bash
sudo systemctl restart calendar-api.service
```

## Step 6: Set Up the Frontend

```bash
# Create directory for frontend
sudo mkdir -p /var/www/calendar-frontend

# Move built files to the directory
sudo mv /tmp/calendar-frontend/* /var/www/calendar-frontend/

# Set proper permissions
sudo chown -R www-data:www-data /var/www/calendar-frontend
sudo chmod -R 755 /var/www/calendar-frontend
```

## Step 7: Configure Apache

Edit your existing Apache VirtualHost configuration for sebaslive.xyz:

```bash
# Find your current site configuration (likely in /etc/apache2/sites-available/)
sudo nano /etc/apache2/sites-available/sebaslive.xyz.conf
```

Add the following configuration **inside your existing `<VirtualHost *:443>` block** (after your existing app configuration):

```apache
# Calendar Frontend - Serve static files from /calendar
Alias /calendar /var/www/calendar-frontend
<Directory /var/www/calendar-frontend>
    Options -Indexes +FollowSymLinks
    AllowOverride All
    Require all granted

    # Handle client-side routing - redirect all requests to index.html
    <IfModule mod_rewrite.c>
        RewriteEngine On
        RewriteBase /calendar
        RewriteRule ^index\.html$ - [L]
        RewriteCond %{REQUEST_FILENAME} !-f
        RewriteCond %{REQUEST_FILENAME} !-d
        RewriteRule . /calendar/index.html [L]
    </IfModule>
</Directory>

# Calendar API - Proxy to .NET backend
<Location /calendarapi>
    ProxyPass http://localhost:5001
    ProxyPassReverse http://localhost:5001
    ProxyPreserveHost On
</Location>
```

## Step 8: Enable Required Apache Modules

```bash
sudo a2enmod proxy
sudo a2enmod proxy_http
sudo a2enmod rewrite
```

## Step 9: Test and Restart Apache

```bash
# Test Apache configuration
sudo apache2ctl configtest

# If test passes, restart Apache
sudo systemctl restart apache2
```

## Step 10: Verify Deployment

1. Check that the API service is running:
   ```bash
   sudo systemctl status calendar-api.service
   ```

2. Check API logs if there are issues:
   ```bash
   sudo journalctl -u calendar-api.service -n 50
   ```

3. Visit your calendar app:
   - Frontend: https://sebaslive.xyz/calendar
   - API health check: https://sebaslive.xyz/calendarapi/swagger (if Swagger is enabled)

## Troubleshooting

### If the API service fails to start:

```bash
# Check logs
sudo journalctl -u calendar-api.service -n 100 --no-pager

# Verify .NET runtime is installed
dotnet --version

# Check file permissions
ls -la /var/www/calendar-api/
```

### If Apache gives 404 errors:

```bash
# Verify files are in the correct location
ls -la /var/www/calendar-frontend/

# Check Apache error logs
sudo tail -f /var/log/apache2/error.log
```

### If you get CORS errors:

Make sure your Apache configuration is using HTTPS and the domain matches what's configured in the API's CORS policy.

### Database Migration

If you need to run database migrations:

```bash
cd /var/www/calendar-api
sudo -u www-data dotenv ASPNETCORE_ENVIRONMENT=Production dotnet ef database update
```

Note: You might need to install EF Core tools on the server or run migrations from your development machine using a connection string to the production database.

## Updating the Application

### Update Frontend:

```bash
# Build locally on Windows
cd C:\Users\sebastiaan\source\repos\illAdvisedCalendarApp\AgendaFrontend
yarn build

# Transfer and deploy
scp -r dist/* your-user@your-server-ip:/tmp/calendar-frontend-update/
ssh your-user@your-server-ip
sudo rm -rf /var/www/calendar-frontend/*
sudo mv /tmp/calendar-frontend-update/* /var/www/calendar-frontend/
sudo chown -R www-data:www-data /var/www/calendar-frontend
```

### Update API:

```bash
# Build locally on Windows
cd C:\Users\sebastiaan\source\repos\illAdvisedCalendarApp\AgendaApi\AgendaApi
dotnet publish -c Release -o ./publish

# Transfer and deploy
scp -r publish/* your-user@your-server-ip:/tmp/calendar-api-update/
ssh your-user@your-server-ip
sudo systemctl stop calendar-api.service
sudo rm -rf /var/www/calendar-api/*
sudo mv /tmp/calendar-api-update/* /var/www/calendar-api/
# Restore appsettings.Production.json if needed
sudo chown -R www-data:www-data /var/www/calendar-api
sudo systemctl start calendar-api.service
```

## Security Considerations

1. **Secure your appsettings.Production.json** - Contains sensitive database credentials and JWT secrets
2. **Keep .NET runtime updated** - Regularly update to get security patches
3. **Use strong JWT secrets** - Minimum 32 characters, randomly generated
4. **Database security** - Use strong passwords and limit database user permissions
5. **HTTPS only** - Ensure your Apache configuration redirects HTTP to HTTPS
6. **Firewall** - Only expose necessary ports (80, 443)

## Your Current Setup

Based on your existing server configuration:
- Main app: sebaslive.xyz (root)
- Calendar app: sebaslive.xyz/calendar (new)
- API service likely running on a different port (check your existing systemd services)
- Calendar API: Running on port 5001
