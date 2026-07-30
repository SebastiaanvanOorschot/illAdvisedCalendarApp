<template>
  <div class="calendar-subscriptions">
    <div class="subscriptions-list" v-if="validSubscriptions.length > 0">
      <div
        v-for="subscription in validSubscriptions"
        :key="subscription.id"
        class="subscription-item"
        :class="{ inactive: !subscription.isActive }"
      >
        <div class="subscription-header">
          <div class="subscription-info">
            <div class="subscription-name-row">
              <span
                class="subscription-color"
                :style="{ backgroundColor: subscription.color || '#667eea' }"
              ></span>
              <h4>{{ subscription.name }}</h4>
            </div>
            <p class="subscription-url">{{ getTruncatedUrl(subscription.iCalUrl ?? '') }}</p>
            <p class="subscription-meta">
              <span v-if="subscription.lastSyncedAt">
                Last synced: {{ formatLastSync(subscription.lastSyncedAt) }}
              </span>
              <span v-else class="not-synced">Not synced yet</span>
              <span v-if="subscription.lastSyncError" class="sync-error">
                • {{ subscription.lastSyncError }}
              </span>
            </p>
          </div>
          <div class="subscription-actions">
            <button
              @click="toggleSubscription(subscription)"
              class="btn-icon"
              :title="subscription.isActive ? 'Deactivate' : 'Activate'"
            >
              <svg v-if="subscription.isActive" xmlns="http://www.w3.org/2000/svg" height="20" viewBox="0 -960 960 960" width="20" fill="currentColor">
                <path d="M480-320q75 0 127.5-52.5T660-500q0-75-52.5-127.5T480-680q-75 0-127.5 52.5T300-500q0 75 52.5 127.5T480-320Zm0-72q-45 0-76.5-31.5T372-500q0-45 31.5-76.5T480-608q45 0 76.5 31.5T588-500q0 45-31.5 76.5T480-392Zm0 192q-146 0-266-81.5T40-500q54-137 174-218.5T480-800q146 0 266 81.5T920-500q-54 137-174 218.5T480-200Z"/>
              </svg>
              <svg v-else xmlns="http://www.w3.org/2000/svg" height="20" viewBox="0 -960 960 960" width="20" fill="currentColor">
                <path d="m644-428-58-58q9-47-27-88t-93-32l-58-58q17-8 34.5-12t37.5-4q75 0 127.5 52.5T660-500q0 20-4 37.5T644-428Zm128 126-58-56q38-29 67.5-63.5T832-500q-50-101-143.5-160.5T480-720q-29 0-57 4t-55 12l-62-62q41-17 84-25.5t90-8.5q151 0 269 83.5T920-500q-23 59-60.5 109.5T772-302Zm20 246L624-222q-35 11-70.5 16.5T480-200q-151 0-269-83.5T40-500q21-53 53-98.5t73-81.5L56-792l56-56 736 736-56 56ZM222-624q-29 26-53 57t-41 67q50 101 143.5 160.5T480-280q20 0 39-2.5t39-5.5l-36-38q-11 3-21 4.5t-21 1.5q-75 0-127.5-52.5T300-500q0-11 1.5-21t4.5-21l-84-82Z"/>
              </svg>
            </button>
            <button
              @click="syncSubscription(subscription)"
              class="btn-icon"
              :disabled="syncing === subscription.id || !subscription.isActive"
              title="Sync now"
            >
              <svg xmlns="http://www.w3.org/2000/svg" height="20" viewBox="0 -960 960 960" width="20" fill="currentColor" :class="{ spinning: syncing === subscription.id }">
                <path d="M160-160v-80h110l-16-14q-52-46-73-105t-21-119q0-111 66.5-197.5T400-790v84q-72 26-116 88.5T240-478q0 45 17 87.5t53 78.5l10 10v-98h80v240H160Zm400-10v-84q72-26 116-88.5T720-482q0-45-17-87.5T650-648l-10-10v98h-80v-240h240v80H690l16 14q49 49 71.5 106.5T800-482q0 111-66.5 197.5T560-170Z"/>
              </svg>
            </button>
            <button
              @click="editSubscription(subscription)"
              class="btn-icon"
              title="Edit"
            >
              <svg xmlns="http://www.w3.org/2000/svg" height="20" viewBox="0 -960 960 960" width="20" fill="currentColor">
                <path d="M200-200h57l391-391-57-57-391 391v57Zm-80 80v-170l528-527q12-11 26.5-17t30.5-6q16 0 31 6t26 18l55 56q12 11 17.5 26t5.5 30q0 16-5.5 30.5T817-647L290-120H120Zm640-584-56-56 56 56Zm-141 85-28-29 57 57-29-28Z"/>
              </svg>
            </button>
            <button
              @click="confirmDelete(subscription)"
              class="btn-icon btn-danger"
              title="Delete"
            >
              <svg xmlns="http://www.w3.org/2000/svg" height="20" viewBox="0 -960 960 960" width="20" fill="currentColor">
                <path d="M280-120q-33 0-56.5-23.5T200-200v-520h-40v-80h200v-40h240v40h200v80h-40v520q0 33-23.5 56.5T680-120H280Zm400-600H280v520h400v-520ZM360-280h80v-360h-80v360Zm160 0h80v-360h-80v360ZM280-720v520-520Z"/>
              </svg>
            </button>
          </div>
        </div>
      </div>
    </div>

    <div v-else class="no-subscriptions">
      <p>No calendar subscriptions yet. Add your first one below!</p>
    </div>

    <button @click="showAddForm = true" class="btn-add-subscription" v-if="!showAddForm">
      <svg xmlns="http://www.w3.org/2000/svg" height="20" viewBox="0 -960 960 960" width="20" fill="currentColor">
        <path d="M440-440H200v-80h240v-240h80v240h240v80H520v240h-80v-240Z"/>
      </svg>
      Add Calendar Subscription
    </button>

    <!-- Add/Edit Form -->
    <div v-if="showAddForm || editingSubscription" class="subscription-form">
      <h4>{{ editingSubscription ? 'Edit' : 'Add' }} Calendar Subscription</h4>

      <div class="form-group">
        <label>Calendar Name *</label>
        <input
          v-model="formData.name"
          type="text"
          placeholder="e.g., School Agenda, Work Calendar"
          maxlength="200"
        />
      </div>

      <div class="form-group">
        <label>iCal URL *</label>
        <input
          v-model="formData.iCalUrl"
          type="url"
          placeholder="https://calendar.example.com/ical/..."
        />
        <p class="form-hint">Paste the iCal/ICS subscription URL from your calendar provider</p>
      </div>

      <div class="form-group">
        <label>Color</label>
        <div class="color-options">
          <button
            v-for="color in colorOptions"
            :key="color"
            type="button"
            class="color-option"
            :class="{ selected: formData.color === color }"
            :style="{ backgroundColor: color }"
            @click="formData.color = color"
            :title="color"
          ></button>
        </div>
      </div>

      <div class="form-group">
        <label>Sync Interval (minutes)</label>
        <input
          v-model.number="formData.syncIntervalMinutes"
          type="number"
          min="15"
          max="1440"
          placeholder="60"
        />
        <p class="form-hint">How often to check for updates (15-1440 minutes)</p>
      </div>

      <div class="form-actions">
        <button @click="cancelForm" class="btn-cancel">Cancel</button>
        <button @click="saveSubscription" class="btn-save" :disabled="!isFormValid">
          {{ editingSubscription ? 'Save Changes' : 'Add Subscription' }}
        </button>
      </div>

      <p v-if="error" class="error-message">{{ error }}</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { AgendaAPI, CalendarSubscriptionRequest, CalendarSubscription } from '@/api/agenda-api-swagger';
import { authenticatedAxios, getApiBaseUrl } from '@/api/axios-config';

const subscriptions = ref<CalendarSubscription[]>([]);
const showAddForm = ref(false);
const editingSubscription = ref<CalendarSubscription | null>(null);
const syncing = ref<number | null>(null);
const error = ref('');

const formData = ref({
  name: '',
  iCalUrl: '',
  color: '#0000FF',
  syncIntervalMinutes: 60
});

const api = new AgendaAPI(getApiBaseUrl(), authenticatedAxios);

// Color options matching Google Calendar style
const colorOptions = [
  '#000000', // Black
  '#FF0000', // Red
  '#00FF00', // Green
  '#0000FF', // Blue
  '#FFFF00', // Yellow
  '#FF00FF', // Magenta
  '#00FFFF', // Cyan
  '#FF8800', // Orange
  '#8800FF', // Purple
];

const isFormValid = computed(() => {
  return formData.value.name.trim() !== '' && formData.value.iCalUrl.trim() !== '';
});

const validSubscriptions = computed(() =>
  subscriptions.value.filter((s): s is CalendarSubscription & { id: number } => s.id != null)
);

async function loadSubscriptions() {
  try {
    subscriptions.value = await api.calendarSubscriptionAll();
  } catch (err: any) {
    console.error('Failed to load subscriptions:', err);
    error.value = 'Failed to load subscriptions';
  }
}

function getTruncatedUrl(url: string): string {
  if (url.length <= 50) return url;
  return url.substring(0, 47) + '...';
}

function formatLastSync(date: Date): string {
  const now = new Date();
  const syncDate = new Date(date);
  const diffMs = now.getTime() - syncDate.getTime();
  const diffMins = Math.floor(diffMs / 60000);

  if (diffMins < 1) return 'Just now';
  if (diffMins < 60) return `${diffMins} min ago`;

  const diffHours = Math.floor(diffMins / 60);
  if (diffHours < 24) return `${diffHours} hour${diffHours > 1 ? 's' : ''} ago`;

  const diffDays = Math.floor(diffHours / 24);
  return `${diffDays} day${diffDays > 1 ? 's' : ''} ago`;
}

async function syncSubscription(subscription: CalendarSubscription) {
  syncing.value = subscription.id ?? null;
  error.value = '';

  try {
    await api.sync(subscription.id!);
    await loadSubscriptions();
  } catch (err: any) {
    console.error('Failed to sync subscription:', err);
    error.value = 'Failed to sync subscription';
  } finally {
    syncing.value = null;
  }
}

async function toggleSubscription(subscription: CalendarSubscription) {
  try {
    await api.toggle(subscription.id!);
    await loadSubscriptions();
  } catch (err: any) {
    console.error('Failed to toggle subscription:', err);
    error.value = 'Failed to toggle subscription';
  }
}

function editSubscription(subscription: CalendarSubscription) {
  editingSubscription.value = subscription;
  formData.value = {
    name: subscription.name ?? '',
    iCalUrl: subscription.iCalUrl ?? '',
    color: subscription.color || '#0000FF',
    syncIntervalMinutes: subscription.syncIntervalMinutes || 60
  };
  showAddForm.value = false;
  error.value = ''; // Clear any previous errors
}

function confirmDelete(subscription: CalendarSubscription) {
  if (confirm(`Are you sure you want to delete "${subscription.name}"? All imported events will be removed.`)) {
    deleteSubscription(subscription);
  }
}

async function deleteSubscription(subscription: CalendarSubscription) {
  try {
    await api.calendarSubscriptionDELETE(subscription.id!);
    await loadSubscriptions();
    error.value = ''; // Clear any errors on success
  } catch (err: any) {
    console.error('Failed to delete subscription:', err);
    error.value = err.response?.data?.error || err.message || 'Failed to delete subscription';
  }
}

async function saveSubscription() {
  error.value = '';

  const request = new CalendarSubscriptionRequest({
    name: formData.value.name.trim(),
    iCalUrl: formData.value.iCalUrl.trim(),
    color: formData.value.color || undefined,
    syncIntervalMinutes: formData.value.syncIntervalMinutes || 60
  });

  try {
    if (editingSubscription.value) {
      await api.calendarSubscriptionPUT(editingSubscription.value.id!, request);
    } else {
      await api.calendarSubscriptionPOST(request);
    }

    await loadSubscriptions();
    cancelForm();
  } catch (err: any) {
    console.error('Failed to save subscription:', err);

    // Check if this is actually an error or just a non-200 status code
    // Status 201 (Created) is a success, not an error
    if (err.status === 201 || err.response?.status === 201) {
      // This is actually a success, reload subscriptions and clear form
      await loadSubscriptions();
      cancelForm();
      return;
    }

    // Extract error message from various possible error structures
    const errorMessage = err.response?.data?.error
      || err.response?.data?.message
      || err.message
      || 'Failed to save subscription';
    error.value = errorMessage;
  }
}

function cancelForm() {
  showAddForm.value = false;
  editingSubscription.value = null;
  formData.value = {
    name: '',
    iCalUrl: '',
    color: '#0000FF',
    syncIntervalMinutes: 60
  };
  error.value = '';
}

onMounted(() => {
  loadSubscriptions();
});
</script>

<style scoped>
.calendar-subscriptions {
  margin-top: 16px;
}

.subscriptions-list {
  margin-bottom: 20px;
}

.subscription-item {
  background: #f8f9fa;
  border-radius: 8px;
  padding: 16px;
  margin-bottom: 12px;
  transition: opacity 0.2s;
}

.subscription-item.inactive {
  opacity: 0.6;
}

.subscription-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 16px;
}

.subscription-info {
  flex: 1;
}

.subscription-name-row {
  display: flex;
  align-items: center;
  gap: 10px;
  margin-bottom: 6px;
}

.subscription-color {
  width: 16px;
  height: 16px;
  border-radius: 4px;
  flex-shrink: 0;
}

.subscription-info h4 {
  margin: 0;
  font-size: 16px;
  font-weight: 600;
  color: #333;
}

.subscription-url {
  margin: 4px 0;
  font-size: 13px;
  color: #666;
  font-family: monospace;
}

.subscription-meta {
  margin: 4px 0 0 0;
  font-size: 12px;
  color: #999;
}

.not-synced {
  color: #ff9800;
}

.sync-error {
  color: #f44336;
}

.subscription-actions {
  display: flex;
  gap: 8px;
}

.btn-icon {
  background: white;
  border: 1px solid #ddd;
  border-radius: 6px;
  width: 36px;
  height: 36px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.2s;
  padding: 0;
}

.btn-icon:hover:not(:disabled) {
  background: #f5f5f5;
  border-color: #667eea;
}

.btn-icon:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.btn-icon.btn-danger:hover {
  background: #fee;
  border-color: #f44336;
  color: #f44336;
}

.btn-icon svg {
  display: block;
}

.spinning {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

.no-subscriptions {
  text-align: center;
  padding: 40px 20px;
  color: #999;
}

.btn-add-subscription {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  width: 100%;
  padding: 12px;
  background: #667eea;
  color: white;
  border: none;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: background 0.2s;
}

.btn-add-subscription:hover {
  background: #5568d3;
}

.subscription-form {
  background: #f8f9fa;
  border-radius: 8px;
  padding: 20px;
  margin-top: 20px;
}

.subscription-form h4 {
  margin: 0 0 20px 0;
  font-size: 16px;
  font-weight: 600;
  color: #333;
}

.form-group {
  margin-bottom: 16px;
}

.form-group label {
  display: block;
  margin-bottom: 6px;
  font-size: 13px;
  font-weight: 500;
  color: #555;
}

.form-group input[type="text"],
.form-group input[type="url"],
.form-group input[type="number"] {
  width: 100%;
  padding: 10px 12px;
  border: 1px solid #ddd;
  border-radius: 6px;
  font-size: 14px;
  box-sizing: border-box;
}

.form-group input:focus {
  outline: none;
  border-color: #667eea;
}

.color-options {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.color-option {
  width: 32px;
  height: 32px;
  border-radius: 6px;
  border: 2px solid #e0e0e0;
  cursor: pointer;
  transition: all 0.2s;
  padding: 0;
  flex-shrink: 0;
  box-sizing: border-box;
}

.color-option:hover {
  transform: scale(1.1);
  border-color: #999;
}

.color-option.selected {
  border-color: #4285f4;
  border-width: 3px;
  box-shadow: 0 0 0 2px rgba(66, 133, 244, 0.2);
}

.form-hint {
  margin: 6px 0 0 0;
  font-size: 12px;
  color: #999;
}

.form-actions {
  display: flex;
  gap: 12px;
  margin-top: 20px;
}

.btn-cancel,
.btn-save {
  flex: 1;
  padding: 10px;
  border-radius: 6px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-cancel {
  background: white;
  border: 1px solid #ddd;
  color: #666;
}

.btn-cancel:hover {
  background: #f5f5f5;
}

.btn-save {
  background: #667eea;
  border: none;
  color: white;
}

.btn-save:hover:not(:disabled) {
  background: #5568d3;
}

.btn-save:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.error-message {
  margin: 12px 0 0 0;
  padding: 10px;
  background: #fee;
  border: 1px solid #fcc;
  border-radius: 6px;
  color: #c33;
  font-size: 13px;
}

@media screen and (max-width: 800px) {
  .subscription-header {
    flex-direction: column;
  }

  .subscription-actions {
    width: 100%;
    justify-content: flex-end;
  }

  .subscription-form {
    padding: 16px;
  }

  .form-actions {
    flex-direction: column;
  }

  .btn-cancel,
  .btn-save {
    width: 100%;
  }
}
</style>
