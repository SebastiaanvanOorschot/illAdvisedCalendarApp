<template>
    <div v-if="show" class="modal-overlay" @click="$emit('close')">
        <div class="modal-container" @click.stop
            @touchstart="handleTouchStart"
            @touchmove="handleTouchMove"
            @touchend="handleTouchEnd">
            <button class="btn-modal-close" @click="$emit('close')">&times;</button>

            <div class="image" :style="modalBannerStyle">
                <div class="column">
                    <div class="row dateGroupDay">
                        <h2>{{ selectedDate.format('D') }}, {{ selectedDate.format('dddd') }}</h2>
                    </div>
                    <div class="row dateGroupYear">
                        <button type="button" class="dateNav" @click="navigatePreviousDay">&lt;</button>
                        <h3>{{ selectedDate.format('MMMM') }} | {{ selectedDate.year() }}</h3>
                        <button type="button" class="dateNav" @click="navigateNextDay">&gt;</button>
                    </div>
                </div>
            </div>

            <div class="day-view">
                <!-- Weather Overlay spanning entire view -->
                <WeatherIcon
                    v-if="getCurrentDayWeather()"
                    :weatherCode="getCurrentDayWeather()!.weatherCode"
                    :tempMin="getCurrentDayWeather()!.temperatureMin"
                    :tempMax="getCurrentDayWeather()!.temperatureMax"
                    :date="selectedDate.toDate()"
                    class="day-weather"
                />

                <!-- Events Section with Header -->
                <div class="events-section">
                    <div class="events-header-row">
                        <h3 class="events-header">Events</h3>
                        <button class="btn-new-event" @click="openCreateModal" title="Create new event">
                            <svg xmlns="http://www.w3.org/2000/svg" height="24px" viewBox="0 -960 960 960" width="24px" fill="var(--color-white)">
                                <path d="m499-287 335-335-52-52-335 335 52 52Zm-261 87q-100-5-149-42T40-349q0-65 53.5-105.5T242-503q39-3 58.5-12.5T320-542q0-26-29.5-39T193-600l7-80q103 8 151.5 41.5T400-542q0 53-38.5 83T248-423q-64 5-96 23.5T120-349q0 35 28 50.5t94 18.5l-4 80Zm280 7L353-358l382-382q20-20 47.5-20t47.5 20l70 70q20 20 20 47.5T900-575L518-193Zm-159 33q-17 4-30-9t-9-30l33-159 165 165-159 33Z"/>
                            </svg>
                        </button>
                    </div>
                    <div class="events-list">
                        <div v-if="loadingEvents" class="loading">Loading events...</div>
                        <div v-else-if="events.length === 0" class="no-events">No events yet</div>
                        <div v-else>
                            <div v-for="event in events" :key="event.id" class="event-item" @click="openEventDetails(event)">
                                <span class="event-color-dot" :style="{ backgroundColor: event.color || 'var(--color-primary)' }"></span>
                                <div class="event-content">
                                    <div class="event-time">
                                        <template v-if="event.isAllDay">All day</template>
                                        <template v-else-if="event.endDateTime">{{ formatTime(event.startDateTime) }} - {{ formatTime(event.endDateTime) }}</template>
                                        <template v-else>{{ formatTime(event.startDateTime) }}</template>
                                    </div>
                                    <div class="event-details">
                                        <h4>{{ event.title }}</h4>
                                        <p v-if="event.description">{{ event.description }}</p>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <!-- Event Details Modal -->
        <EventDetailsModal
            :show="showDetailsModal"
            :event="selectedEvent"
            @close="closeDetailsModal"
            @edit="handleEditEvent"
            @delete="handleDeleteEvent"
        />

        <!-- Event Form Modal (Create/Edit) -->
        <EventFormModal
            :show="showFormModal"
            :event="eventToEdit"
            @close="closeFormModal"
            @submit="handleFormSubmit"
            ref="formModalRef"
        />

        <!-- Recurring Event Delete Prompt -->
        <RecurringEventPromptModal
            :show="showRecurringDeletePrompt"
            title="Delete Recurring Event"
            message="This is a recurring event. Would you like to delete only this occurrence or all events in the series?"
            @thisEvent="deleteThisOccurrence"
            @allEvents="deleteAllEvents"
            @cancel="cancelDelete"
        />

        <!-- Recurring Event Edit Prompt -->
        <RecurringEventEditPromptModal
            :show="showRecurringEditPrompt"
            title="Edit Recurring Event"
            message="Do you want to edit only this occurrence or all events in the series?"
            @thisEvent="editThisOccurrence"
            @allEvents="editAllEvents"
            @cancel="cancelEdit"
        />
    </div>
</template>

<script setup lang="ts">
import { ref, watch, computed, toRef } from 'vue';
import dayjs from "dayjs";
import { AgendaAPI, EventWithOwnerDto, EventOccurrenceDto } from '@/api/agenda-api-swagger';
import { authenticatedAxios, getApiBaseUrl } from '@/api/axios-config';
import EventDetailsModal from './EventDetailsModal.vue';
import EventFormModal from './EventFormModal.vue';
import RecurringEventPromptModal from './RecurringEventPromptModal.vue';
import RecurringEventEditPromptModal from './RecurringEventEditPromptModal.vue';
import WeatherIcon from '../weather/WeatherIcon.vue';
import { useWeather } from '@/composables/useWeather';
import { useEventOperations } from '@/composables/useEventOperations';
import { useBackButton } from '@/composables/useBackButton';
import { formatTime } from '@/utils/dateFormat';

interface Props {
    show: boolean;
    currentMonth: number;
    currentYear: number;
    selectedDay: number;
    monthImageUrl?: string;
    initialEventId?: number;
}

const props = defineProps<Props>();

const modalBannerStyle = computed(() => {
    if (!props.monthImageUrl) {
        return {
            background: 'var(--color-border)'
        };
    }
    return {
        backgroundImage: `url(${props.monthImageUrl})`,
        backgroundSize: 'cover',
        backgroundPosition: 'center'
    };
});

const emit = defineEmits<{
    (e: 'close'): void;
}>();

// Handle back button to close modal
const showRef = toRef(props, 'show');
useBackButton(showRef, () => emit('close'));

const api = new AgendaAPI(getApiBaseUrl(), authenticatedAxios);

// Internal state for navigation
const displayDay = ref(props.selectedDay);
const displayMonth = ref(props.currentMonth);
const displayYear = ref(props.currentYear);

const events = ref<EventWithOwnerDto[]>([]);
const occurrences = ref<EventOccurrenceDto[]>([]);
const loadingEvents = ref(false);
const showDetailsModal = ref(false);
const showFormModal = ref(false);
const showRecurringDeletePrompt = ref(false);
const showRecurringEditPrompt = ref(false);
const selectedEvent = ref<EventWithOwnerDto | null>(null);
const eventToEdit = ref<EventWithOwnerDto | null>(null);
const eventToDelete = ref<EventWithOwnerDto | null>(null);
const pendingEditEvent = ref<EventWithOwnerDto | null>(null);
const formModalRef = ref<InstanceType<typeof EventFormModal> | null>(null);
const editSeriesMode = ref(false);

// Weather functionality
const { weatherData, getWeatherForDate } = useWeather();

// Event operations
const { createEvent, updateEvent, deleteEvent, editOccurrence, confirmDelete, isRecurringEvent, formatErrorMessage } = useEventOperations();

const selectedDate = computed(() => {
    return dayjs()
        .year(displayYear.value)
        .month(displayMonth.value)
        .date(displayDay.value)
        .hour(0)
        .minute(0)
        .second(0)
        .millisecond(0);
});

async function loadEvents() {
    loadingEvents.value = true;
    try {
        // Get start and end of the selected day
        const startOfDay = selectedDate.value.startOf('day').toDate();
        const endOfDay = selectedDate.value.endOf('day').toDate();

        // Load occurrences for this day
        occurrences.value = await api.occurrences(startOfDay, endOfDay);

        // Fetch full event details for each unique eventId
        const uniqueEventIds = [...new Set(occurrences.value.map(o => o.eventId).filter(id => id !== undefined))];
        const eventPromises = uniqueEventIds.map(id => api.eventsGET(id!));
        const fetchedEvents = await Promise.all(eventPromises);
        events.value = fetchedEvents;
    } catch (error) {
        console.error('Failed to load events:', error);
        events.value = [];
        occurrences.value = [];
    } finally {
        loadingEvents.value = false;
    }
}

function openCreateModal() {
    eventToEdit.value = null;
    showFormModal.value = true;
}

function openEventDetails(event: EventWithOwnerDto) {
    selectedEvent.value = event;
    showDetailsModal.value = true;
}

function closeDetailsModal() {
    showDetailsModal.value = false;
    selectedEvent.value = null;
}

function handleEditEvent(event: EventWithOwnerDto) {
    closeDetailsModal();

    // Check if recurring event
    if (isRecurringEvent(event)) {
        pendingEditEvent.value = event;
        showRecurringEditPrompt.value = true;
    } else {
        // Non-recurring event - edit directly
        eventToEdit.value = event;
        editSeriesMode.value = false;
        showFormModal.value = true;
    }
}

function editThisOccurrence() {
    if (!pendingEditEvent.value) return;
    showRecurringEditPrompt.value = false;
    eventToEdit.value = pendingEditEvent.value;
    editSeriesMode.value = false;
    showFormModal.value = true;
    pendingEditEvent.value = null;
}

function editAllEvents() {
    if (!pendingEditEvent.value) return;
    showRecurringEditPrompt.value = false;
    eventToEdit.value = pendingEditEvent.value;
    editSeriesMode.value = true;
    showFormModal.value = true;
    pendingEditEvent.value = null;
}

function cancelEdit() {
    showRecurringEditPrompt.value = false;
    pendingEditEvent.value = null;
}

async function handleDeleteEvent(event: EventWithOwnerDto) {
    if (!event.id) return;

    // Check if recurring event
    if (isRecurringEvent(event)) {
        eventToDelete.value = event;
        showRecurringDeletePrompt.value = true;
    } else {
        // Non-recurring event - simple confirmation
        if (confirmDelete(event)) {
            try {
                await deleteEvent(event, false);
                await loadEvents();
                closeDetailsModal();
            } catch (error) {
                console.error('Failed to delete event:', error);
                alert('Failed to delete event. Please try again.');
            }
        }
    }
}

async function deleteThisOccurrence() {
    if (!eventToDelete.value?.id) return;

    showRecurringDeletePrompt.value = false;

    try {
        await deleteEvent(eventToDelete.value, false);
        await loadEvents();
        closeDetailsModal();
    } catch (error) {
        console.error('Failed to delete occurrence:', error);
        alert('Failed to delete occurrence. Please try again.');
    } finally {
        eventToDelete.value = null;
    }
}

async function deleteAllEvents() {
    if (!eventToDelete.value?.id) return;

    showRecurringDeletePrompt.value = false;

    try {
        await deleteEvent(eventToDelete.value, true);
        await loadEvents();
        closeDetailsModal();
    } catch (error) {
        console.error('Failed to delete event series:', error);
        alert('Failed to delete event series. Please try again.');
    } finally {
        eventToDelete.value = null;
    }
}

function cancelDelete() {
    showRecurringDeletePrompt.value = false;
    eventToDelete.value = null;
}

function closeFormModal() {
    showFormModal.value = false;
    eventToEdit.value = null;
}

async function handleFormSubmit(formData: any) {
    if (!formModalRef.value) return;

    formModalRef.value.setLoading(true);

    try {
        if (eventToEdit.value && eventToEdit.value.id) {
            // Check if this is a recurring event edit
            const isRecurring = isRecurringEvent(eventToEdit.value);

            if (isRecurring && !editSeriesMode.value) {
                // Edit single occurrence - create exception and new event
                await editOccurrence(eventToEdit.value, formData, selectedDate.value);
                formModalRef.value.setMessage('Occurrence updated successfully!', 'success');
            } else {
                // Edit entire series or non-recurring event
                await updateEvent(eventToEdit.value, formData, selectedDate.value);
                formModalRef.value.setMessage('Event updated successfully!', 'success');
            }
        } else {
            // Create new event
            await createEvent(formData, selectedDate.value);
            formModalRef.value.setMessage('Event created successfully!', 'success');
        }

        await loadEvents();

        setTimeout(() => {
            closeFormModal();
            editSeriesMode.value = false;
        }, 1000);

    } catch (error: any) {
        console.error('Failed to save event:', error);

        // Check if the error response contains the created event data
        if (error.response?.data && typeof error.response.data === 'object' && 'id' in error.response.data) {
            // Success case - the event was created but returned in an error format
            formModalRef.value.setMessage(
                eventToEdit.value ? 'Event updated successfully!' : 'Event created successfully!',
                'success'
            );
            await loadEvents();
            setTimeout(() => {
                closeFormModal();
                editSeriesMode.value = false;
            }, 1000);
        } else {
            // Actual error case
            const errorMessage = formatErrorMessage(error);
            formModalRef.value.setMessage(errorMessage, 'error');
        }
    } finally {
        formModalRef.value.setLoading(false);
    }
}

function navigatePreviousDay() {
    const newDate = selectedDate.value.subtract(1, 'day');
    displayDay.value = newDate.date();
    displayMonth.value = newDate.month();
    displayYear.value = newDate.year();
}

function navigateNextDay() {
    const newDate = selectedDate.value.add(1, 'day');
    displayDay.value = newDate.date();
    displayMonth.value = newDate.month();
    displayYear.value = newDate.year();
}

// Swipe gesture handling for day navigation
let touchStartX = 0;
let touchStartY = 0;
let touchEndX = 0;
let touchEndY = 0;
let isScrolling = false;

const SWIPE_THRESHOLD = 60;
const HORIZONTAL_RATIO = 2;

function handleTouchStart(event: TouchEvent) {
    // Stop event from bubbling to parent calendar
    event.stopPropagation();

    touchStartX = event.touches[0].clientX;
    touchStartY = event.touches[0].clientY;
    touchEndX = touchStartX;
    touchEndY = touchStartY;
    isScrolling = false;
}

function handleTouchMove(event: TouchEvent) {
    // Stop event from bubbling to parent calendar
    event.stopPropagation();

    touchEndX = event.touches[0].clientX;
    touchEndY = event.touches[0].clientY;

    const deltaX = Math.abs(touchEndX - touchStartX);
    const deltaY = Math.abs(touchEndY - touchStartY);

    if (!isScrolling && (deltaX > 10 || deltaY > 10)) {
        isScrolling = deltaY > deltaX;
    }
}

function handleTouchEnd(event: TouchEvent) {
    // Stop event from bubbling to parent calendar
    event.stopPropagation();

    const deltaX = touchEndX - touchStartX;
    const deltaY = Math.abs(touchEndY - touchStartY);
    const absDeltaX = Math.abs(deltaX);

    if (!isScrolling &&
        absDeltaX > SWIPE_THRESHOLD &&
        absDeltaX > deltaY * HORIZONTAL_RATIO) {

        if (deltaX > 0) {
            navigatePreviousDay();
        } else {
            navigateNextDay();
        }
    }

    touchStartX = 0;
    touchStartY = 0;
    touchEndX = 0;
    touchEndY = 0;
    isScrolling = false;
}

function getCurrentDayWeather() {
    const today = dayjs().startOf('day');
    const currentDay = selectedDate.value.startOf('day');
    const daysDiff = currentDay.diff(today, 'day');

    if (daysDiff < 0 || daysDiff >= 14) {
        return null;
    }

    return getWeatherForDate(selectedDate.value.toDate());
}

// Sync display state when props change
watch(() => [props.selectedDay, props.currentMonth, props.currentYear, props.show, props.initialEventId], async () => {
    if (props.show) {
        displayDay.value = props.selectedDay;
        displayMonth.value = props.currentMonth;
        displayYear.value = props.currentYear;
        await loadEvents();

        // If initialEventId is provided, open that event's details
        if (props.initialEventId) {
            const event = events.value.find(e => e.id === props.initialEventId);
            if (event) {
                openEventDetails(event);
            }
        }
    }
});

// Reload events when display date changes
watch([displayDay, displayMonth, displayYear], () => {
    if (props.show) {
        loadEvents();
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
    background: rgba(0, 0, 0, 0.3);
    display: flex;
    align-items: flex-start;
    justify-content: center;
    z-index: 1000;
    padding-top: 40px;
}

.modal-container {
    background: white;
    width: 80%;
    max-width: 900px;
    max-height: 80vh;
    display: flex;
    flex-direction: column;
    overflow: hidden;
    box-shadow: 0 5px 50px rgba(0, 0, 0, 5);
    position: relative;
}

.btn-modal-close {
    position: absolute;
    top: 10px;
    right: 10px;
    background: rgba(0, 0, 0, 0.7) !important;
    border: 2px solid rgba(255, 255, 255, 0.8) !important;
    font-size: 32px !important;
    line-height: 1 !important;
    cursor: pointer;
    color: var(--color-white) !important;
    padding: 5px 12px !important;
    width: 40px !important;
    height: 40px !important;
    display: flex;
    align-items: center;
    justify-content: center;
    border-radius: 50%;
    transition: all 0.2s;
    z-index: 1001;
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.5) !important;
}

.btn-modal-close:hover {
    background: rgba(0, 0, 0, 0.9) !important;
    border-color: white !important;
}

.image {
    display: flex;
    justify-content: left;
    align-items: top;
    width: 100%;
    height: 450px;
    padding: 40px;
}

.image h2,
.image h3 {
    margin: 0;
    padding: 0;
    color: var(--color-white);
    text-shadow: 0 2px 2px rgba(0, 0, 0, .2);
}

.image h3 {
    font-weight: 500;
}

.column {
    display: flex;
    flex-direction: column;
}

.row {
    display: flex;
    justify-content: center;
    align-items: center;
    flex-direction: row;
}

.dateGroupDay {
    display: flex;
    justify-content: center;
    width: 230px;
}

.dateGroupYear {
    display: flex;
    justify-content: center;
    width: 230px;
}

.dateNav {
    background: rgba(255, 255, 255, 0.2);
    border: none;
    color: white;
    font-size: 18px;
    font-weight: bold;
    padding: 5px 12px;
    cursor: pointer;
    border-radius: 4px;
    transition: background 0.2s;
}

.dateNav:hover {
    background: rgba(255, 255, 255, 0.3);
}

.day-view {
    position: relative;
    padding: 20px;
    height: 600px;
    display: flex;
    flex-direction: column;
    overflow: hidden;
    background: transparent;
}

.day-weather {
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    pointer-events: none;
    z-index: 0;
}

.day-weather :deep(.weather-icon) {
    top: 20px !important;
    right: 40px !important;
    width: 150px !important;
    height: 150px !important;
}

.day-weather :deep(.temp-display) {
    top: 25px !important;
    right: 180px !important;
    font-size: 36px !important;
}

.events-section {
    padding-top: 50px;
    flex: 1;
    display: flex;
    flex-direction: column;
    overflow: hidden;
    position: relative;
    z-index: 1;
}

.events-header-row {
    display: flex;
    justify-content: flex-start;
    align-items: center;
    margin-bottom: 50px;
    padding: 0 20px;
    flex-shrink: 0;
    position: relative;
    z-index: 2;
    gap: 15px;
}

.events-header {
    margin: 0;
    color: var(--color-text-dark);
    font-size: 1.2rem;
    font-weight: 600;
}

.btn-new-event {
    background: var(--color-primary) !important;
    border: none;
    border-radius: 50%;
    width: 48px;
    height: 48px;
    display: flex;
    align-items: center;
    justify-content: center;
    cursor: pointer;
    transition: background 0.2s, transform 0.2s;
    box-shadow: 0 2px 4px rgba(0,0,0,0.1);
    flex-shrink: 0;
}

.btn-new-event:hover {
    background: var(--color-primary-hover) !important;
    transform: scale(1.05);
}

.btn-new-event svg {
    display: block;
    fill: white !important;
}

.events-list {
    flex: 1;
    overflow-y: auto;
    overflow-x: hidden;
    position: relative;
    z-index: 2;
}

.loading,
.no-events {
    color: var(--color-text-muted);
    font-style: italic;
    padding: 20px;
    text-align: center;
}

.event-item {
    background: transparent;
    border: 1px solid var(--color-border);
    border-radius: 4px;
    padding: 15px;
    margin-bottom: 10px;
    transition: box-shadow 0.2s;
    cursor: pointer;
    position: relative;
    z-index: 2;
    display: flex;
    align-items: flex-start;
    gap: 12px;
}

.event-item:hover {
    box-shadow: 0 2px 8px rgba(0,0,0,0.1);
}

.event-color-dot {
    width: 10px;
    height: 10px;
    border-radius: 50%;
    flex-shrink: 0;
    margin-top: 5px;
}

.event-content {
    flex: 1;
}

.event-time {
    font-size: 14px;
    color: var(--color-primary);
    font-weight: 600;
    margin-bottom: 8px;
}

.event-details h4 {
    margin: 0 0 5px 0;
    color: var(--color-text-dark);
}

.event-details p {
    margin: 0;
    color: var(--color-text-muted);
    font-size: 14px;
}

/* Mobile responsive styles */
@media screen and (max-width: 800px) {
    .modal-overlay {
        padding-top: 0;
        align-items: flex-start;
        overflow: hidden;
    }

    .modal-container {
        width: 100%;
        max-width: 100%;
        height: 100vh;
        max-height: 100vh;
        border-radius: 0;
        display: flex;
        flex-direction: column;
        overflow: hidden;
        position: fixed;
        top: 0;
        left: 0;
    }

    .image {
        height: 200px;
        padding: 20px;
        flex-shrink: 0;
    }

    .image h2 {
        font-size: 1.2rem;
    }

    .image h3 {
        font-size: 0.9rem;
    }

    .dateGroupDay {
        width: 100%;
    }

    .dateGroupYear {
        width: 100%;
    }

    .dateNav {
        font-size: 14px;
        padding: 4px 10px;
    }

    .btn-modal-close {
        width: 36px;
        height: 36px;
        font-size: 28px;
    }

    .day-view {
        padding: 15px;
        flex: 1;
        height: auto;
        min-height: 0;
        overflow: hidden;
        display: flex;
        flex-direction: column;
    }

    .events-section {
        flex: 1;
        overflow: hidden;
        display: flex;
        flex-direction: column;
        padding-top: 20px;
    }

    .events-list {
        flex: 1;
        overflow-y: auto;
    }

    .events-header-row {
        padding: 0 10px;
        margin-bottom: 30px;
        flex-shrink: 0;
    }

    .events-header {
        font-size: 1rem;
    }

    .btn-new-event {
        width: 40px;
        height: 40px;
    }

    .btn-new-event svg {
        width: 20px;
        height: 20px;
    }

    .day-weather :deep(.weather-icon) {
        width: 100px !important;
        height: 100px !important;
        right: 20px !important;
    }

    .day-weather :deep(.temp-display) {
        font-size: 24px !important;
        right: 130px !important;
    }

    .event-item {
        padding: 12px;
    }
}
</style>
