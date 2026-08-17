<template>
  <div class="calendar calendar-settings-page">
    <div class="calendar-settings-header">
      <button class="back-button" @click="$router.push('/agenda')">
        <svg xmlns="http://www.w3.org/2000/svg" height="24" viewBox="0 -960 960 960" width="24" fill="currentColor">
          <path d="m313-440 224 224-57 56-320-320 320-320 57 56-224 224h487v80H313Z"/>
        </svg>
        Back to Calendar
      </button>
      <h1>Calendar Settings</h1>
    </div>

    <div class="calendar-settings-content">
      <div class="calendar-settings-card">
        <div class="calendar-settings-section">
          <h3>Preferences</h3>
          <p class="section-description">Customize your calendar display and behavior</p>

          <!-- Calendar Preferences Component -->
          <CalendarPreferences />
        </div>

        <div class="calendar-settings-section">
          <h3>Sharing</h3>
          <p class="section-description">Share your calendar with others or access shared calendars</p>

          <!-- Calendar Sharing Component -->
          <CalendarSharing />
        </div>

        <div class="calendar-settings-section">
          <h3>Other Calendars</h3>
          <p class="section-description">Connect and import events from other calendar services</p>

          <!-- Google Calendar Sync Component -->
          <GoogleCalendarSync />

          <!-- iCal Subscriptions Component -->
          <div class="subsection-divider"></div>
          <h4 class="subsection-title">Calendar Subscriptions</h4>
          <p class="subsection-description">Subscribe to external calendars (e.g., school agenda, work calendar) using iCal/ICS URLs</p>
          <CalendarSubscriptions />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import GoogleCalendarSync from '@/components/calendar/GoogleCalendarSync.vue';
import CalendarSharing from '@/components/calendar/CalendarSharing.vue';
import CalendarPreferences from '@/components/calendar/CalendarPreferences.vue';
import CalendarSubscriptions from '@/components/calendar/CalendarSubscriptions.vue';

// OAuth callback is now handled by GoogleCalendarSync component
</script>

<style scoped>
.calendar-settings-page {
  /* Inherits width from .calendar class (55vw desktop, 90vw tablet, 100vw mobile) */
  min-height: 100vh;
  background-color: var(--color-bg-subtle-2);
  /* Override calendar's box-shadow */
  box-shadow: none !important;
  /* Allow scrolling when content is long */
  overflow-y: auto;
  overflow-x: hidden;
}

.calendar-settings-header {
  background: var(--color-surface);
  padding: 20px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
  margin-bottom: 24px;
}

.back-button {
  display: flex;
  align-items: center;
  gap: 8px;
  background: none;
  border: none;
  cursor: pointer;
  color: var(--color-accent);
  font-size: 14px;
  font-weight: 500;
  padding: 8px 12px;
  border-radius: 6px;
  transition: background-color 0.2s;
  margin-bottom: 12px;
}

.back-button:hover {
  background-color: var(--color-bg-muted);
}

.calendar-settings-header h1 {
  margin: 0;
  font-size: 28px;
  color: var(--color-text);
}

.calendar-settings-content {
  padding: 0 20px 40px;
}

.calendar-settings-card {
  background: var(--color-surface);
  border-radius: 12px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
  padding: 48px;
}

.calendar-settings-section {
  margin-bottom: 48px;
}

.calendar-settings-section:last-child {
  margin-bottom: 0;
}

.calendar-settings-section h3 {
  margin: 0 0 8px 0;
  font-size: 20px;
  color: var(--color-text);
  font-weight: 600;
}

.section-description {
  margin: 0 0 24px 0;
  color: var(--color-text-muted);
  font-size: 14px;
}

.subsection-divider {
  height: 1px;
  background: var(--color-border);
  margin: 32px 0 24px 0;
}

.subsection-title {
  margin: 0 0 6px 0;
  font-size: 16px;
  color: var(--color-text);
  font-weight: 600;
}

.subsection-description {
  margin: 0 0 16px 0;
  color: var(--color-text-muted);
  font-size: 13px;
}

/* Mobile responsive adjustments */
@media screen and (max-width: 800px) {
  .calendar-settings-page {
    min-height: 100vh;
  }

  .calendar-settings-header {
    padding: 12px 16px;
    margin-bottom: 16px;
  }

  .calendar-settings-header h1 {
    font-size: 22px;
  }

  .back-button {
    font-size: 13px;
    padding: 6px 10px;
    margin-bottom: 8px;
  }

  .back-button svg {
    width: 20px;
    height: 20px;
  }

  .calendar-settings-content {
    padding: 0 12px 24px;
  }

  .calendar-settings-card {
    padding: 20px;
    border-radius: 8px;
  }

  .calendar-settings-section {
    margin-bottom: 32px;
  }

  .calendar-settings-section h3 {
    font-size: 18px;
  }

  .section-description {
    font-size: 13px;
  }
}
</style>
