<template>
  <div class="localization-page">
    <div class="localization-header">
      <button class="back-button" @click="$router.push('/agenda')">
        <svg xmlns="http://www.w3.org/2000/svg" height="24" viewBox="0 -960 960 960" width="24" fill="currentColor">
          <path d="m313-440 224 224-57 56-320-320 320-320 57 56-224 224h487v80H313Z"/>
        </svg>
        Back to Calendar
      </button>
      <h1>Localization</h1>
    </div>

    <div class="localization-content">
      <div class="localization-card">
        <div class="localization-section">
          <h3>Language & Region Settings</h3>
          <p class="coming-soon">Localization settings coming soon...</p>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
// Stub page for localization settings
</script>

<style scoped>
.localization-page {
  min-height: 100vh;
  background-color: var(--color-bg-subtle-2);
}

.localization-header {
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

.localization-header h1 {
  margin: 0;
  font-size: 28px;
  color: var(--color-text);
}

.localization-content {
  max-width: 1600px;
  margin: 0 auto;
  padding: 0 20px 40px;
}

.localization-card {
  background: var(--color-surface);
  border-radius: 12px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
  padding: 128px 264px;
}

.localization-section {
  margin-top: 24px;
}

.localization-section h3 {
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

/* Mobile responsive adjustments */
@media screen and (max-width: 800px) {
  .localization-page {
    min-height: 100vh;
  }

  .localization-header {
    padding: 12px 16px;
    margin-bottom: 16px;
  }

  .localization-header h1 {
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

  .localization-content {
    padding: 0 12px 24px;
  }

  .localization-card {
    padding: 40px 20px;
    border-radius: 8px;
  }

  .localization-section h3 {
    font-size: 16px;
  }

  .coming-soon {
    font-size: 13px;
    padding: 30px 0;
  }
}
</style>
