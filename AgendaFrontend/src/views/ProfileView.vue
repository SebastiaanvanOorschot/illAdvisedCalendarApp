<template>
  <div class="profile-page">
    <div class="profile-header">
      <button class="back-button" @click="$router.push('/agenda')">
        <svg xmlns="http://www.w3.org/2000/svg" height="24" viewBox="0 -960 960 960" width="24" fill="currentColor">
          <path d="m313-440 224 224-57 56-320-320 320-320 57 56-224 224h487v80H313Z"/>
        </svg>
        Back to Calendar
      </button>
      <h1>Profile</h1>
    </div>

    <div class="profile-content">
      <div class="profile-card">
        <div class="profile-avatar-section">
          <img
            v-if="user?.profilePictureUrl"
            :src="user.profilePictureUrl"
            :alt="user.name"
            class="profile-avatar-large"
          />
          <div v-else class="profile-avatar-large-placeholder">
            {{ userInitials }}
          </div>
        </div>

        <div class="profile-info">
          <h2>{{ user?.name }}</h2>
          <p class="profile-email">{{ user?.email }}</p>
        </div>

        <div class="profile-section">
          <h3>Account Settings</h3>
          <p class="coming-soon">Profile settings coming soon...</p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useAuth } from '@/composables/useAuth';

const { user } = useAuth();

const userInitials = computed(() => {
  if (!user.value?.name) return '?';
  const names = user.value.name.split(' ');
  if (names.length >= 2) {
    return (names[0][0] + names[names.length - 1][0]).toUpperCase();
  }
  return user.value.name.substring(0, 2).toUpperCase();
});
</script>

<style scoped>
.profile-page {
  min-height: 100vh;
  background-color: var(--color-bg-subtle-2);
}

.profile-header {
  background: white;
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

.profile-header h1 {
  margin: 0;
  font-size: 28px;
  color: var(--color-text);
}

.profile-content {
  max-width: 1600px;
  margin: 0 auto;
  padding: 0 20px 40px;
}

.profile-card {
  background: white;
  border-radius: 12px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
  padding: 128px 264px;
}

.profile-avatar-section {
  display: flex;
  justify-content: center;
  margin-bottom: 24px;
}

.profile-avatar-large {
  width: 120px;
  height: 120px;
  border-radius: 50%;
  object-fit: cover;
}

.profile-avatar-large-placeholder {
  width: 120px;
  height: 120px;
  border-radius: 50%;
  background: linear-gradient(135deg, var(--color-accent) 0%, var(--color-accent-2) 100%);
  color: white;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 700;
  font-size: 48px;
}

.profile-info {
  text-align: center;
  margin-bottom: 32px;
  padding-bottom: 32px;
  border-bottom: 1px solid var(--color-border);
}

.profile-info h2 {
  margin: 0 0 8px 0;
  font-size: 24px;
  color: var(--color-text);
}

.profile-email {
  margin: 0;
  color: var(--color-text-muted);
  font-size: 16px;
}

.profile-section {
  margin-top: 24px;
}

.profile-section h3 {
  margin: 0 0 16px 0;
  font-size: 18px;
  color: var(--color-text);
}

.coming-soon {
  color: var(--color-text-subtle);
  font-style: italic;
  text-align: center;
  padding: 40px 0;
}
</style>
