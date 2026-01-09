# Authentication Setup Guide

## Implementation Complete! ✅

The Google OAuth authentication system has been fully implemented. Here's what you need to do to get it working:

---

## Step 1: Create Google Cloud OAuth Credentials

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project or select an existing one:
   - Click the project dropdown at the top
   - Click "New Project"
   - Name it "IllAdvisedCalendar" (or whatever you prefer)
   - Click "Create"

3. Enable the Google+ API:
   - In the left sidebar, go to "APIs & Services" > "Library"
   - Search for "Google+ API" or "Google Identity"
   - Click on it and click "Enable"

4. Create OAuth 2.0 Credentials:
   - Go to "APIs & Services" > "Credentials"
   - Click "Create Credentials" > "OAuth client ID"
   - If prompted, configure the OAuth consent screen first:
     - Choose "External" user type
     - Fill in app name: "IllAdvisedCalendar"
     - Add your email as support email
     - Add your email as developer contact
     - Click "Save and Continue" through the scopes and test users screens

5. Configure OAuth Client:
   - Application type: "Web application"
   - Name: "IllAdvisedCalendar Web Client"
   - Authorized JavaScript origins:
     - `http://localhost:5173`
     - `http://localhost:5174`
     - (Add your production domain when you deploy)
   - Authorized redirect URIs:
     - `http://localhost:5173`
     - `http://localhost:5174`
   - Click "Create"

6. **Copy Your Client ID** - You'll see a popup with your Client ID and Client Secret. Copy the **Client ID** (NOT the secret for frontend use).

---

## Step 2: Configure Your Application

### Backend Configuration

Update this file: `AgendaApi/AgendaApi/appsettings.Development.json`

Replace `YOUR_GOOGLE_CLIENT_ID_HERE` with your actual Google Client ID:

```json
{
  "Google": {
    "ClientId": "YOUR_ACTUAL_CLIENT_ID_HERE.apps.googleusercontent.com"
  }
}
```

### Frontend Configuration

Update this file: `AgendaFrontend/src/views/LoginView.vue`

Find line 36 and replace the placeholder:

```typescript
client_id: 'YOUR_ACTUAL_CLIENT_ID_HERE.apps.googleusercontent.com',
```

---

## Step 3: Test the Authentication Flow

1. **Start the Backend:**
   ```bash
   cd C:\Users\sebastiaan\source\repos\illAdvisedCalendarApp\AgendaApi\AgendaApi
   dotnet run
   ```
   Should start on `https://localhost:44385`

2. **Start the Frontend:**
   ```bash
   cd C:\Users\sebastiaan\source\repos\illAdvisedCalendarApp\AgendaFrontend
   yarn dev
   ```
   Should start on `http://localhost:5173`

3. **Test the Flow:**
   - Navigate to `http://localhost:5173`
   - You should be automatically redirected to `/login`
   - Click the "Sign in with Google" button
   - Choose your Google account
   - You should be redirected to `/agenda` after successful login
   - Your existing events should now be visible (they're assigned to the admin user)

---

## What Was Implemented

### Backend:
- ✅ Users and RefreshTokens database tables
- ✅ JWT token generation and validation
- ✅ Google OAuth token validation
- ✅ Auth endpoints: `/api/Auth/google-login`, `/api/Auth/refresh`, `/api/Auth/me`, `/api/Auth/logout`
- ✅ Protected Events endpoints (require authentication)
- ✅ User-specific event filtering

### Frontend:
- ✅ `useAuth` composable for auth state management
- ✅ LoginView with Google Sign-In button
- ✅ JWT token injection into API requests
- ✅ Router guards (redirect to login if not authenticated)
- ✅ Token storage in localStorage
- ✅ Auto-refresh token support

---

## Security Notes

### Current Setup (Development):
- Access tokens expire after 1 hour
- Refresh tokens expire after 30 days
- Tokens stored in localStorage (acceptable for development)
- JWT secret key in appsettings (change before production!)

### Before Going Production:
1. Generate a strong, random JWT secret key
2. Store secrets in environment variables or Azure Key Vault
3. Enable HTTPS everywhere
4. Consider HttpOnly cookies instead of localStorage
5. Add rate limiting to auth endpoints
6. Update CORS to only allow your production domain

---

## Troubleshooting

### "Invalid Client ID" Error:
- Make sure you copied the full Client ID including `.apps.googleusercontent.com`
- Check that JavaScript origins match exactly (`http://localhost:5173`)

### "Unauthorized" on API Calls:
- Check browser console for token
- Verify backend is running and accessible
- Check that `appsettings.Development.json` has correct Google Client ID

### Login Redirects Back to Login:
- Check browser console for errors
- Verify Google Sign-In script loaded (check Network tab)
- Make sure you're using HTTP (not HTTPS) for localhost

---

## Next Steps After Testing

1. Create a logout button in your calendar UI
2. Display user profile picture/name in header
3. Add user profile settings page
4. Implement the public calendar sharing feature
5. Add image upload for month banners

---

## Need Help?

If something isn't working:
1. Check browser console for errors
2. Check backend terminal for API errors
3. Verify all tokens are being stored in localStorage
4. Try clearing localStorage and logging in again
