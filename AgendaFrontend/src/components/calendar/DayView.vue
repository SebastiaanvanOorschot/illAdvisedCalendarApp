<template>
    <div class="day-view">
        <!-- Day Banner with Month Image -->
        <div class="day-banner" :style="dayBannerStyle">
            <div class="day-info">
                <h2 class="day-title">{{ getDayTitle() }}</h2>
                <p class="day-date">{{ getFormattedDate() }}</p>
            </div>
        </div>

        <!-- Weather Overlay spanning entire view -->
        <WeatherIcon
            v-if="getCurrentDayWeather()"
            :weatherCode="getCurrentDayWeather()!.weatherCode"
            :tempMin="getCurrentDayWeather()!.temperatureMin"
            :tempMax="getCurrentDayWeather()!.temperatureMax"
            class="day-weather"
        />

        <!-- Events Section with Header -->
        <div class="events-section">
            <div class="events-header-row">
                <h3 class="events-header">Events</h3>
                <button class="btn-new-event" @click="openCreateModal" title="Create new event">
                    <svg xmlns="http://www.w3.org/2000/svg" height="24px" viewBox="0 -960 960 960" width="24px" fill="#e3e3e3">
                        <path d="m499-287 335-335-52-52-335 335 52 52Zm-261 87q-100-5-149-42T40-349q0-65 53.5-105.5T242-503q39-3 58.5-12.5T320-542q0-26-29.5-39T193-600l7-80q103 8 151.5 41.5T400-542q0 53-38.5 83T248-423q-64 5-96 23.5T120-349q0 35 28 50.5t94 18.5l-4 80Zm280 7L353-358l382-382q20-20 47.5-20t47.5 20l70 70q20 20 20 47.5T900-575L518-193Zm-159 33q-17 4-30-9t-9-30l33-159 165 165-159 33Z"/>
                    </svg>
                </button>
            </div>
            <div class="events-list">
                <div v-if="loadingEvents" class="loading">Loading events...</div>
                <div v-else-if="events.length === 0" class="no-events">No events yet</div>
                <div v-else>
                    <div v-for="event in events" :key="event.id" class="event-item" @click="openEventDetails(event)">
                        <div class="event-time">
                            {{ formatTime(event.startDateTime) }} - {{ formatTime(event.endDateTime) }}
                        </div>
                        <div class="event-details">
                            <h4>{{ event.title }}</h4>
                            <p v-if="event.description">{{ event.description }}</p>
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
            message="Do you want to delete only this occurrence or all events in the series?"
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
import { computed, ref, watch, onMounted } from 'vue';
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
import { formatTime } from '@/utils/dateFormat';

interface Props {
    currentMonth: number;
    currentYear: number;
    selectedDay: number;
    monthImageUrl?: string;
}

const props = defineProps<Props>();

const dayBannerStyle = computed(() => {
    console.log('DayView dayBannerStyle - monthImageUrl:', props.monthImageUrl);
    if (!props.monthImageUrl) {
        console.log('No monthImageUrl provided to DayView');
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

const api = new AgendaAPI(getApiBaseUrl(), authenticatedAxios);
const { createEvent, updateEvent, editOccurrence, deleteEvent, confirmDelete, isRecurringEvent, formatErrorMessage } = useEventOperations();

const events = ref<EventWithOwnerDto[]>([]);
const loadingEvents = ref(false);
const showDetailsModal = ref(false);
const showFormModal = ref(false);
const selectedEvent = ref<EventWithOwnerDto | null>(null);
const eventToEdit = ref<EventWithOwnerDto | null>(null);
const formModalRef = ref<InstanceType<typeof EventFormModal> | null>(null);

// Recurring event delete prompt state
const showRecurringDeletePrompt = ref(false);
const eventToDelete = ref<EventWithOwnerDto | null>(null);

// Recurring event edit prompt state
const showRecurringEditPrompt = ref(false);
const pendingEditEvent = ref<EventWithOwnerDto | null>(null);
const editSeriesMode = ref(false);

// Weather functionality
const { weatherData, fetchWeather, getWeatherForDate } = useWeather();

const selectedDate = computed(() => {
    return dayjs()
        .date(props.selectedDay)
        .month(props.currentMonth)
        .year(props.currentYear);
});

async function loadEvents() {
    loadingEvents.value = true;
    try {
        // Get start and end of the selected day
        const startOfDay = selectedDate.value.startOf('day').toDate();
        const endOfDay = selectedDate.value.endOf('day').toDate();

        console.log('Loading occurrences for date range:', startOfDay, endOfDay);

        // Load occurrences for this day
        const occurrences = await api.occurrences(startOfDay, endOfDay);

        // Fetch full event details for each unique eventId
        const uniqueEventIds = [...new Set(occurrences.map(o => o.eventId).filter(id => id !== undefined))];
        const eventPromises = uniqueEventIds.map(id => api.eventsGET(id!));
        const fullEvents = await Promise.all(eventPromises);

        // Create a map of eventId to full event
        const eventMap = new Map<number, EventWithOwnerDto>();
        fullEvents.forEach(event => {
            if (event.id !== undefined) {
                eventMap.set(event.id, event);
            }
        });

        // Create display events with occurrence times
        events.value = occurrences
            .filter(o => o.eventId !== undefined)
            .map(occurrence => {
                const fullEvent = eventMap.get(occurrence.eventId!);
                if (!fullEvent) return null;

                return {
                    ...fullEvent,
                    startDateTime: occurrence.occurrenceStart,
                    endDateTime: occurrence.occurrenceEnd
                } as EventWithOwnerDto;
            })
            .filter(e => e !== null) as EventWithOwnerDto[];

        console.log('Loaded events:', events.value);
    } catch (error) {
        console.error('Failed to load events:', error);
        events.value = [];
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
    editSeriesMode.value = false;
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
        }, 1000);

    } catch (error: any) {
        console.error('Failed to save event:', error);
        const errorMessage = formatErrorMessage(error);
        formModalRef.value.setMessage(errorMessage, 'error');
    } finally {
        formModalRef.value.setLoading(false);
    }
}

function getDayTitle() {
    const selectedDate = dayjs().date(props.selectedDay).month(props.currentMonth).year(props.currentYear);
    return selectedDate.format("dddd");
}

function getFormattedDate() {
    const selectedDate = dayjs().date(props.selectedDay).month(props.currentMonth).year(props.currentYear);
    return selectedDate.format("MMMM D, YYYY");
}

// Get weather for the selected day
function getCurrentDayWeather() {
    const today = dayjs().startOf('day');
    const currentDay = selectedDate.value.startOf('day');
    const daysDiff = currentDay.diff(today, 'day');

    // Only show weather for next 14 days
    if (daysDiff < 0 || daysDiff >= 14) {
        return null;
    }

    return getWeatherForDate(selectedDate.value.toDate());
}

// Fetch user location and weather on mount
async function initWeather() {
    if ('geolocation' in navigator) {
        try {
            const position = await new Promise<GeolocationPosition>((resolve, reject) => {
                navigator.geolocation.getCurrentPosition(resolve, reject);
            });

            await fetchWeather(position.coords.latitude, position.coords.longitude);
        } catch (error) {
            console.warn('Could not get user location for weather:', error);
            // Fallback to a default location (e.g., Amsterdam)
            await fetchWeather(52.3676, 4.9041);
        }
    } else {
        // Fallback to default location
        await fetchWeather(52.3676, 4.9041);
    }
}

// Load events when component mounts or date changes
watch([() => props.selectedDay, () => props.currentMonth, () => props.currentYear], () => {
    loadEvents();
}, { immediate: true });

onMounted(() => {
    initWeather();
});

</script>

<style scoped>
.day-view {
    position: relative;
    padding: 0;
    height: 780px;
    display: flex;
    flex-direction: column;
    overflow: hidden;
    background: transparent;
}

/* Day Banner with Month Image */
.day-banner {
    position: relative;
    width: 100%;
    min-height: 200px;
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 40px 20px;
    flex-shrink: 0;
}

.day-info {
    text-align: center;
}

.day-title {
    font-size: 2.5rem;
    font-weight: 700;
    color: var(--color-white);
    margin: 0 0 8px 0;
    text-shadow:
        0 0 3px rgba(0, 0, 0, 0.8),
        0 0 5px rgba(0, 0, 0, 0.6),
        0 2px 4px rgba(0, 0, 0, 0.4),
        -1px -1px 0 rgba(0, 0, 0, 0.5),
        1px -1px 0 rgba(0, 0, 0, 0.5),
        -1px 1px 0 rgba(0, 0, 0, 0.5),
        1px 1px 0 rgba(0, 0, 0, 0.5);
}

.day-date {
    font-size: 1.2rem;
    color: var(--color-white);
    margin: 0;
    font-weight: 500;
    text-shadow:
        0 0 3px rgba(0, 0, 0, 0.8),
        0 0 5px rgba(0, 0, 0, 0.6),
        0 2px 4px rgba(0, 0, 0, 0.4),
        -1px -1px 0 rgba(0, 0, 0, 0.5),
        1px -1px 0 rgba(0, 0, 0, 0.5),
        -1px 1px 0 rgba(0, 0, 0, 0.5),
        1px 1px 0 rgba(0, 0, 0, 0.5);
}

/* Weather overlay contained within white area */
.day-weather {
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    pointer-events: none;
    z-index: 0;
}

/* Larger weather icon and temp for day view */
.day-weather :deep(.weather-icon) {
    top: 20px !important;
    right: 40px !important;
    width: 200px !important;
    height: 200px !important;
}

.day-weather :deep(.temp-display) {
    top: 25px !important;
    right: 210px !important;
    font-size: 48px !important;
}

.btn-new-event {
    background: var(--color-primary);
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
    background: var(--color-primary-hover);
    transform: scale(1.05);
}

.btn-new-event svg {
    display: block;
}

/* Events Section */
.events-section {
    padding: 50px 20px 20px 20px;
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
}

.event-item:hover {
    box-shadow: 0 2px 8px rgba(0,0,0,0.1);
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
</style>
