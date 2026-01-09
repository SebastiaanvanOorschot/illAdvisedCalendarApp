<template>
    <div v-if="show" class="modal-overlay" @click="handleClose">
        <div class="modal-content" @click.stop>
            <div class="modal-header">
                <h3>Create New Event</h3>
                <button class="btn-close" @click="handleClose">&times;</button>
            </div>

            <form @submit.prevent="handleSubmit">
                <div class="form-group">
                    <label for="title">Title</label>
                    <input
                        id="title"
                        v-model="form.title"
                        type="text"
                        required
                        placeholder="Event title"
                    />
                </div>

                <div class="form-group">
                    <label for="description">Description</label>
                    <textarea
                        id="description"
                        v-model="form.description"
                        placeholder="Event description (optional)"
                        rows="3"
                    ></textarea>
                </div>

                <div class="form-row">
                    <div class="form-group">
                        <label for="startTime">Start Time</label>
                        <input
                            id="startTime"
                            v-model="form.startTime"
                            type="time"
                            required
                        />
                    </div>

                    <div class="form-group">
                        <label for="endTime">End Time</label>
                        <input
                            id="endTime"
                            v-model="form.endTime"
                            type="time"
                            required
                        />
                    </div>
                </div>

                <div class="form-group">
                    <label for="color">Event Color</label>
                    <div class="color-picker-container">
                        <div class="color-swatches">
                            <button
                                type="button"
                                v-for="colorOption in colorOptions"
                                :key="colorOption"
                                :class="['color-swatch', { selected: form.color === colorOption }]"
                                :style="{ backgroundColor: colorOption }"
                                @click="form.color = colorOption"
                                :title="colorOption"
                            ></button>
                        </div>
                    </div>
                </div>

                <div class="form-group">
                    <label class="checkbox-label">
                        <input
                            type="checkbox"
                            v-model="form.isRecurring"
                        />
                        Make this a recurring event
                    </label>
                </div>

                <div v-if="form.isRecurring" class="recurring-options">
                    <div class="form-row">
                        <div class="form-group">
                            <label for="recurrencePattern">Repeat</label>
                            <select
                                id="recurrencePattern"
                                v-model="form.recurrencePattern"
                                required
                            >
                                <option value="daily">Daily</option>
                                <option value="weekly">Weekly</option>
                                <option value="monthly">Monthly</option>
                                <option value="yearly">Yearly</option>
                            </select>
                        </div>

                        <div class="form-group">
                            <label for="recurrenceInterval">Every</label>
                            <input
                                id="recurrenceInterval"
                                v-model.number="form.recurrenceInterval"
                                type="number"
                                min="1"
                                required
                            />
                        </div>
                    </div>

                    <div class="form-group">
                        <label for="recurrenceEndDate">End Date (optional)</label>
                        <input
                            id="recurrenceEndDate"
                            v-model="form.recurrenceEndDate"
                            type="date"
                        />
                    </div>
                </div>

                <div v-if="message" :class="['message', messageType]">
                    {{ message }}
                </div>

                <div class="modal-actions">
                    <button type="button" class="btn-cancel" @click="handleClose">Cancel</button>
                    <button type="submit" class="btn-create" :disabled="loading">
                        {{ loading ? 'Creating...' : 'Create Event' }}
                    </button>
                </div>
            </form>
        </div>
    </div>
</template>

<script setup lang="ts">
import { reactive, ref, watch } from 'vue';

interface Props {
    show: boolean;
}

interface EventFormData {
    title: string;
    description: string;
    startTime: string;
    endTime: string;
    isRecurring: boolean;
    recurrencePattern: string;
    recurrenceInterval: number;
    recurrenceEndDate: string;
    color: string;
}

const props = defineProps<Props>();

const emit = defineEmits<{
    close: [];
    submit: [formData: EventFormData];
}>();

const form = reactive<EventFormData>({
    title: '',
    description: '',
    startTime: '09:00',
    endTime: '10:00',
    isRecurring: false,
    recurrencePattern: 'weekly',
    recurrenceInterval: 1,
    recurrenceEndDate: '',
    color: '#000000'
});

const loading = ref(false);
const message = ref('');
const messageType = ref<'success' | 'error'>('success');

// Primary color options
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

function resetForm() {
    form.title = '';
    form.description = '';
    form.startTime = '09:00';
    form.endTime = '10:00';
    form.isRecurring = false;
    form.recurrencePattern = 'weekly';
    form.recurrenceInterval = 1;
    form.recurrenceEndDate = '';
    form.color = '#000000';
    message.value = '';
}

function handleClose() {
    resetForm();
    emit('close');
}

function handleSubmit() {
    emit('submit', { ...form });
}

// Expose methods for parent components to control loading and messages
defineExpose({
    setLoading: (value: boolean) => {
        loading.value = value;
    },
    setMessage: (msg: string, type: 'success' | 'error') => {
        message.value = msg;
        messageType.value = type;
    },
    resetForm
});

// Reset form when modal is closed
watch(() => props.show, (newValue) => {
    if (!newValue) {
        resetForm();
    }
});
</script>

<style scoped>
.modal-overlay {
    position: fixed;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    background: rgba(0, 0, 0, 0.5);
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 1000;
}

.modal-content {
    background: white;
    border-radius: 8px;
    padding: 24px;
    max-width: 500px;
    width: 90%;
    max-height: 90vh;
    overflow-y: auto;
    box-shadow: 0 4px 20px rgba(0,0,0,0.3);
}

.modal-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 20px;
}

.modal-header h3 {
    margin: 0;
    color: #262626;
}

.btn-close {
    background: none;
    border: none;
    font-size: 28px;
    line-height: 1;
    cursor: pointer;
    color: #666;
    padding: 0;
    width: 32px;
    height: 32px;
    display: flex;
    align-items: center;
    justify-content: center;
    border-radius: 4px;
    transition: background 0.2s;
}

.btn-close:hover {
    background: #f0f0f0;
}

.form-group {
    margin-bottom: 15px;
}

.form-group label {
    display: block;
    margin-bottom: 5px;
    font-weight: 600;
    color: #262626;
}

.form-group input,
.form-group textarea,
.form-group select {
    width: 100%;
    padding: 8px 12px;
    border: 1px solid #e0e0e0;
    border-radius: 4px;
    font-size: 14px;
    font-family: inherit;
    box-sizing: border-box;
    background: white;
}

.form-group input:focus,
.form-group textarea:focus,
.form-group select:focus {
    outline: none;
    border-color: #4a90e2;
}

.form-group select {
    cursor: pointer;
}

.form-row {
    display: flex;
    gap: 15px;
}

.form-row .form-group {
    flex: 1;
}

.checkbox-label {
    display: flex;
    align-items: center;
    gap: 8px;
    cursor: pointer;
    font-weight: 600;
}

.checkbox-label input[type="checkbox"] {
    width: auto;
    cursor: pointer;
}

.recurring-options {
    padding: 15px;
    background: #f9f9f9;
    border-radius: 4px;
    border: 1px solid #e0e0e0;
    margin-bottom: 15px;
}

.message {
    margin-bottom: 15px;
    padding: 10px;
    border-radius: 4px;
    font-weight: 500;
}

.message.success {
    background: #d4edda;
    color: #155724;
    border: 1px solid #c3e6cb;
}

.message.error {
    background: #f8d7da;
    color: #721c24;
    border: 1px solid #f5c6cb;
}

.modal-actions {
    display: flex;
    gap: 10px;
    justify-content: flex-end;
    margin-top: 20px;
}

.btn-cancel {
    padding: 10px 20px;
    background: #f5f5f5;
    color: #262626;
    border: 1px solid #e0e0e0;
    border-radius: 4px;
    font-size: 14px;
    font-weight: 600;
    cursor: pointer;
    transition: background 0.2s;
}

.btn-cancel:hover {
    background: #e0e0e0;
}

.btn-create {
    padding: 10px 20px;
    background: #4a90e2;
    color: white;
    border: none;
    border-radius: 4px;
    font-size: 14px;
    font-weight: 600;
    cursor: pointer;
    transition: background 0.2s;
}

.btn-create:hover:not(:disabled) {
    background: #357abd;
}

.btn-create:disabled {
    background: #ccc;
    cursor: not-allowed;
}

.color-picker-container {
    margin-top: 5px;
}

.color-swatches {
    display: flex;
    gap: 10px;
    flex-wrap: wrap;
}

.color-swatch {
    width: 36px;
    height: 36px;
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
    border-color: #4a90e2;
    border-width: 3px;
    box-shadow: 0 0 0 2px rgba(74, 144, 226, 0.2);
}
</style>
