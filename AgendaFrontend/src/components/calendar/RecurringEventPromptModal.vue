<template>
    <div v-if="show" class="modal-overlay" @click="handleCancel">
        <div class="modal-content" @click.stop>
            <div class="modal-header">
                <h3>{{ title }}</h3>
            </div>

            <div class="modal-body">
                <p>{{ message }}</p>
            </div>

            <div class="modal-actions">
                <button type="button" class="btn-cancel" @click="handleCancel">
                    Cancel
                </button>
                <button type="button" class="btn-this-event" @click="handleThisEvent">
                    This Event Only
                </button>
                <button type="button" class="btn-all-events" @click="handleAllEvents">
                    All Events in Series
                </button>
            </div>
        </div>
    </div>
</template>

<script setup lang="ts">
interface Props {
    show: boolean;
    title: string;
    message: string;
}

interface Emits {
    (e: 'thisEvent'): void;
    (e: 'allEvents'): void;
    (e: 'cancel'): void;
}

defineProps<Props>();
const emit = defineEmits<Emits>();

function handleThisEvent() {
    emit('thisEvent');
}

function handleAllEvents() {
    emit('allEvents');
}

function handleCancel() {
    emit('cancel');
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
    z-index: 1001;
}

.modal-content {
    background: white;
    border-radius: 8px;
    padding: 24px;
    max-width: 500px;
    width: 90%;
    box-shadow: 0 4px 20px rgba(0, 0, 0, 0.3);
}

.modal-header {
    margin-bottom: 16px;
}

.modal-header h3 {
    margin: 0;
    color: var(--color-text-dark);
    font-size: 20px;
}

.modal-body {
    margin-bottom: 24px;
}

.modal-body p {
    margin: 0;
    color: var(--color-text-muted);
    font-size: 15px;
    line-height: 1.5;
}

.modal-actions {
    display: flex;
    gap: 12px;
    justify-content: flex-end;
}

.btn-cancel,
.btn-this-event,
.btn-all-events {
    padding: 10px 20px;
    border: none;
    border-radius: 4px;
    font-size: 14px;
    font-weight: 600;
    cursor: pointer;
    transition: all 0.2s;
}

.btn-cancel {
    background: var(--color-border);
    color: var(--color-text-dark);
}

.btn-cancel:hover {
    background: #d0d0d0;
}

.btn-this-event {
    background: var(--color-primary);
    color: white;
}

.btn-this-event:hover {
    background: var(--color-primary-hover);
}

.btn-all-events {
    background: var(--color-danger);
    color: white;
}

.btn-all-events:hover {
    background: var(--color-danger-hover);
}
</style>
