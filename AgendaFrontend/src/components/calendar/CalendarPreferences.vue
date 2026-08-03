<template>
  <div class="preferences-section">
    <div v-if="loading" class="loading">Loading preferences...</div>
    <div v-else-if="error" class="error-message">{{ error }}</div>
    <div v-else class="preferences-container">
      <!-- Month View Display Preference -->
      <div class="preference-item">
        <div class="preference-header">
          <label class="preference-label">Month View Event Display</label>
          <p class="preference-description">Choose what to show in the event preview on month view calendar</p>
        </div>
        <div class="preference-options">
          <label class="radio-option" :class="{ active: !preferences.showEventTitleInMonthView }">
            <input
              type="radio"
              name="monthViewDisplay"
              :value="false"
              v-model="preferences.showEventTitleInMonthView"
              @change="savePreference"
            />
            <div class="radio-content">
              <span class="radio-title">Show Time</span>
              <span class="radio-subtitle">Display event start time (e.g., "14:30")</span>
            </div>
          </label>
          <label class="radio-option" :class="{ active: preferences.showEventTitleInMonthView }">
            <input
              type="radio"
              name="monthViewDisplay"
              :value="true"
              v-model="preferences.showEventTitleInMonthView"
              @change="savePreference"
            />
            <div class="radio-content">
              <span class="radio-title">Show Title</span>
              <span class="radio-subtitle">Display event title instead of time</span>
            </div>
          </label>
        </div>
      </div>

      <div v-if="saveMessage" class="save-message" :class="saveStatus">
        {{ saveMessage }}
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { AgendaAPI } from '@/api/agenda-api-swagger';
import { authenticatedAxios, getApiBaseUrl } from '@/api/axios-config';

interface UserPreferences {
  showEventTitleInMonthView: boolean;
}

const api = new AgendaAPI(getApiBaseUrl(), authenticatedAxios);
const loading = ref(true);
const error = ref<string | null>(null);
const saveMessage = ref<string | null>(null);
const saveStatus = ref<'success' | 'error'>('success');

const preferences = ref<UserPreferences>({
  showEventTitleInMonthView: false
});

async function loadPreferences() {
  try {
    loading.value = true;
    error.value = null;

    const response = await authenticatedAxios.get(`${getApiBaseUrl()}/api/UserPreferences`);
    preferences.value = response.data;
  } catch (err: any) {
    console.error('Failed to load preferences:', err);
    error.value = 'Failed to load preferences. Please try again.';
  } finally {
    loading.value = false;
  }
}

async function savePreference() {
  try {
    saveMessage.value = null;

    await authenticatedAxios.put(`${getApiBaseUrl()}/api/UserPreferences`, {
      showEventTitleInMonthView: preferences.value.showEventTitleInMonthView
    });

    saveStatus.value = 'success';
    saveMessage.value = 'Preference saved successfully!';

    // Emit event to notify parent components of preference change
    window.dispatchEvent(new CustomEvent('preferencesUpdated', {
      detail: preferences.value
    }));

    // Clear message after 3 seconds
    setTimeout(() => {
      saveMessage.value = null;
    }, 3000);
  } catch (err: any) {
    console.error('Failed to save preference:', err);
    saveStatus.value = 'error';
    saveMessage.value = 'Failed to save preference. Please try again.';
  }
}

onMounted(() => {
  loadPreferences();
});
</script>

<style scoped>
.preferences-section {
  margin-top: 20px;
}

.loading {
  padding: 20px;
  text-align: center;
  color: var(--color-text-muted);
}

.error-message {
  padding: 12px;
  background-color: #fee;
  color: #c33;
  border-radius: 6px;
  border: 1px solid #fcc;
}

.preferences-container {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.preference-item {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.preference-header {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.preference-label {
  font-size: 16px;
  font-weight: 600;
  color: var(--color-text);
}

.preference-description {
  font-size: 14px;
  color: var(--color-text-muted);
  margin: 0;
}

.preference-options {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.radio-option {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  padding: 16px;
  border: 2px solid var(--color-border);
  border-radius: 8px;
  cursor: pointer;
  transition: all 0.2s;
  background: white;
}

.radio-option:hover {
  border-color: var(--color-accent);
  background-color: #f8f9ff;
}

.radio-option.active {
  border-color: var(--color-accent);
  background-color: #f0f2ff;
}

.radio-option input[type="radio"] {
  margin-top: 2px;
  cursor: pointer;
  width: 18px;
  height: 18px;
  flex-shrink: 0;
}

.radio-content {
  display: flex;
  flex-direction: column;
  gap: 4px;
  flex: 1;
}

.radio-title {
  font-size: 15px;
  font-weight: 600;
  color: var(--color-text);
}

.radio-subtitle {
  font-size: 13px;
  color: var(--color-text-muted);
}

.save-message {
  padding: 12px 16px;
  border-radius: 6px;
  font-size: 14px;
  font-weight: 500;
  animation: slideIn 0.3s ease-out;
}

.save-message.success {
  background-color: var(--color-success-bg);
  color: var(--color-success-text);
  border: 1px solid var(--color-success-border);
}

.save-message.error {
  background-color: var(--color-danger-bg);
  color: var(--color-danger-text);
  border: 1px solid var(--color-danger-border);
}

@keyframes slideIn {
  from {
    opacity: 0;
    transform: translateY(-10px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

/* Mobile responsive */
@media screen and (max-width: 800px) {
  .preference-label {
    font-size: 15px;
  }

  .preference-description {
    font-size: 13px;
  }

  .radio-option {
    padding: 12px;
  }

  .radio-title {
    font-size: 14px;
  }

  .radio-subtitle {
    font-size: 12px;
  }
}
</style>
