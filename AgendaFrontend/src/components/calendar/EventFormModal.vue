<template>
    <div v-if="show" class="modal-overlay" @click="handleClose">
        <div class="modal-content" @click.stop>
            <div class="modal-header">
                <h3>{{ isEditMode ? 'Edit Event' : 'Create New Event' }}</h3>
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

                <div class="form-group">
                    <label class="checkbox-label">
                        <input
                            type="checkbox"
                            v-model="form.isAllDay"
                        />
                        All day
                    </label>
                </div>

                <div v-if="!form.isAllDay" class="form-row">
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

                <div v-if="!isEditMode" class="form-group">
                    <label class="checkbox-label">
                        <input
                            type="checkbox"
                            v-model="form.isRecurring"
                        />
                        Make this a recurring event
                    </label>
                </div>

                <div v-if="(isEditMode && isEditingRecurringEvent) || (!isEditMode && form.isRecurring)" class="recurring-options">
                    <div class="form-group">
                        <label class="checkbox-label">
                            <input
                                type="checkbox"
                                v-model="useAdvancedRecurrence"
                            />
                            Use advanced recurrence rules (RRULE)
                        </label>
                    </div>

                    <div v-if="!useAdvancedRecurrence">
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

                    <div v-else class="advanced-recurrence">
                        <div class="form-group">
                            <label for="advancedPattern">Recurrence Pattern</label>
                            <select
                                id="advancedPattern"
                                v-model="advancedPattern"
                                @change="updateRRuleFromPattern"
                            >
                                <option value="weekdays">Every weekday (Monday to Friday)</option>
                                <option value="weekends">Every weekend (Saturday and Sunday)</option>
                                <option value="first-monday">First Monday of every month</option>
                                <option value="last-friday">Last Friday of every month</option>
                                <option value="second-tuesday">Second Tuesday of every month</option>
                                <option value="third-wednesday">Third Wednesday of every month</option>
                                <option value="custom">Custom RRULE</option>
                            </select>
                        </div>

                        <div v-if="advancedPattern === 'custom'" class="form-group">
                            <label for="customRRule">Custom RRULE String</label>
                            <input
                                id="customRRule"
                                v-model="form.recurrenceRule"
                                type="text"
                                placeholder="FREQ=WEEKLY;BYDAY=MO,WE,FR"
                            />
                            <div class="help-text">
                                <a href="https://icalendar.org/iCalendar-RFC-5545/3-8-5-3-recurrence-rule.html" target="_blank">
                                    Learn about RRULE syntax
                                </a>
                            </div>
                        </div>

                        <div v-else class="form-group">
                            <label>Generated RRULE</label>
                            <div class="rrule-display">{{ form.recurrenceRule || 'Not set' }}</div>
                        </div>

                        <div class="form-group">
                            <label for="advancedEndDate">End Date (optional)</label>
                            <input
                                id="advancedEndDate"
                                v-model="form.recurrenceEndDate"
                                type="date"
                            />
                        </div>
                    </div>
                </div>

                <div v-if="message" :class="['message', messageType]">
                    {{ message }}
                </div>

                <div class="modal-actions">
                    <button type="button" class="btn-cancel" @click="handleClose">Cancel</button>
                    <button type="submit" class="btn-create" :disabled="loading">
                        {{ loading ? (isEditMode ? 'Updating...' : 'Creating...') : (isEditMode ? 'Update Event' : 'Create Event') }}
                    </button>
                </div>
            </form>
        </div>
    </div>
</template>

<script setup lang="ts">
import { reactive, ref, watch, computed, toRef } from 'vue';
import { Event } from '@/api/agenda-api-swagger';
import dayjs from 'dayjs';
import { useBackButton } from '@/composables/useBackButton';

interface Props {
    show: boolean;
    event?: Event | null;
}

interface EventFormData {
    title: string;
    description: string;
    isAllDay: boolean;
    startTime: string;
    endTime: string;
    isRecurring: boolean;
    recurrencePattern: string;
    recurrenceInterval: number;
    recurrenceEndDate: string;
    recurrenceRule?: string;
    color: string;
}

const props = defineProps<Props>();

const emit = defineEmits<{
    close: [];
    submit: [formData: EventFormData];
}>();

// Handle back button to close modal
const showRef = toRef(props, 'show');
useBackButton(showRef, () => emit('close'));

const form = reactive<EventFormData>({
    title: '',
    description: '',
    isAllDay: false,
    startTime: '09:00',
    endTime: '10:00',
    isRecurring: false,
    recurrencePattern: 'weekly',
    recurrenceInterval: 1,
    recurrenceEndDate: '',
    recurrenceRule: undefined,
    color: '#000000'
});

const loading = ref(false);
const message = ref('');
const messageType = ref<'success' | 'error'>('success');

const isEditMode = ref(false);
const useAdvancedRecurrence = ref(false);
const advancedPattern = ref('weekdays');

// Check if we're editing a recurring event
const isEditingRecurringEvent = computed(() => {
    return isEditMode.value && props.event && (props.event.isRecurring || !!props.event.recurrenceRule);
});

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

function updateRRuleFromPattern() {
    const patterns: Record<string, string> = {
        'weekdays': 'FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR',
        'weekends': 'FREQ=WEEKLY;BYDAY=SA,SU',
        'first-monday': 'FREQ=MONTHLY;BYDAY=1MO',
        'last-friday': 'FREQ=MONTHLY;BYDAY=-1FR',
        'second-tuesday': 'FREQ=MONTHLY;BYDAY=2TU',
        'third-wednesday': 'FREQ=MONTHLY;BYDAY=3WE'
    };

    if (advancedPattern.value !== 'custom') {
        form.recurrenceRule = patterns[advancedPattern.value];
    }
}

function resetForm() {
    form.title = '';
    form.description = '';
    form.isAllDay = false;
    form.startTime = '09:00';
    form.endTime = '10:00';
    form.isRecurring = false;
    form.recurrencePattern = 'weekly';
    form.recurrenceInterval = 1;
    form.recurrenceEndDate = '';
    form.recurrenceRule = undefined;
    form.color = '#000000';
    message.value = '';
    isEditMode.value = false;
    useAdvancedRecurrence.value = false;
    advancedPattern.value = 'weekdays';
}

function populateForm(event: Event) {
    form.title = event.title || '';
    form.description = event.description || '';
    form.color = event.color || '#000000';
    form.isAllDay = event.isAllDay || false;

    if (!form.isAllDay) {
        if (event.startDateTime) {
            form.startTime = dayjs(event.startDateTime).format('HH:mm');
        }
        if (event.endDateTime) {
            form.endTime = dayjs(event.endDateTime).format('HH:mm');
        }
    }

    // Populate recurrence fields if editing a recurring event
    if (event.isRecurring || event.recurrenceRule) {
        form.isRecurring = true;

        // Check if event uses RRULE (advanced recurrence)
        if (event.recurrenceRule) {
            useAdvancedRecurrence.value = true;
            form.recurrenceRule = event.recurrenceRule;

            // Try to match to a known pattern
            const rruleLower = event.recurrenceRule.toLowerCase();
            if (rruleLower === 'freq=weekly;byday=mo,tu,we,th,fr') {
                advancedPattern.value = 'weekdays';
            } else if (rruleLower === 'freq=weekly;byday=sa,su') {
                advancedPattern.value = 'weekends';
            } else if (rruleLower === 'freq=monthly;byday=1mo') {
                advancedPattern.value = 'first-monday';
            } else if (rruleLower === 'freq=monthly;byday=-1fr') {
                advancedPattern.value = 'last-friday';
            } else if (rruleLower === 'freq=monthly;byday=2tu') {
                advancedPattern.value = 'second-tuesday';
            } else if (rruleLower === 'freq=monthly;byday=3we') {
                advancedPattern.value = 'third-wednesday';
            } else {
                advancedPattern.value = 'custom';
            }
        } else {
            // Simple recurrence pattern
            useAdvancedRecurrence.value = false;
            form.recurrencePattern = event.recurrencePattern || 'weekly';
            form.recurrenceInterval = event.recurrenceInterval || 1;
        }

        if (event.recurrenceEndDate) {
            form.recurrenceEndDate = dayjs(event.recurrenceEndDate).format('YYYY-MM-DD');
        }
    }

    isEditMode.value = true;
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

// Initialize RRULE when advanced recurrence is enabled
watch(useAdvancedRecurrence, (newValue) => {
    if (newValue && !form.recurrenceRule) {
        updateRRuleFromPattern();
    }
});

// Watch for event prop changes to populate form in edit mode
watch(() => props.event, (newEvent) => {
    if (newEvent && props.show) {
        populateForm(newEvent);
    }
}, { immediate: true });

// Reset form when modal is closed
watch(() => props.show, (newValue) => {
    if (!newValue) {
        resetForm();
    } else if (props.event) {
        populateForm(props.event);
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

.form-group input:disabled {
    background: #f5f5f5;
    color: #999;
    cursor: not-allowed;
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

.info-message {
    margin-bottom: 15px;
    padding: 10px;
    border-radius: 4px;
    background: #d1ecf1;
    color: #0c5460;
    border: 1px solid #bee5eb;
    font-size: 13px;
    line-height: 1.4;
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

.advanced-recurrence {
    padding-top: 10px;
}

.rrule-display {
    padding: 8px 12px;
    background: #f5f5f5;
    border: 1px solid #e0e0e0;
    border-radius: 4px;
    font-family: 'Courier New', monospace;
    font-size: 13px;
    color: #262626;
    word-break: break-all;
}

.help-text {
    margin-top: 5px;
    font-size: 12px;
    color: #666;
}

.help-text a {
    color: #4a90e2;
    text-decoration: none;
}

.help-text a:hover {
    text-decoration: underline;
}
</style>
