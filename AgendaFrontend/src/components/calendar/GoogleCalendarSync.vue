<template>
  <div class="google-calendar-sync">
    <div class="sync-header">
      <div class="sync-title-row">
        <img src="https://www.google.com/calendar/images/ext/gc_button6.gif" alt="Google Calendar" class="google-logo">
        <h4>Google Calendar</h4>
      </div>
      <div class="sync-status" :class="statusClass">
        <svg v-if="isConnected" xmlns="http://www.w3.org/2000/svg" height="16" viewBox="0 -960 960 960" width="16" fill="currentColor">
          <path d="M382-240 154-468l57-57 171 171 367-367 57 57-424 424Z"/>
        </svg>
        {{ statusText }}
      </div>
    </div>

    <div v-if="error" class="error-message">
      {{ error }}
    </div>

    <div v-if="success" class="success-message">
      {{ success }}
    </div>

    <!-- Not connected state -->
    <div v-if="!isConnected" class="connect-section">
      <p class="description">
        Connect your Google Calendar to import events to IllAdvisedCalendar.
        This is a one-way sync - imported events become native events in your calendar.
      </p>
      <button @click="connectGoogleCalendar" class="btn-connect" :disabled="loading">
        <svg xmlns="http://www.w3.org/2000/svg" height="20" viewBox="0 -960 960 960" width="20" fill="currentColor">
          <path d="M440-280h80v-160h160v-80H520v-160h-80v160H280v80h160v160Zm40 200q-83 0-156-31.5T197-197q-54-54-85.5-127T80-480q0-83 31.5-156T197-763q54-54 127-85.5T480-880q83 0 156 31.5T763-763q54 54 85.5 127T880-480q0 83-31.5 156T763-197q-54 54-127 85.5T480-80Z"/>
        </svg>
        {{ loading ? 'Connecting...' : 'Connect Google Calendar' }}
      </button>
    </div>

    <!-- Connected state - Show calendars -->
    <div v-else class="calendars-section">
      <p class="description">
        Select which Google Calendars to import and assign a color to each.
      </p>

      <div v-if="loadingCalendars" class="loading-state">
        <div class="spinner"></div>
        <span>Loading your calendars...</span>
      </div>

      <div v-else-if="calendars.length > 0" class="calendars-list">
        <div v-for="calendar in calendars" :key="calendar.id" class="calendar-item">
          <label class="calendar-checkbox">
            <input type="checkbox" v-model="calendar.selected" />
            <div class="calendar-info">
              <span class="calendar-name">
                {{ calendar.name }}
                <span v-if="calendar.primary" class="primary-badge">Primary</span>
              </span>
              <span v-if="calendar.description" class="calendar-description">
                {{ calendar.description }}
              </span>
            </div>
          </label>

          <div v-if="calendar.selected" class="color-picker">
            <label>Color:</label>
            <div class="color-swatches">
              <button
                v-for="colorOption in colorOptions"
                :key="colorOption"
                :class="['color-swatch', { selected: calendar.color === colorOption }]"
                :style="{ backgroundColor: colorOption }"
                @click="calendar.color = colorOption"
                :title="colorOption"
              ></button>
            </div>
          </div>
        </div>
      </div>

      <div v-else class="no-calendars">
        No calendars found in your Google account.
      </div>

      <div class="sync-actions">
        <button
          @click="importSelectedCalendars"
          class="btn-import"
          :disabled="!hasSelectedCalendars || importing"
        >
          <svg v-if="!importing" xmlns="http://www.w3.org/2000/svg" height="20" viewBox="0 -960 960 960" width="20" fill="currentColor">
            <path d="M440-320v-326L336-542l-56-58 200-200 200 200-56 58-104-104v326h-80ZM240-160q-33 0-56.5-23.5T160-240v-120h80v120h480v-120h80v120q0 33-23.5 56.5T720-160H240Z"/>
          </svg>
          <div v-else class="spinner-small"></div>
          {{ importing ? `Importing... ${importProgress}` : 'Import Selected Calendars' }}
        </button>

        <button @click="disconnectGoogle" class="btn-disconnect" :disabled="importing">
          Disconnect
        </button>
      </div>

      <div v-if="lastSyncTime" class="last-sync">
        Last sync: {{ formatDate(lastSyncTime) }}
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useAuth } from '@/composables/useAuth';
import { AgendaAPI, GoogleCalendarInfo, ImportRequest, CalendarImportItem } from '@/api/agenda-api-swagger';
import { authenticatedAxios, getApiBaseUrl } from '@/api/axios-config';

const api = new AgendaAPI(getApiBaseUrl(), authenticatedAxios);

// Color options matching the app's palette
const colorOptions = [
  '#000000', // Black (default)
  '#FF0000', // Red
  '#00FF00', // Green
  '#0000FF', // Blue
  '#FFFF00', // Yellow
  '#FF00FF', // Magenta
  '#00FFFF', // Cyan
  '#FF8800', // Orange
  '#8800FF', // Purple
];

interface GoogleCalendar {
  id: string;
  name: string;
  description?: string;
  primary: boolean;
  selected: boolean;
  color: string;
}

const { getAccessToken } = useAuth();

const isConnected = ref(false);
const loading = ref(false);
const loadingCalendars = ref(false);
const importing = ref(false);
const calendars = ref<GoogleCalendar[]>([]);
const error = ref<string | null>(null);
const success = ref<string | null>(null);
const lastSyncTime = ref<Date | null>(null);
const importProgress = ref('');

const statusClass = computed(() => ({
  'status-connected': isConnected.value,
  'status-disconnected': !isConnected.value
}));

const statusText = computed(() => {
  if (isConnected.value) {
    return 'Connected';
  }
  return 'Not connected';
});

const hasSelectedCalendars = computed(() => {
  return calendars.value.some(c => c.selected);
});

onMounted(() => {
  // Check for OAuth callback in URL hash
  handleOAuthRedirect();

  // Check if user has already connected Google Calendar
  checkConnection();
});

const checkConnection = () => {
  // Check if user has a stored calendar token
  const hasToken = sessionStorage.getItem('google_calendar_token');
  isConnected.value = !!hasToken;

  // If connected, load calendars
  if (isConnected.value) {
    loadCalendars();
  }
};

const connectGoogleCalendar = () => {
  // Use full page redirect instead of popup to avoid browser blocking issues
  const GOOGLE_CLIENT_ID = '210057685866-ektli26tc1i7kv46ftc9bmc6eo2g1ggv.apps.googleusercontent.com';

  // Use import.meta.env.BASE_URL to match the Vite base configuration
  // In production: '/calendar/', in development: '/'
  const basePath = import.meta.env.BASE_URL;
  const REDIRECT_URI = window.location.origin + basePath + 'calendar-settings';
  const SCOPE = 'https://www.googleapis.com/auth/calendar.readonly';

  // Create OAuth URL using implicit flow
  const authUrl = new URL('https://accounts.google.com/o/oauth2/v2/auth');
  authUrl.searchParams.append('client_id', GOOGLE_CLIENT_ID);
  authUrl.searchParams.append('redirect_uri', REDIRECT_URI);
  authUrl.searchParams.append('response_type', 'token');
  authUrl.searchParams.append('scope', SCOPE);
  authUrl.searchParams.append('include_granted_scopes', 'true');
  authUrl.searchParams.append('state', 'calendar_connect');

  // Full page redirect to Google OAuth
  window.location.href = authUrl.toString();
};

const handleOAuthRedirect = async () => {
  // Check if we're returning from Google OAuth (token will be in URL hash)
  const hash = window.location.hash;

  if (!hash || !hash.includes('access_token')) {
    return;
  }

  loading.value = true;

  try {
    // Parse the hash fragment for access token
    const params = new URLSearchParams(hash.substring(1)); // Remove the # and parse
    const accessToken = params.get('access_token');
    const state = params.get('state');

    if (accessToken && state === 'calendar_connect') {
      // Send access token to backend to store in database
      await api.connect({ accessToken });

      // Store the calendar access token in sessionStorage as a marker
      sessionStorage.setItem('google_calendar_token', accessToken);
      isConnected.value = true;

      // Clear the hash from URL to clean up
      history.replaceState(null, '', window.location.pathname);

      // Load user's calendars
      await loadCalendars();

      success.value = 'Successfully connected to Google Calendar!';
      setTimeout(() => {
        success.value = null;
      }, 3000);
    }
  } catch (err: any) {
    error.value = err.message || 'Failed to connect to Google Calendar';
  } finally {
    loading.value = false;
  }
};

const loadCalendars = async () => {
  loadingCalendars.value = true;
  error.value = null;

  try {
    // Call backend API to get user's Google Calendars
    const googleCalendars = await api.calendars();

    // Convert API response to local calendar format with UI state
    calendars.value = googleCalendars.map((cal: GoogleCalendarInfo) => ({
      id: cal.id!,
      name: cal.name!,
      description: cal.description,
      primary: cal.primary || false,
      selected: cal.primary || false, // Auto-select primary calendar
      color: '#000000' // Default color
    }));

  } catch (err: any) {
    error.value = err.message || 'Failed to load calendars from Google. Please ensure you have granted calendar access.';
    console.error('Error loading calendars:', err);
  } finally {
    loadingCalendars.value = false;
  }
};

const importSelectedCalendars = async () => {
  importing.value = true;
  error.value = null;
  success.value = null;
  importProgress.value = '';

  try {
    const selectedCalendars = calendars.value.filter(c => c.selected);

    if (selectedCalendars.length === 0) {
      error.value = 'Please select at least one calendar to import';
      importing.value = false;
      return;
    }

    importProgress.value = 'Preparing import...';

    // Build import request
    const importRequest = new ImportRequest({
      calendars: selectedCalendars.map(c => new CalendarImportItem({
        calendarId: c.id,
        color: c.color
      }))
    });

    // Call backend API to import calendars
    const result = await api.import(importRequest);

    if (result.success) {
      success.value = result.message || `Successfully imported ${result.totalImported} events from ${selectedCalendars.length} calendar(s)`;
      lastSyncTime.value = new Date();

      // Clear success message after 5 seconds
      setTimeout(() => {
        success.value = null;
      }, 5000);
    } else {
      error.value = result.message || 'Import failed. Please try again.';
      if (result.errors && result.errors.length > 0) {
        console.error('Import errors:', result.errors);
      }
    }

  } catch (err: any) {
    error.value = err.message || 'Failed to import calendars. Please try again.';
    console.error('Import error:', err);
  } finally {
    importing.value = false;
    importProgress.value = '';
  }
};

const disconnectGoogle = () => {
  if (confirm('Are you sure you want to disconnect Google Calendar? Previously imported events will remain in your calendar.')) {
    sessionStorage.removeItem('google_calendar_token');
    isConnected.value = false;
    calendars.value = [];
    success.value = 'Disconnected from Google Calendar';

    setTimeout(() => {
      success.value = null;
    }, 3000);
  }
};

const formatDate = (date: Date) => {
  return new Intl.DateTimeFormat('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  }).format(date);
};
</script>

<style scoped>
.google-calendar-sync {
  border: 1px solid #e0e0e0;
  border-radius: 8px;
  padding: 24px;
  margin-top: 16px;
}

.sync-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
}

.sync-title-row {
  display: flex;
  align-items: center;
  gap: 12px;
}

.google-logo {
  height: 24px;
  width: auto;
}

.sync-header h4 {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  color: #333;
}

.sync-status {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 14px;
  font-weight: 500;
  padding: 6px 12px;
  border-radius: 16px;
}

.status-connected {
  background: #d4edda;
  color: #155724;
}

.status-disconnected {
  background: #f8d7da;
  color: #721c24;
}

.error-message {
  background: #f8d7da;
  color: #721c24;
  padding: 12px 16px;
  border-radius: 6px;
  margin-bottom: 16px;
  font-size: 14px;
  border: 1px solid #f5c6cb;
}

.success-message {
  background: #d4edda;
  color: #155724;
  padding: 12px 16px;
  border-radius: 6px;
  margin-bottom: 16px;
  font-size: 14px;
  border: 1px solid #c3e6cb;
}

.description {
  color: #666;
  font-size: 14px;
  line-height: 1.5;
  margin-bottom: 20px;
}

.btn-connect {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  background: #4285f4;
  color: white;
  border: none;
  padding: 12px 24px;
  border-radius: 6px;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  transition: background 0.2s;
}

.btn-connect:hover:not(:disabled) {
  background: #357ae8;
}

.btn-connect:disabled {
  background: #ccc;
  cursor: not-allowed;
}

.loading-state {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 24px;
  color: #666;
}

.spinner {
  width: 24px;
  height: 24px;
  border: 3px solid #f3f3f3;
  border-top: 3px solid #4285f4;
  border-radius: 50%;
  animation: spin 1s linear infinite;
}

.spinner-small {
  width: 16px;
  height: 16px;
  border: 2px solid #f3f3f3;
  border-top: 2px solid #fff;
  border-radius: 50%;
  animation: spin 1s linear infinite;
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

.calendars-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
  margin-bottom: 24px;
}

.calendar-item {
  border: 1px solid #e0e0e0;
  border-radius: 6px;
  padding: 16px;
  background: #fafafa;
}

.calendar-checkbox {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  cursor: pointer;
  margin-bottom: 12px;
}

.calendar-checkbox input[type="checkbox"] {
  margin-top: 2px;
  width: 18px;
  height: 18px;
  cursor: pointer;
}

.calendar-info {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.calendar-name {
  font-weight: 600;
  color: #333;
  font-size: 15px;
}

.primary-badge {
  display: inline-block;
  background: #4285f4;
  color: white;
  font-size: 11px;
  font-weight: 600;
  padding: 2px 8px;
  border-radius: 10px;
  margin-left: 8px;
}

.calendar-description {
  font-size: 13px;
  color: #666;
}

.color-picker {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-top: 8px;
  padding-top: 12px;
  border-top: 1px solid #e0e0e0;
}

.color-picker label {
  font-size: 14px;
  font-weight: 500;
  color: #555;
}

.color-swatches {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.color-swatch {
  width: 32px;
  height: 32px;
  border-radius: 6px;
  border: 2px solid #e0e0e0;
  cursor: pointer;
  transition: all 0.2s;
  padding: 0;
}

.color-swatch:hover {
  transform: scale(1.1);
  border-color: #999;
}

.color-swatch.selected {
  border-color: #4285f4;
  border-width: 3px;
  box-shadow: 0 0 0 2px rgba(66, 133, 244, 0.2);
}

.no-calendars {
  text-align: center;
  padding: 40px 20px;
  color: #999;
  font-style: italic;
}

.sync-actions {
  display: flex;
  gap: 12px;
  align-items: center;
}

.btn-import {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  background: #34a853;
  color: white;
  border: none;
  padding: 12px 24px;
  border-radius: 6px;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  transition: background 0.2s;
}

.btn-import:hover:not(:disabled) {
  background: #2d8e47;
}

.btn-import:disabled {
  background: #ccc;
  cursor: not-allowed;
}

.btn-disconnect {
  background: none;
  color: #666;
  border: 1px solid #e0e0e0;
  padding: 12px 24px;
  border-radius: 6px;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-disconnect:hover:not(:disabled) {
  background: #f5f5f5;
  border-color: #999;
}

.btn-disconnect:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.last-sync {
  margin-top: 16px;
  padding-top: 16px;
  border-top: 1px solid #e0e0e0;
  font-size: 13px;
  color: #666;
}

/* Mobile responsive adjustments */
@media screen and (max-width: 800px) {
  .google-calendar-sync {
    padding: 16px;
    border-radius: 6px;
  }

  .sync-header {
    flex-direction: column;
    align-items: flex-start;
    gap: 12px;
  }

  .sync-header h4 {
    font-size: 16px;
  }

  .google-logo {
    height: 20px;
  }

  .sync-status {
    font-size: 12px;
    padding: 4px 10px;
    align-self: flex-start;
  }

  .description {
    font-size: 13px;
    margin-bottom: 16px;
  }

  .btn-connect {
    width: 100%;
    justify-content: center;
    padding: 10px 20px;
    font-size: 13px;
  }

  .calendar-item {
    padding: 12px;
  }

  .calendar-checkbox {
    gap: 8px;
  }

  .calendar-name {
    font-size: 14px;
  }

  .primary-badge {
    font-size: 10px;
    padding: 2px 6px;
    margin-left: 6px;
  }

  .calendar-description {
    font-size: 12px;
  }

  .color-picker {
    flex-direction: column;
    align-items: flex-start;
    gap: 8px;
    padding-top: 8px;
  }

  .color-swatches {
    width: 100%;
    justify-content: flex-start;
  }

  .color-swatch {
    width: 28px;
    height: 28px;
  }

  .sync-actions {
    flex-direction: column;
    gap: 8px;
  }

  .btn-import,
  .btn-disconnect {
    width: 100%;
    justify-content: center;
    padding: 10px 20px;
    font-size: 13px;
  }

  .error-message,
  .success-message {
    padding: 10px 12px;
    font-size: 13px;
  }

  .last-sync {
    font-size: 12px;
  }
}
</style>
