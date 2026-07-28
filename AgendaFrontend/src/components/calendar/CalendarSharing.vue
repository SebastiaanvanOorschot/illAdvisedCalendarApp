<template>
  <div class="calendar-sharing">
    <!-- Send Invite Section -->
    <div class="sharing-section">
      <h4>Share Your Calendar</h4>
      <p class="section-description">Invite others to view or edit your calendar events</p>

      <div v-if="error" class="error-message">
        {{ error }}
      </div>

      <div v-if="success" class="success-message">
        {{ success }}
      </div>

      <div class="invite-form">
        <div class="form-row">
          <input
            v-model="inviteEmail"
            type="email"
            placeholder="Enter email address"
            class="email-input"
            @keyup.enter="sendInvite"
          />
          <select v-model="invitePermission" class="permission-select">
            <option :value="0">Read Only</option>
            <option :value="1">Read & Write</option>
          </select>
          <button @click="sendInvite" class="btn-send" :disabled="!inviteEmail || sending">
            <svg v-if="!sending" xmlns="http://www.w3.org/2000/svg" height="20" viewBox="0 -960 960 960" width="20" fill="currentColor">
              <path d="M120-160v-640l760 320-760 320Zm80-120 474-200-474-200v140l240 60-240 60v140Z"/>
            </svg>
            <div v-else class="spinner-small"></div>
            {{ sending ? 'Sending...' : 'Send Invite' }}
          </button>
        </div>
      </div>
    </div>

    <!-- Pending Invites Received -->
    <div v-if="receivedInvites.length > 0" class="sharing-section">
      <h4>Invites Received</h4>
      <p class="section-description">People who want to share their calendar with you</p>

      <div class="invites-list">
        <div v-for="invite in receivedInvites" :key="invite.id" class="invite-item">
          <div class="invite-info">
            <div class="invite-from">
              <strong>{{ invite.senderUser?.name }}</strong>
              <span class="email-text">{{ invite.senderUser?.email }}</span>
            </div>
            <span class="permission-badge" :class="getPermissionClass(invite.permission)">
              {{ getPermissionText(invite.permission) }}
            </span>
          </div>
          <div class="invite-actions">
            <button @click="acceptInvite(invite.id)" class="btn-accept" :disabled="processingInvite === invite.id">
              Accept
            </button>
            <button @click="rejectInvite(invite.id)" class="btn-reject" :disabled="processingInvite === invite.id">
              Decline
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Pending Invites Sent -->
    <div v-if="sentInvites.length > 0" class="sharing-section">
      <h4>Pending Invites Sent</h4>
      <p class="section-description">Invitations waiting for response</p>

      <div class="invites-list">
        <div v-for="invite in sentInvites" :key="invite.id" class="invite-item">
          <div class="invite-info">
            <div class="invite-to">
              <strong>{{ invite.recipientEmail }}</strong>
              <span v-if="!invite.recipientUserId" class="pending-text">(Not registered yet)</span>
            </div>
            <span class="permission-badge" :class="getPermissionClass(invite.permission)">
              {{ getPermissionText(invite.permission) }}
            </span>
          </div>
          <div class="invite-actions">
            <button @click="cancelInvite(invite.id)" class="btn-cancel" :disabled="processingInvite === invite.id">
              Cancel
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Calendars Shared With Me -->
    <div v-if="sharedWithMe.length > 0" class="sharing-section">
      <h4>Calendars Shared With Me</h4>
      <p class="section-description">Calendars you can access from others</p>

      <div class="shares-list">
        <div v-for="share in sharedWithMe" :key="share.id" class="share-item">
          <div class="share-info">
            <div class="share-owner">
              <div class="owner-avatar">{{ getInitials(share.ownerUser?.name) }}</div>
              <div class="owner-details">
                <strong>{{ share.ownerUser?.name }}'s Calendar</strong>
                <span class="email-text">{{ share.ownerUser?.email }}</span>
              </div>
            </div>
            <span class="permission-badge" :class="getPermissionClass(share.permission)">
              {{ getPermissionText(share.permission) }}
            </span>
          </div>
          <div class="share-actions">
            <button @click="removeShare(share.id)" class="btn-remove" :disabled="processingShare === share.id">
              Remove
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- My Active Shares -->
    <div v-if="myShares.length > 0" class="sharing-section">
      <h4>People With Access to My Calendar</h4>
      <p class="section-description">Users who can see your events</p>

      <div class="shares-list">
        <div v-for="share in myShares" :key="share.id" class="share-item">
          <div class="share-info">
            <div class="share-owner">
              <div class="owner-avatar">{{ getInitials(share.sharedWithUser?.name) }}</div>
              <div class="owner-details">
                <strong>{{ share.sharedWithUser?.name }}</strong>
                <span class="email-text">{{ share.sharedWithUser?.email }}</span>
              </div>
            </div>
            <select
              v-model="share.permission"
              @change="updatePermission(share)"
              class="permission-select-inline"
              :disabled="processingShare === share.id"
            >
              <option :value="0">Read Only</option>
              <option :value="1">Read & Write</option>
            </select>
          </div>
          <div class="share-actions">
            <button @click="removeShare(share.id)" class="btn-remove" :disabled="processingShare === share.id">
              Remove
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Empty State -->
    <div v-if="isEmpty" class="empty-state">
      <svg xmlns="http://www.w3.org/2000/svg" height="64" viewBox="0 -960 960 960" width="64" fill="#ccc">
        <path d="M40-160v-112q0-34 17.5-62.5T104-378q62-31 126-46.5T360-440q66 0 130 15.5T616-378q29 15 46.5 43.5T680-272v112H40Zm720 0v-120q0-44-24.5-84.5T666-434q51 6 96 20.5t84 35.5q36 20 55 44.5t19 53.5v120H760ZM360-480q-66 0-113-47t-47-113q0-66 47-113t113-47q66 0 113 47t47 113q0 66-47 113t-113 47Zm400-160q0 66-47 113t-113 47q-11 0-28-2.5t-28-5.5q27-32 41.5-71t14.5-81q0-42-14.5-81T544-792q14-5 28-6.5t28-1.5q66 0 113 47t47 113Z"/>
      </svg>
      <p>No shared calendars yet</p>
      <p class="empty-subtext">Start by inviting someone to share your calendar</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import { AgendaAPI, SendInviteRequest, UpdatePermissionRequest } from '@/api/agenda-api-swagger';
import { authenticatedAxios, getApiBaseUrl } from '@/api/axios-config';

const api = new AgendaAPI(getApiBaseUrl(), authenticatedAxios);

// State
const inviteEmail = ref('');
const invitePermission = ref(0); // Default to Read Only
const sending = ref(false);
const error = ref('');
const success = ref('');
const processingInvite = ref<number | null>(null);
const processingShare = ref<number | null>(null);

const receivedInvites = ref<any[]>([]);
const sentInvites = ref<any[]>([]);
const sharedWithMe = ref<any[]>([]);
const myShares = ref<any[]>([]);

// Computed
const isEmpty = computed(() => {
  return receivedInvites.value.length === 0 &&
         sentInvites.value.length === 0 &&
         sharedWithMe.value.length === 0 &&
         myShares.value.length === 0;
});

// Methods
const loadData = async () => {
  try {
    const [received, sent, shared, shares] = await Promise.all([
      api.received(),
      api.sent(),
      api.sharedWithMe(),
      api.myShares()
    ]);

    receivedInvites.value = received || [];
    sentInvites.value = sent || [];
    sharedWithMe.value = shared || [];
    myShares.value = shares || [];
  } catch (err: any) {
    console.error('Failed to load sharing data:', err);
    error.value = 'Failed to load sharing data';
  }
};

const sendInvite = async () => {
  if (!inviteEmail.value) return;

  error.value = '';
  success.value = '';
  sending.value = true;

  try {
    await api.invitesPOST(new SendInviteRequest({
      recipientEmail: inviteEmail.value,
      permission: invitePermission.value
    }));

    success.value = `Invite sent to ${inviteEmail.value}`;
    inviteEmail.value = '';
    invitePermission.value = 0;

    // Reload data
    await loadData();

    // Clear success message after 3 seconds
    setTimeout(() => {
      success.value = '';
    }, 3000);
  } catch (err: any) {
    error.value = err.response?.data?.message || 'Failed to send invite';
  } finally {
    sending.value = false;
  }
};

const acceptInvite = async (inviteId: number) => {
  processingInvite.value = inviteId;
  error.value = '';

  try {
    await api.accept(inviteId);
    success.value = 'Invite accepted! You can now see their calendar events.';
    await loadData();

    setTimeout(() => {
      success.value = '';
    }, 3000);
  } catch (err: any) {
    error.value = err.response?.data?.message || 'Failed to accept invite';
  } finally {
    processingInvite.value = null;
  }
};

const rejectInvite = async (inviteId: number) => {
  processingInvite.value = inviteId;
  error.value = '';

  try {
    await api.reject(inviteId);
    await loadData();
  } catch (err: any) {
    error.value = err.response?.data?.message || 'Failed to reject invite';
  } finally {
    processingInvite.value = null;
  }
};

const cancelInvite = async (inviteId: number) => {
  processingInvite.value = inviteId;
  error.value = '';

  try {
    await api.invitesDELETE(inviteId);
    await loadData();
  } catch (err: any) {
    error.value = err.response?.data?.message || 'Failed to cancel invite';
  } finally {
    processingInvite.value = null;
  }
};

const removeShare = async (shareId: number) => {
  if (!confirm('Are you sure you want to remove this share?')) return;

  processingShare.value = shareId;
  error.value = '';

  try {
    await api.shares(shareId);
    await loadData();
  } catch (err: any) {
    error.value = err.response?.data?.message || 'Failed to remove share';
  } finally {
    processingShare.value = null;
  }
};

const updatePermission = async (share: any) => {
  processingShare.value = share.id;
  error.value = '';

  try {
    await api.permission(share.id, new UpdatePermissionRequest({
      permission: share.permission
    }));
    success.value = 'Permission updated successfully';

    setTimeout(() => {
      success.value = '';
    }, 2000);
  } catch (err: any) {
    error.value = err.response?.data?.message || 'Failed to update permission';
    await loadData(); // Reload to revert changes
  } finally {
    processingShare.value = null;
  }
};

const getPermissionText = (permission: number): string => {
  return permission === 1 ? 'Read & Write' : 'Read Only';
};

const getPermissionClass = (permission: number): string => {
  return permission === 1 ? 'permission-readwrite' : 'permission-readonly';
};

const getInitials = (name: string | null | undefined): string => {
  if (!name) return '?';
  const parts = name.split(' ');
  if (parts.length >= 2) {
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  }
  return name.substring(0, 2).toUpperCase();
};

onMounted(() => {
  loadData();
});
</script>

<style scoped>
.calendar-sharing {
  display: flex;
  flex-direction: column;
  gap: 32px;
}

.sharing-section {
  border-bottom: 1px solid #e0e0e0;
  padding-bottom: 24px;
}

.sharing-section:last-child {
  border-bottom: none;
  padding-bottom: 0;
}

.sharing-section h4 {
  margin: 0 0 8px 0;
  font-size: 18px;
  color: #333;
  font-weight: 600;
}

.section-description {
  margin: 0 0 16px 0;
  color: #666;
  font-size: 14px;
}

.error-message {
  background-color: #fee;
  color: #c33;
  padding: 12px 16px;
  border-radius: 6px;
  margin-bottom: 16px;
  font-size: 14px;
}

.success-message {
  background-color: #e8f5e9;
  color: #2e7d32;
  padding: 12px 16px;
  border-radius: 6px;
  margin-bottom: 16px;
  font-size: 14px;
}

/* Invite Form */
.invite-form {
  margin-top: 16px;
}

.form-row {
  display: flex;
  gap: 12px;
  align-items: center;
}

.email-input {
  flex: 1;
  padding: 10px 14px;
  border: 1px solid #ddd;
  border-radius: 6px;
  font-size: 14px;
  transition: border-color 0.2s;
}

.email-input:focus {
  outline: none;
  border-color: #667eea;
}

.permission-select {
  padding: 10px 14px;
  border: 1px solid #ddd;
  border-radius: 6px;
  font-size: 14px;
  background: white;
  cursor: pointer;
  min-width: 140px;
}

.permission-select-inline {
  padding: 6px 10px;
  border: 1px solid #ddd;
  border-radius: 4px;
  font-size: 13px;
  background: white;
  cursor: pointer;
}

.btn-send {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 10px 20px;
  background: #667eea;
  color: white;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-size: 14px;
  font-weight: 500;
  transition: background-color 0.2s;
  white-space: nowrap;
}

.btn-send:hover:not(:disabled) {
  background: #5568d3;
}

.btn-send:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

/* Lists */
.invites-list,
.shares-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.invite-item,
.share-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px;
  background: #f9f9f9;
  border-radius: 8px;
  border: 1px solid #e0e0e0;
}

.invite-info,
.share-info {
  display: flex;
  align-items: center;
  gap: 16px;
  flex: 1;
}

.invite-from,
.invite-to,
.owner-details {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.share-owner {
  display: flex;
  align-items: center;
  gap: 12px;
}

.owner-avatar {
  width: 40px;
  height: 40px;
  border-radius: 50%;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 600;
  font-size: 14px;
}

.email-text {
  font-size: 13px;
  color: #888;
}

.pending-text {
  font-size: 12px;
  color: #f59e0b;
  font-style: italic;
}

.permission-badge {
  padding: 4px 12px;
  border-radius: 12px;
  font-size: 12px;
  font-weight: 500;
  white-space: nowrap;
}

.permission-readonly {
  background: #e3f2fd;
  color: #1976d2;
}

.permission-readwrite {
  background: #fff3e0;
  color: #f57c00;
}

/* Actions */
.invite-actions,
.share-actions {
  display: flex;
  gap: 8px;
}

.btn-accept,
.btn-reject,
.btn-cancel,
.btn-remove {
  padding: 8px 16px;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-size: 13px;
  font-weight: 500;
  transition: all 0.2s;
}

.btn-accept {
  background: #4caf50;
  color: white;
}

.btn-accept:hover:not(:disabled) {
  background: #45a049;
}

.btn-reject,
.btn-cancel,
.btn-remove {
  background: #f5f5f5;
  color: #666;
  border: 1px solid #ddd;
}

.btn-reject:hover:not(:disabled),
.btn-cancel:hover:not(:disabled),
.btn-remove:hover:not(:disabled) {
  background: #e0e0e0;
  color: #333;
}

.btn-accept:disabled,
.btn-reject:disabled,
.btn-cancel:disabled,
.btn-remove:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

/* Empty State */
.empty-state {
  text-align: center;
  padding: 48px 24px;
  color: #999;
}

.empty-state svg {
  margin-bottom: 16px;
}

.empty-state p {
  margin: 8px 0;
  font-size: 16px;
  color: #666;
}

.empty-subtext {
  font-size: 14px !important;
  color: #999 !important;
}

/* Spinner */
.spinner-small {
  width: 16px;
  height: 16px;
  border: 2px solid rgba(255, 255, 255, 0.3);
  border-radius: 50%;
  border-top-color: white;
  animation: spin 0.6s linear infinite;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}

/* Responsive */
@media (max-width: 800px) {
  .calendar-sharing {
    padding: 0;
  }

  .sharing-section {
    margin-bottom: 24px;
  }

  .sharing-section h4 {
    font-size: 16px;
  }

  .section-description {
    font-size: 13px;
  }

  .form-row {
    flex-direction: column;
    align-items: stretch;
    gap: 10px;
  }

  .email-input,
  .permission-select {
    font-size: 14px;
    padding: 10px 12px;
  }

  .btn-send {
    justify-content: center;
    width: 100%;
    padding: 10px 20px;
    font-size: 13px;
  }

  .invite-item,
  .share-item {
    flex-direction: column;
    align-items: stretch;
    gap: 10px;
    padding: 12px;
  }

  .invite-info,
  .share-info {
    flex-direction: column;
    align-items: flex-start;
    gap: 10px;
  }

  .share-owner {
    width: 100%;
  }

  .avatar,
  .owner-avatar {
    width: 36px;
    height: 36px;
    font-size: 14px;
  }

  .invite-from,
  .owner-details {
    flex: 1;
    min-width: 0;
  }

  .invite-from strong,
  .share-info strong,
  .owner-details strong {
    font-size: 14px;
    display: block;
    word-break: break-word;
  }

  .email-text {
    font-size: 12px;
    display: block;
    word-break: break-all;
  }

  .permission-badge {
    font-size: 11px;
    padding: 3px 8px;
    white-space: nowrap;
    align-self: flex-start;
  }

  .invite-actions,
  .share-actions {
    justify-content: stretch;
    gap: 8px;
  }

  .btn-accept,
  .btn-decline,
  .btn-remove,
  .btn-cancel {
    flex: 1;
    justify-content: center;
    font-size: 12px;
    padding: 8px 12px;
  }

  .permission-select-inline {
    font-size: 13px;
    padding: 6px 10px;
  }

  .error-message,
  .success-message {
    font-size: 13px;
    padding: 10px 12px;
  }

  .empty-state {
    padding: 30px 20px;
    font-size: 13px;
  }
}
</style>
