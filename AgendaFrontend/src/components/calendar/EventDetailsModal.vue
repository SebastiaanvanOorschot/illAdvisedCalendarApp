<template>
    <div v-if="show && event" class="modal-overlay" @click="close">
        <div class="modal-content" @click.stop>
            <div class="modal-header">
                <h3>Event Details</h3>
                <button class="btn-close" @click="close">&times;</button>
            </div>

            <div class="event-details-content">
                <div v-if="isExternalEvent" class="external-indicator">
                    <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <rect x="3" y="4" width="18" height="18" rx="2" ry="2"></rect>
                        <line x1="16" y1="2" x2="16" y2="6"></line>
                        <line x1="8" y1="2" x2="8" y2="6"></line>
                        <line x1="3" y1="10" x2="21" y2="10"></line>
                    </svg>
                    <div class="indicator-content">
                        <span class="indicator-title">{{ event.subscriptionName || 'External Calendar' }}</span>
                        <span class="indicator-subtitle">Read-only subscription</span>
                    </div>
                </div>

                <div v-else-if="!isOwnEvent" class="owner-indicator">
                    <div class="owner-avatar">{{ ownerInitials }}</div>
                    <div class="owner-info">
                        <span class="owner-name">{{ event.ownerName }}'s Event</span>
                        <span class="owner-email">{{ event.ownerEmail }}</span>
                    </div>
                </div>

                <div v-if="isRecurringEvent" class="recurring-indicator">
                    <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M21 12a9 9 0 1 1-9-9c2.52 0 4.93 1 6.74 2.74L21 8"></path>
                        <path d="M21 3v5h-5"></path>
                    </svg>
                    <span>Recurring Event</span>
                </div>

                <div class="detail-group">
                    <label>Title</label>
                    <p>{{ event.title }}</p>
                </div>

                <div class="detail-group" v-if="event.description">
                    <label>Description</label>
                    <p>{{ event.description }}</p>
                </div>

                <div v-if="event.isAllDay" class="detail-group">
                    <label>Time</label>
                    <p>All day</p>
                </div>

                <div v-else class="detail-row">
                    <div class="detail-group">
                        <label>Start Time</label>
                        <p>{{ formatTime(event.startDateTime) }}</p>
                    </div>

                    <div v-if="event.endDateTime" class="detail-group">
                        <label>End Time</label>
                        <p>{{ formatTime(event.endDateTime) }}</p>
                    </div>
                </div>

                <div class="detail-group">
                    <label>Date</label>
                    <p>{{ formatDate(event.startDateTime) }}</p>
                </div>
            </div>

            <div class="modal-actions">
                <button v-if="canEdit" type="button" class="btn-delete" @click="handleDelete">Delete</button>
                <button v-if="canEdit" type="button" class="btn-edit" @click="handleEdit">Edit</button>
                <span v-if="!canEdit" class="read-only-notice">Read-only access</span>
            </div>
        </div>
    </div>
</template>

<script setup lang="ts">
import { computed, toRef } from 'vue';
import { Event, EventWithOwner } from '@/api/agenda-api-swagger';
import dayjs from 'dayjs';
import { useBackButton } from '@/composables/useBackButton';

interface Props {
    show: boolean;
    event: Event | EventWithOwner | null;
}

interface Emits {
    (e: 'close'): void;
    (e: 'edit', event: Event | EventWithOwner): void;
    (e: 'delete', event: Event | EventWithOwner): void;
}

const props = defineProps<Props>();
const emit = defineEmits<Emits>();

// Handle back button to close modal
const showRef = toRef(props, 'show');
useBackButton(showRef, () => emit('close'));

const isRecurringEvent = computed(() => {
    return props.event && (props.event.isRecurring || !!props.event.recurrenceRule);
});

const isExternalEvent = computed(() => {
    return props.event?.isFromSubscription || props.event?.isReadOnly;
});

const isOwnEvent = computed(() => {
    const eventWithOwner = props.event as EventWithOwner;
    return eventWithOwner?.isOwnEvent === undefined || eventWithOwner?.isOwnEvent === true;
});

const canEdit = computed(() => {
    // External events are always read-only
    if (isExternalEvent.value) return false;

    const eventWithOwner = props.event as EventWithOwner;

    // Owner always has edit access
    if (isOwnEvent.value) return true;

    // Check if user has ReadWrite permission on shared event
    // SharePermission: 0 = Read, 1 = ReadWrite
    return eventWithOwner?.permission === 1;
});

const ownerInitials = computed(() => {
    const eventWithOwner = props.event as EventWithOwner;
    if (!eventWithOwner?.ownerName) return '?';
    const parts = eventWithOwner.ownerName.split(' ');
    if (parts.length >= 2) {
        return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    }
    return eventWithOwner.ownerName.substring(0, 2).toUpperCase();
});

function formatTime(date?: Date): string {
    if (!date) return '';
    return dayjs(date).format('HH:mm');
}

function formatDate(date?: Date): string {
    if (!date) return '';
    return dayjs(date).format('MMMM D, YYYY');
}

function close() {
    emit('close');
}

function handleEdit() {
    if (props.event) {
        emit('edit', props.event);
    }
}

function handleDelete() {
    if (props.event) {
        emit('delete', props.event);
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
    box-shadow: 0 4px 20px rgba(0, 0, 0, 0.3);
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

.event-details-content {
    margin-bottom: 20px;
}

.detail-group {
    margin-bottom: 15px;
}

.detail-group label {
    display: block;
    font-weight: 600;
    color: #666;
    font-size: 12px;
    text-transform: uppercase;
    margin-bottom: 5px;
}

.detail-group p {
    margin: 0;
    color: #262626;
    font-size: 14px;
}

.detail-row {
    display: flex;
    gap: 20px;
}

.detail-row .detail-group {
    flex: 1;
}

.modal-actions {
    display: flex;
    gap: 12px;
    justify-content: flex-end;
    padding-top: 16px;
    border-top: 1px solid #e0e0e0;
}

.btn-delete,
.btn-edit {
    padding: 10px 20px;
    border: none;
    border-radius: 4px;
    font-size: 14px;
    font-weight: 600;
    cursor: pointer;
    transition: all 0.2s;
}

.btn-delete {
    background: #dc3545 !important;
    color: white !important;
}

.btn-delete:hover {
    background: #c82333 !important;
}

.btn-edit {
    background: #4a90e2 !important;
    color: white !important;
}

.btn-edit:hover {
    background: #357abd !important;
}

.recurring-indicator {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 8px 12px;
    background: #e3f2fd;
    border-left: 3px solid #2196f3;
    border-radius: 4px;
    margin-bottom: 16px;
    font-size: 14px;
    color: #1976d2;
    font-weight: 500;
}

.recurring-indicator svg {
    flex-shrink: 0;
}

.external-indicator {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 12px;
    background: #fff3e0;
    border-left: 3px solid #ff9800;
    border-radius: 4px;
    margin-bottom: 16px;
}

.external-indicator svg {
    flex-shrink: 0;
    color: #e65100;
}

.indicator-content {
    display: flex;
    flex-direction: column;
    gap: 2px;
}

.indicator-title {
    font-weight: 600;
    color: #e65100;
    font-size: 14px;
}

.indicator-subtitle {
    font-size: 12px;
    color: #f57c00;
}

.owner-indicator {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 12px;
    background: #f3e5f5;
    border-left: 3px solid #9c27b0;
    border-radius: 4px;
    margin-bottom: 16px;
}

.owner-avatar {
    width: 36px;
    height: 36px;
    border-radius: 50%;
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
    color: white;
    display: flex;
    align-items: center;
    justify-content: center;
    font-weight: 600;
    font-size: 13px;
    flex-shrink: 0;
}

.owner-info {
    display: flex;
    flex-direction: column;
    gap: 2px;
}

.owner-name {
    font-weight: 600;
    color: #6a1b9a;
    font-size: 14px;
}

.owner-email {
    font-size: 12px;
    color: #8e24aa;
}

.read-only-notice {
    color: #666;
    font-size: 13px;
    font-style: italic;
}
</style>
