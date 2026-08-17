<template>
  <div class="modal-overlay" @click="$emit('close')">
    <div class="modal-content" @click.stop>
      <div class="modal-header">
        <h3>Change Month Image</h3>
        <button class="close-btn" @click="$emit('close')">
          <svg xmlns="http://www.w3.org/2000/svg" height="24" viewBox="0 -960 960 960" width="24" fill="currentColor">
            <path d="m256-200-56-56 224-224-224-224 56-56 224 224 224-224 56 56-224 224 224 224-56 56-224-224-224 224Z"/>
          </svg>
        </button>
      </div>

      <div class="modal-body">
        <p class="month-info">Uploading image for <strong>{{ monthName }}</strong></p>

        <div
          class="drop-zone"
          :class="{ 'drag-over': isDragOver }"
          @drop.prevent="handleDrop"
          @dragover.prevent="isDragOver = true"
          @dragleave="isDragOver = false"
          @click="triggerFileInput"
        >
          <div v-if="!selectedFile" class="drop-zone-content">
            <svg xmlns="http://www.w3.org/2000/svg" height="48" viewBox="0 -960 960 960" width="48" fill="currentColor">
              <path d="M260-160q-91 0-155.5-63T40-377q0-78 47-139t123-78q25-92 100-149t170-57q117 0 198.5 81.5T760-520q69 8 114.5 59.5T920-340q0 75-52.5 127.5T740-160H520q-33 0-56.5-23.5T440-240v-206l-64 62-56-56 160-160 160 160-56 56-64-62v206h220q42 0 71-29t29-71q0-42-29-71t-71-29h-60v-80q0-83-58.5-141.5T480-720q-83 0-141.5 58.5T280-520h-20q-58 0-99 41t-41 99q0 58 41 99t99 41h100v80H260Zm220-280Z"/>
            </svg>
            <p>Drop image here or click to select</p>
            <span class="file-hint">JPG, PNG, GIF, WEBP (max 10MB)</span>
          </div>

          <div v-else class="selected-file-info">
            <img v-if="previewUrl" :src="previewUrl" alt="Preview" class="image-preview" />
            <p class="file-name">{{ selectedFile.name }}</p>
            <button class="remove-file-btn" @click.stop="removeFile">Remove</button>
          </div>
        </div>

        <input
          ref="fileInput"
          type="file"
          accept="image/jpeg,image/jpg,image/png,image/gif,image/webp"
          @change="handleFileSelect"
          style="display: none"
        />

        <div v-if="error" class="error-message">
          {{ error }}
        </div>

        <div v-if="uploading" class="uploading-message">
          Uploading image...
        </div>
      </div>

      <div class="modal-footer">
        <button class="btn-secondary" @click="$emit('close')" :disabled="uploading">Cancel</button>
        <button class="btn-primary" @click="uploadImage" :disabled="!selectedFile || uploading">
          Upload
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue';
import { AgendaAPI } from '@/api/agenda-api-swagger';
import { authenticatedAxios, getApiBaseUrl } from '@/api/axios-config';

interface Props {
  month: number; // 1-12
}

const props = defineProps<Props>();
const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'uploaded'): void;
}>();

const api = new AgendaAPI(getApiBaseUrl(), authenticatedAxios);

const fileInput = ref<HTMLInputElement | null>(null);
const selectedFile = ref<File | null>(null);
const previewUrl = ref<string | null>(null);
const isDragOver = ref(false);
const uploading = ref(false);
const error = ref<string | null>(null);

const monthNames = ['January', 'February', 'March', 'April', 'May', 'June',
  'July', 'August', 'September', 'October', 'November', 'December'];

const monthName = computed(() => monthNames[props.month - 1]);

function triggerFileInput() {
  fileInput.value?.click();
}

function handleFileSelect(event: Event) {
  const target = event.target as HTMLInputElement;
  const files = target.files;
  if (files && files.length > 0) {
    validateAndSetFile(files[0]);
  }
}

function handleDrop(event: DragEvent) {
  isDragOver.value = false;
  const files = event.dataTransfer?.files;
  if (files && files.length > 0) {
    validateAndSetFile(files[0]);
  }
}

function validateAndSetFile(file: File) {
  error.value = null;

  // Check file type
  const allowedTypes = ['image/jpeg', 'image/jpg', 'image/png', 'image/gif', 'image/webp'];
  if (!allowedTypes.includes(file.type)) {
    error.value = 'Please select a valid image file (JPG, PNG, GIF, WEBP)';
    return;
  }

  // Check file size (10MB)
  const maxSize = 10 * 1024 * 1024;
  if (file.size > maxSize) {
    error.value = 'File size must be less than 10MB';
    return;
  }

  selectedFile.value = file;

  // Create preview
  const reader = new FileReader();
  reader.onload = (e) => {
    previewUrl.value = e.target?.result as string;
  };
  reader.readAsDataURL(file);
}

function removeFile() {
  selectedFile.value = null;
  previewUrl.value = null;
  if (fileInput.value) {
    fileInput.value.value = '';
  }
}

async function uploadImage() {
  if (!selectedFile.value) return;

  uploading.value = true;
  error.value = null;

  try {
    const formData = new FormData();
    formData.append('file', selectedFile.value);
    formData.append('month', props.month.toString());

    // Call API directly because NSwag generates incorrect signature for file uploads
    await authenticatedAxios.post('/api/MonthImages', formData, {
      headers: {
        'Content-Type': 'multipart/form-data'
      }
    });

    emit('uploaded');
    emit('close');
  } catch (err: any) {
    error.value = err.response?.data?.message || 'Failed to upload image';
  } finally {
    uploading.value = false;
  }
}
</script>

<style scoped>
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: rgba(0, 0, 0, 0.7);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  animation: fadeIn 0.2s ease;
}

@keyframes fadeIn {
  from {
    opacity: 0;
  }
  to {
    opacity: 1;
  }
}

.modal-content {
  background: var(--color-surface);
  border-radius: 12px;
  width: 90%;
  max-width: 500px;
  max-height: 90vh;
  overflow-y: auto;
  animation: slideUp 0.3s ease;
}

@keyframes slideUp {
  from {
    transform: translateY(50px);
    opacity: 0;
  }
  to {
    transform: translateY(0);
    opacity: 1;
  }
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 20px;
  border-bottom: 1px solid var(--color-border);
}

.modal-header h3 {
  margin: 0;
  font-size: 20px;
  color: var(--color-text);
}

.close-btn {
  background: none;
  border: none;
  cursor: pointer;
  color: var(--color-text-muted);
  padding: 4px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  transition: background-color 0.2s;
}

.close-btn:hover {
  background-color: var(--color-bg-muted);
}

.modal-body {
  padding: 20px;
}

.month-info {
  margin: 0 0 20px 0;
  color: var(--color-text-muted);
  font-size: 14px;
}

.drop-zone {
  border: 2px dashed var(--color-border-strong);
  border-radius: 8px;
  padding: 40px 20px;
  text-align: center;
  cursor: pointer;
  transition: all 0.2s;
  background-color: var(--color-bg-subtle);
}

.drop-zone:hover {
  border-color: var(--color-accent);
  background-color: var(--color-accent-tint);
}

.drop-zone.drag-over {
  border-color: var(--color-accent);
  background-color: var(--color-accent-tint-strong);
}

.drop-zone-content svg {
  color: var(--color-text-subtle);
  margin-bottom: 16px;
}

.drop-zone-content p {
  margin: 0 0 8px 0;
  font-size: 16px;
  color: var(--color-text);
}

.file-hint {
  font-size: 12px;
  color: var(--color-text-subtle);
}

.selected-file-info {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 12px;
}

.image-preview {
  max-width: 100%;
  max-height: 200px;
  border-radius: 8px;
  object-fit: contain;
}

.file-name {
  margin: 0;
  font-size: 14px;
  color: var(--color-text);
  word-break: break-all;
}

.remove-file-btn {
  background: #f44336;
  color: white;
  border: none;
  padding: 8px 16px;
  border-radius: 6px;
  cursor: pointer;
  font-size: 14px;
  transition: background-color 0.2s;
}

.remove-file-btn:hover {
  background: #d32f2f;
}

.error-message {
  margin-top: 16px;
  padding: 12px;
  background-color: #ffebee;
  color: #c62828;
  border-radius: 6px;
  font-size: 14px;
}

.uploading-message {
  margin-top: 16px;
  padding: 12px;
  background-color: #e3f2fd;
  color: #1565c0;
  border-radius: 6px;
  font-size: 14px;
  text-align: center;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  padding: 20px;
  border-top: 1px solid var(--color-border);
}

.btn-secondary,
.btn-primary {
  padding: 10px 24px;
  border-radius: 6px;
  border: none;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}

.btn-secondary {
  background: var(--color-bg-muted);
  color: var(--color-text);
}

.btn-secondary:hover:not(:disabled) {
  background: var(--color-border);
}

.btn-primary {
  background: var(--color-accent);
  color: white;
}

.btn-primary:hover:not(:disabled) {
  background: var(--color-accent-hover);
}

.btn-secondary:disabled,
.btn-primary:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
</style>
