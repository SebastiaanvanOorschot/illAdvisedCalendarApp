<template>
    <div class="week-view">
        <div v-for="(d, index) in dates" :key="index" class="week-column" @click="onDateSelect(d)">
            <div class="week-header">
                <!-- Weather icon overlay -->
                <WeatherIcon
                    v-if="getWeatherForDay(index)"
                    :weatherCode="getWeatherForDay(index)!.weatherCode"
                    :tempMin="getWeatherForDay(index)!.temperatureMin"
                    :tempMax="getWeatherForDay(index)!.temperatureMax"
                    :date="getDateForDayIndex(index)"
                    class="week-weather"
                />
                <div class="day-name">
                    <span class="day-name-full">{{ days[index] }}</span>
                    <span class="day-name-abbr">{{ daysAbbreviated[index] }}</span>
                </div>
                <div class="header-bottom">
                    <div :class="['day-number', d.thisMonth ? '' : 'not-this-month']">{{ d.date }}</div>
                </div>
            </div>
            <div class="week-content">
                <div v-if="loadingEvents" class="loading">Loading...</div>
                <div v-else-if="getEventsForDay(index).length === 0" class="no-events">No events</div>
                <div v-else class="events-container">
                    <div
                        v-for="occurrence in getEventsForDay(index)"
                        :key="`${occurrence.eventId}-${occurrence.occurrenceStart?.getTime()}`"
                        class="event-item"
                        :style="{ borderLeftColor: occurrence.color || 'var(--color-primary)' }"
                        @click.stop="openEventDetails(occurrence)"
                        @touchend.stop.prevent="openEventDetails(occurrence)"
                    >
                        <div class="event-time">
                            <span class="event-time-full">{{ formatTime(occurrence.occurrenceStart) }}</span>
                            <span class="event-time-abbr">{{ formatTime(occurrence.occurrenceStart).split(':')[0] }}</span>
                        </div>
                        <div class="event-title">{{ occurrence.title }}</div>
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

        <!-- Event Form Modal (Edit) -->
        <EventFormModal
            :show="showEditModal"
            :event="selectedEvent"
            @close="closeEditModal"
            @submit="handleFormSubmit"
            ref="formModalRef"
        />

        <!-- Day Modal -->
        <DayModal
            :show="showDayModal"
            :currentMonth="modalMonth"
            :currentYear="modalYear"
            :selectedDay="modalDay"
            :monthImageUrl="monthImageUrl"
            @close="closeDayModal"
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
import { ref, watch, onMounted } from 'vue';
import dayjs from "dayjs";
import weekOfYear from 'dayjs/plugin/weekOfYear';
import isoWeek from 'dayjs/plugin/isoWeek';
import { AgendaAPI, Event, EventOccurrence } from '@/api/agenda-api-swagger';
import { authenticatedAxios, getApiBaseUrl } from '@/api/axios-config';
import DayModal from './DayModal.vue';
import EventFormModal from './EventFormModal.vue';
import EventDetailsModal from './EventDetailsModal.vue';
import RecurringEventPromptModal from './RecurringEventPromptModal.vue';
import RecurringEventEditPromptModal from './RecurringEventEditPromptModal.vue';
import WeatherIcon from '../weather/WeatherIcon.vue';
import { useWeather } from '@/composables/useWeather';
import { useEventOperations } from '@/composables/useEventOperations';
import type { CalendarDate } from '@/types/calendar';
import { formatTime, formatDate } from '@/utils/dateFormat';

dayjs.extend(weekOfYear);
dayjs.extend(isoWeek);

interface Props {
    currentMonth: number;
    currentYear: number;
    selectedDay: number;
    monthImageUrl?: string;
}

interface Emits {
    (e: 'dateSelect', date: CalendarDate): void;
}

const props = defineProps<Props>();
const emit = defineEmits<Emits>();

const api = new AgendaAPI(getApiBaseUrl(), authenticatedAxios);

const days = [
    "Monday",
    "Tuesday",
    "Wednesday",
    "Thursday",
    "Friday",
    "Saturday",
    "Sunday"
];

const daysAbbreviated = [
    "Mon",
    "Tue",
    "Wed",
    "Thu",
    "Fri",
    "Sat",
    "Sun"
];

const dates = ref<CalendarDate[]>([]);
const weekOccurrences = ref<EventOccurrence[]>([]);
const loadingEvents = ref(false);
const showDetailsModal = ref(false);
const showEditModal = ref(false);
const selectedEvent = ref<Event | null>(null);
const formModalRef = ref<InstanceType<typeof EventFormModal> | null>(null);

// Day Modal state
const showDayModal = ref(false);
const modalDay = ref(1);
const modalMonth = ref(0);
const modalYear = ref(2024);

// Weather functionality
const { weatherData, fetchWeather, getWeatherForDate } = useWeather();

// Event operations
const { createEvent, updateEvent, editOccurrence, deleteEvent, confirmDelete, isRecurringEvent, formatErrorMessage } = useEventOperations();

// Recurring event delete prompt state
const showRecurringDeletePrompt = ref(false);
const eventToDelete = ref<Event | null>(null);

// Recurring event edit prompt state
const showRecurringEditPrompt = ref(false);
const pendingEditEvent = ref<Event | null>(null);
const editSeriesMode = ref(false);

function generateWeekView() {
    dates.value = [];

    // Get the selected date
    const selectedDate = dayjs().date(props.selectedDay).month(props.currentMonth).year(props.currentYear);

    // Get the start of the week (Monday) for the selected date
    const startOfWeek = selectedDate.startOf('isoWeek');

    // Generate all 7 days of the week
    for (let i = 0; i < 7; i++) {
        const currentDay = startOfWeek.add(i, 'day');

        dates.value.push({
            date: currentDay.date(),
            day: currentDay.isoWeekday(),
            thisMonth: currentDay.month() === props.currentMonth
        });
    }
}

async function loadWeekEvents() {
    loadingEvents.value = true;
    weekOccurrences.value = [];

    try {
        const selectedDate = dayjs().date(props.selectedDay).month(props.currentMonth).year(props.currentYear);
        const startOfWeek = selectedDate.startOf('isoWeek');
        const endOfWeek = selectedDate.endOf('isoWeek');

        // Load all event occurrences (including RRULE-calculated ones) for the week
        weekOccurrences.value = await api.occurrences(startOfWeek.toDate(), endOfWeek.toDate());
    } catch (error) {
        console.error('Failed to load week events:', error);
        weekOccurrences.value = [];
    } finally {
        loadingEvents.value = false;
    }
}

function getEventsForDay(dayIndex: number): EventOccurrence[] {
    const selectedDate = dayjs().date(props.selectedDay).month(props.currentMonth).year(props.currentYear);
    const startOfWeek = selectedDate.startOf('isoWeek');
    const targetDay = startOfWeek.add(dayIndex, 'day');

    return weekOccurrences.value.filter(occurrence => {
        if (!occurrence.occurrenceStart) return false;
        const occurrenceDate = dayjs(occurrence.occurrenceStart);
        return occurrenceDate.isSame(targetDay, 'day');
    }).sort((a, b) => {
        if (!a.occurrenceStart || !b.occurrenceStart) return 0;
        return new Date(a.occurrenceStart).getTime() - new Date(b.occurrenceStart).getTime();
    });
}

async function openEventDetails(occurrence: EventOccurrence) {
    // Fetch the full event from the API using the eventId
    if (!occurrence.eventId) return;

    try {
        const fullEvent = await api.eventsGET(occurrence.eventId);
        selectedEvent.value = fullEvent;
        showDetailsModal.value = true;
    } catch (error) {
        console.error('Failed to load event details:', error);
    }
}

function closeDetailsModal() {
    showDetailsModal.value = false;
    selectedEvent.value = null;
}

function handleEditEvent(event: Event) {
    closeDetailsModal();

    // Check if recurring event
    if (isRecurringEvent(event)) {
        pendingEditEvent.value = event;
        showRecurringEditPrompt.value = true;
    } else {
        // Non-recurring event - edit directly
        selectedEvent.value = event;
        editSeriesMode.value = false;
        showEditModal.value = true;
    }
}

function editThisOccurrence() {
    if (!pendingEditEvent.value) return;
    showRecurringEditPrompt.value = false;
    selectedEvent.value = pendingEditEvent.value;
    editSeriesMode.value = false;
    showEditModal.value = true;
    pendingEditEvent.value = null;
}

function editAllEvents() {
    if (!pendingEditEvent.value) return;
    showRecurringEditPrompt.value = false;
    selectedEvent.value = pendingEditEvent.value;
    editSeriesMode.value = true;
    showEditModal.value = true;
    pendingEditEvent.value = null;
}

function cancelEdit() {
    showRecurringEditPrompt.value = false;
    pendingEditEvent.value = null;
}

function closeEditModal() {
    showEditModal.value = false;
    selectedEvent.value = null;
    editSeriesMode.value = false;
}

async function handleFormSubmit(formData: any) {
    if (!formModalRef.value || !selectedEvent.value || !selectedEvent.value.id || !selectedEvent.value.startDateTime) return;

    formModalRef.value.setLoading(true);

    try {
        const eventDate = dayjs(selectedEvent.value.startDateTime);
        const isRecurring = isRecurringEvent(selectedEvent.value);

        if (isRecurring && !editSeriesMode.value) {
            // Edit single occurrence - create exception and new event
            await editOccurrence(selectedEvent.value, formData, eventDate);
            formModalRef.value.setMessage('Occurrence updated successfully!', 'success');
        } else {
            // Edit entire series or non-recurring event
            await updateEvent(selectedEvent.value, formData, eventDate);
            formModalRef.value.setMessage('Event updated successfully!', 'success');
        }

        await loadWeekEvents();

        setTimeout(() => {
            closeEditModal();
        }, 1000);

    } catch (error: any) {
        console.error('Failed to update event:', error);
        const errorMessage = formatErrorMessage(error);
        formModalRef.value.setMessage(errorMessage, 'error');
    } finally {
        formModalRef.value.setLoading(false);
    }
}

async function handleDeleteEvent(event: Event) {
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
                await loadWeekEvents();
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
        await loadWeekEvents();
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
        await loadWeekEvents();
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

function onDateSelect(d: CalendarDate) {
    // Determine which month this date belongs to based on the week context
    const selectedDateObj = dayjs().date(props.selectedDay).month(props.currentMonth).year(props.currentYear);
    const startOfWeek = selectedDateObj.startOf('isoWeek');

    // Find which day in the week was clicked
    const clickedDate = dates.value.find(date => date.date === d.date && date.day === d.day);
    const dayIndex = dates.value.indexOf(clickedDate!);

    if (dayIndex >= 0) {
        const targetDay = startOfWeek.add(dayIndex, 'day');

        // Emit dateSelect to update the parent's selected date
        emit('dateSelect', d);

        modalDay.value = targetDay.date();
        modalMonth.value = targetDay.month();
        modalYear.value = targetDay.year();
        showDayModal.value = true;
    }
}

function closeDayModal() {
    showDayModal.value = false;
    // Reload week events to reflect any changes made in the day modal
    loadWeekEvents();
}

// Helper to get the date for a day index
function getDateForDayIndex(dayIndex: number): Date {
    const selectedDate = dayjs().date(props.selectedDay).month(props.currentMonth).year(props.currentYear);
    const startOfWeek = selectedDate.startOf('isoWeek');
    return startOfWeek.add(dayIndex, 'day').toDate();
}

// Get weather for a specific day in the week
function getWeatherForDay(dayIndex: number) {
    const targetDay = dayjs(getDateForDayIndex(dayIndex));
    const today = dayjs().startOf('day');
    const dayDate = targetDay.startOf('day');
    const daysDiff = dayDate.diff(today, 'day');

    // Only show weather for next 14 days
    if (daysDiff < 0 || daysDiff >= 14) {
        return null;
    }

    return getWeatherForDate(targetDay.toDate());
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

watch(() => [props.currentMonth, props.currentYear, props.selectedDay], () => {
    generateWeekView();
    loadWeekEvents();
});

onMounted(() => {
    generateWeekView();
    loadWeekEvents();
    initWeather();
});

</script>

<style scoped>
.week-column {
    min-width: 0;   /* <— THIS fixes the flex shrinking issue */
    flex: 1;
    display: flex;
    flex-direction: column;
    border-right: 1px solid var(--color-border);
    cursor: pointer;
    transition: background-color 0.2s;
    position: relative;
}

.week-column:hover {
    background-color: var(--color-bg-subtle);
}

.week-column:last-child {
    border-right: none;
}

.week-header {
    border-bottom: 2px solid var(--color-border);
    text-align: center;
    position: relative;
    overflow: hidden;
}

.day-name {
    font-weight: 700;
    font-size: 0.9rem;
    color: var(--color-text-dark);
    margin-top: 25px;
    margin-bottom: 5px;
    position: relative;
    z-index: 3;
}

.header-bottom {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 8px;
    position: relative;
    z-index: 3;
}

.week-column:nth-child(7) .day-name {
    color: #ff685d;
}

.day-number {
    font-size: 1.5rem;
    font-weight: 600;
    color: var(--color-text-dark);
    margin-bottom: 5px;
}

.day-number.not-this-month {
    color: var(--color-text-subtle);
}

.week-column:nth-child(7) .day-number {
    color: #ff685d;
}

.week-column:nth-child(7) .day-number.not-this-month {
    color: #ffb3ad;
}

.week-content {
    flex: 1;
    padding: 10px;
    overflow-y: auto;
    position: relative;
    z-index: 2;
}

.loading,
.no-events {
    color: var(--color-text-subtle);
    font-size: 0.85rem;
    text-align: center;
    margin-top: 20px;
}

.events-container {
    display: flex;
    flex-direction: column;
    gap: 8px;
}

.event-item {
    background: white;
    border-left: 3px solid var(--color-primary);
    padding: 8px;
    border-radius: 4px;
    box-shadow: 0 1px 3px rgba(0,0,0,0.1);
    transition: transform 0.2s, box-shadow 0.2s;
}

.event-item:hover {
    transform: translateX(2px);
    box-shadow: 0 2px 6px rgba(0,0,0,0.15);
}

.event-time {
    font-size: 0.75rem;
    color: var(--color-primary);
    font-weight: 600;
    margin-bottom: 4px;
}

.event-title {
    font-size: 0.85rem;
    color: var(--color-text-dark);
    font-weight: 500;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
}

/* Modal */
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
    color: var(--color-text-dark);
}

.btn-close {
    background: none;
    border: none;
    font-size: 28px;
    line-height: 1;
    cursor: pointer;
    color: var(--color-text-muted);
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
    background: var(--color-bg-muted);
}

/* Event Details Modal */
.event-details-content {
    margin-bottom: 20px;
}

.detail-group {
    margin-bottom: 15px;
}

.detail-group label {
    display: block;
    font-weight: 600;
    color: var(--color-text-muted);
    font-size: 12px;
    text-transform: uppercase;
    margin-bottom: 5px;
}

.detail-group p {
    margin: 0;
    color: var(--color-text-dark);
    font-size: 14px;
}

.detail-row {
    display: flex;
    gap: 20px;
}

.detail-row .detail-group {
    flex: 1;
}

/* Form Styles */
.form-group {
    margin-bottom: 15px;
}

.form-group label {
    display: block;
    margin-bottom: 5px;
    font-weight: 600;
    color: var(--color-text-dark);
}

.form-group input,
.form-group textarea {
    width: 100%;
    padding: 8px 12px;
    border: 1px solid var(--color-border);
    border-radius: 4px;
    font-size: 14px;
    font-family: inherit;
    box-sizing: border-box;
}

.form-group input:focus,
.form-group textarea:focus {
    outline: none;
    border-color: var(--color-primary);
}

.form-row {
    display: flex;
    gap: 15px;
}

.form-row .form-group {
    flex: 1;
}

.message {
    margin-bottom: 15px;
    padding: 10px;
    border-radius: 4px;
    font-weight: 500;
}

.message.success {
    background: var(--color-success-bg);
    color: var(--color-success-text);
    border: 1px solid var(--color-success-border);
}

.message.error {
    background: var(--color-danger-bg);
    color: var(--color-danger-text);
    border: 1px solid var(--color-danger-border);
}

.modal-actions {
    display: flex;
    gap: 10px;
    justify-content: flex-end;
    margin-top: 20px;
}

.btn-cancel {
    padding: 10px 20px;
    background: var(--color-bg-subtle-2);
    color: var(--color-text-dark);
    border: 1px solid var(--color-border);
    border-radius: 4px;
    font-size: 14px;
    font-weight: 600;
    cursor: pointer;
    transition: background 0.2s;
}

.btn-cancel:hover {
    background: var(--color-border);
}

.btn-create {
    padding: 10px 20px;
    background: var(--color-primary);
    color: white;
    border: none;
    border-radius: 4px;
    font-size: 14px;
    font-weight: 600;
    cursor: pointer;
    transition: background 0.2s;
}

.btn-create:hover:not(:disabled) {
    background: var(--color-primary-hover);
}

.btn-create:disabled {
    background: var(--color-border-strong);
    cursor: not-allowed;
}

.btn-delete {
    padding: 10px 20px;
    background: var(--color-danger);
    color: white;
    border: none;
    border-radius: 4px;
    font-size: 14px;
    font-weight: 600;
    cursor: pointer;
    transition: background 0.2s;
}

.btn-delete:hover {
    background: var(--color-danger-hover);
}

.btn-edit {
    padding: 10px 20px;
    background: var(--color-primary);
    color: white;
    border: none;
    border-radius: 4px;
    font-size: 14px;
    font-weight: 600;
    cursor: pointer;
    transition: background 0.2s;
}

.btn-edit:hover {
    background: var(--color-primary-hover);
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
    border: 2px solid var(--color-border);
    cursor: pointer;
    transition: all 0.2s;
    padding: 0;
}

.color-swatch:hover {
    transform: scale(1.1);
    border-color: var(--color-text-subtle);
}

.color-swatch.selected {
    border-color: var(--color-primary);
    border-width: 3px;
    box-shadow: 0 0 0 2px rgba(74, 144, 226, 0.2);
}

.btn-add-event {
    background: var(--color-primary);
    border: none;
    border-radius: 3px;
    color: white;
    width: 18px;
    height: 18px;
    display: flex;
    align-items: center;
    justify-content: center;
    cursor: pointer;
    transition: background 0.2s, transform 0.2s;
    padding: 0;
    line-height: 1;
    flex-shrink: 0;
    font-size: 14px;
    font-weight: 600;
    position: absolute;
    right: 5px;
    bottom: 0;
}

.btn-add-event:hover {
    background: var(--color-primary-hover);
    transform: scale(1.1);
}

/* Weather overlay - similar to month view */
.week-weather {
    position: absolute;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    pointer-events: none;
    z-index: 1;
}

.week-weather :deep(.weather-triangle) {
    position: absolute;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    pointer-events: none;
    overflow: hidden;
}

.week-weather :deep(.weather-triangle::before) {
    content: '';
    position: absolute;
    top: -50%;
    right: -50%;
    width: 200%;
    height: 200%;
    border-radius: 50%;
    z-index: 0;
}

.week-weather::v-deep .weather-icon,
.week-weather :deep(.weather-icon) {
    position: absolute !important;
    top: 5px !important;
    right: 5px !important;
    width: 45px !important;
    height: 45px !important;
    filter: drop-shadow(0 2px 4px rgba(0, 0, 0, 0.25)) !important;
    z-index: 100 !important;
    pointer-events: all !important;
}

.week-weather::v-deep .temp-display,
.week-weather :deep(.temp-display) {
    position: absolute !important;
    top: 5px !important;
    right: 55px !important;
    font-size: 14px !important;
    font-weight: 800 !important;
    color: var(--color-white) !important;
    text-shadow:
        0 1px 3px rgba(0, 0, 0, 0.6),
        0 0 6px rgba(0, 0, 0, 0.4) !important;
    pointer-events: none !important;
    z-index: 100 !important;
    letter-spacing: -0.5px !important;
}

/* Additional fallback selectors */
.week-weather >>> .weather-icon {
    position: absolute !important;
    top: 5px !important;
    right: 5px !important;
    width: 45px !important;
    height: 45px !important;
    filter: drop-shadow(0 2px 4px rgba(0, 0, 0, 0.25)) !important;
    z-index: 100 !important;
    pointer-events: all !important;
}

.week-weather >>> .temp-display {
    position: absolute !important;
    top: 5px !important;
    right: 55px !important;
    font-size: 14px !important;
    font-weight: 800 !important;
    color: var(--color-white) !important;
    text-shadow:
        0 1px 3px rgba(0, 0, 0, 0.6),
        0 0 6px rgba(0, 0, 0, 0.4) !important;
    pointer-events: none !important;
    z-index: 100 !important;
    letter-spacing: -0.5px !important;
}

/* Mobile-specific styles */
.day-name-abbr {
    display: none;
}

.event-time-abbr {
    display: none;
}

/* Tablet and medium screens */
@media screen and (max-width: 1024px) {
    .day-name-full {
        display: none;
    }

    .day-name-abbr {
        display: inline;
    }
}

/* Mobile responsive styles */
@media screen and (max-width: 800px) {
    .day-name-full {
        display: none;
    }

    .day-name-abbr {
        display: inline;
    }

    .event-time-full {
        display: none;
    }

    .event-time-abbr {
        display: inline;
    }

    .day-name {
        font-size: 0.75rem;
        margin-top: 15px;
        margin-bottom: 3px;
    }

    .day-number {
        font-size: 1.1rem;
        margin-bottom: 3px;
    }

    .week-content {
        padding: 5px;
    }

    .event-item {
        padding: 5px;
        border-left-width: 2px;
    }

    .event-time {
        font-size: 0.65rem;
        margin-bottom: 2px;
    }

    .event-title {
        font-size: 0.7rem;
    }

    .events-container {
        gap: 5px;
    }

    .loading,
    .no-events {
        font-size: 0.7rem;
        margin-top: 10px;
    }

    /* Reorganize week header layout for mobile */
    .week-header {
        min-height: 65px;
        padding: 3px;
        display: flex;
        flex-direction: column;
        align-items: flex-start;
        position: relative;
    }

    .day-name {
        font-size: 0.65rem;
        margin-top: 18px;
        margin-bottom: 2px;
        align-self: center;
        width: 100%;
        text-align: center;
    }

    .header-bottom {
        position: absolute;
        top: 3px;
        left: 3px;
    }

    .day-number {
        font-size: 0.95rem;
        margin-bottom: 0;
    }

    /* Adjust weather icons for mobile */
    .week-weather :deep(.weather-icon) {
        width: 26px !important;
        height: 26px !important;
        top: 2px !important;
        right: 2px !important;
        opacity: 1 !important;
    }

    .week-weather :deep(.temp-display) {
        font-size: 10px !important;
        bottom: 2px !important;
        right: 2px !important;
        top: auto !important;
        left: auto !important;
        font-weight: 800 !important;
        text-shadow:
            0 1px 2px rgba(0, 0, 0, 0.7),
            0 0 4px rgba(0, 0, 0, 0.5) !important;
    }

    .week-weather :deep(.weather-triangle::before) {
        opacity: 0.7;
    }
}

</style>

<style>
/* Unscoped styles to override WeatherIcon in WeekView - using exact attribute selector */
.week-view[data-v-54127138] .week-weather .weather-icon[data-v-157d367b],
.week-view .week-weather .weather-icon[data-v-157d367b],
.week-weather .weather-icon[data-v-157d367b],
svg.weather-icon[data-v-157d367b] {
    position: absolute !important;
    top: 5px !important;
    right: 5px !important;
    width: 45px !important;
    height: 45px !important;
    filter: drop-shadow(0 2px 4px rgba(0, 0, 0, 0.25)) !important;
    z-index: 100 !important;
    pointer-events: all !important;
}

.week-view[data-v-54127138] .week-weather .temp-display[data-v-157d367b],
.week-view .week-weather .temp-display[data-v-157d367b],
.week-weather .temp-display[data-v-157d367b],
div.temp-display[data-v-157d367b] {
    position: absolute !important;
    top: 5px !important;
    right: 55px !important;
    font-size: 14px !important;
    font-weight: 800 !important;
    color: var(--color-white) !important;
    text-shadow:
        0 1px 3px rgba(0, 0, 0, 0.6),
        0 0 6px rgba(0, 0, 0, 0.4) !important;
    pointer-events: none !important;
    z-index: 100 !important;
    letter-spacing: -0.5px !important;
}

/* Mobile overrides for unscoped weather styles */
@media screen and (max-width: 800px) {
    .week-view[data-v-54127138] .week-weather .weather-icon[data-v-157d367b],
    .week-view .week-weather .weather-icon[data-v-157d367b],
    .week-weather .weather-icon[data-v-157d367b],
    svg.weather-icon[data-v-157d367b] {
        width: 26px !important;
        height: 26px !important;
        top: 2px !important;
        right: 2px !important;
    }

    .week-view[data-v-54127138] .week-weather .temp-display[data-v-157d367b],
    .week-view .week-weather .temp-display[data-v-157d367b],
    .week-weather .temp-display[data-v-157d367b],
    div.temp-display[data-v-157d367b] {
        font-size: 10px !important;
        bottom: 2px !important;
        right: 2px !important;
        top: auto !important;
        left: auto !important;
        text-shadow:
            0 1px 2px rgba(0, 0, 0, 0.7),
            0 0 4px rgba(0, 0, 0, 0.5) !important;
    }
}
</style>
