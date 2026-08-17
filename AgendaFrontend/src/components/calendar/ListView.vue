<template>
    <div class="list-view" ref="scrollContainer" @scroll="handleScroll">
        <div v-if="isInitializing" class="loading-container">
            <div class="loading-message">Loading calendar...</div>
        </div>
        <div v-else class="days-list">
            <div
                v-for="day in days"
                :key="`${day.year}-${day.month}-${day.date}`"
                class="day-item"
                @click="openDayModal(day)"
            >
                <!-- Weather background overlay -->
                <WeatherIcon
                    v-if="day.weather"
                    :weatherCode="day.weather.weatherCode"
                    :tempMin="day.weather.temperatureMin"
                    :tempMax="day.weather.temperatureMax"
                    class="day-weather-icon"
                />

                <div class="day-item-header">
                    <div class="date-info">
                        <div class="day-number">{{ day.date }}</div>
                        <div class="day-meta">
                            <div class="day-name">{{ day.dayName }}</div>
                            <div class="month-year">{{ day.monthName }} {{ day.year }}</div>
                        </div>
                    </div>
                </div>

                <div class="day-events">
                    <div v-if="loadingEvents" class="loading-small">Loading...</div>
                    <div v-else-if="day.events.length === 0" class="no-events">No events</div>
                    <div v-else class="events-list">
                        <div
                            v-for="event in day.events"
                            :key="event.id"
                            class="event-item"
                            @click.stop="openEventDetails(event)"
                        >
                            <span class="event-dot" :style="{ backgroundColor: event.color || 'var(--color-primary)' }"></span>
                            <span class="event-title">{{ event.title }}</span>
                            <span class="event-time">{{ formatEventTime(event.startDateTime) }}</span>
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <!-- Loading indicator for infinite scroll -->
        <div v-if="isLoadingMore" class="loading-more">Loading more days...</div>

        <!-- Day Modal -->
        <DayModal
            :show="showDayModal"
            :currentMonth="modalMonth"
            :currentYear="modalYear"
            :selectedDay="modalDay"
            :monthImageUrl="monthImageUrl"
            @close="closeDayModal"
        />

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
import { ref, onMounted, computed, watch, reactive, nextTick } from 'vue';
import dayjs from 'dayjs';
import weekOfYear from 'dayjs/plugin/weekOfYear';
import isoWeek from 'dayjs/plugin/isoWeek';
import { AgendaAPI, EventWithOwnerDto, EventOccurrenceDto } from '@/api/agenda-api-swagger';
import { authenticatedAxios, getApiBaseUrl } from '@/api/axios-config';
import DayModal from './DayModal.vue';
import WeatherIcon from '../weather/WeatherIcon.vue';
import EventDetailsModal from './EventDetailsModal.vue';
import EventFormModal from './EventFormModal.vue';
import RecurringEventPromptModal from './RecurringEventPromptModal.vue';
import RecurringEventEditPromptModal from './RecurringEventEditPromptModal.vue';
import { useWeather } from '@/composables/useWeather';
import { useEventOperations, EventFormData } from '@/composables/useEventOperations';
import type { CalendarDate } from '@/types/calendar';

dayjs.extend(weekOfYear);
dayjs.extend(isoWeek);

interface Props {
    currentMonth: number;
    currentYear: number;
    selectedDay: number;
    monthImageUrl?: string;
}

interface WeatherForecast {
    date: string;
    temperatureMax: number;
    temperatureMin: number;
    weatherCode: number;
    precipitationProbability: number;
}

interface DayData {
    date: number;
    month: number;
    year: number;
    dayName: string;
    monthName: string;
    events: EventWithOwnerDto[];
    weather: WeatherForecast | null;
}

const props = defineProps<Props>();

const emit = defineEmits<{
    (e: 'dateSelect', date: CalendarDate): void;
    (e: 'visibleDateChange', date: { date: number; month: number; year: number }): void;
}>();

const api = new AgendaAPI(getApiBaseUrl(), authenticatedAxios);
const { createEvent, updateEvent, deleteEvent, editOccurrence, confirmDelete, isRecurringEvent, formatErrorMessage } = useEventOperations();

const days = ref<DayData[]>([]);
const loadingEvents = ref(false);
const isLoadingMore = ref(false);
const isInitializing = ref(true);
const scrollContainer = ref<HTMLElement | null>(null);
const isUpdatingFromScroll = ref(false);
let scrollUpdateTimeout: ReturnType<typeof setTimeout> | null = null;

// Day Modal state
const showDayModal = ref(false);
const modalDay = ref(1);
const modalMonth = ref(0);
const modalYear = ref(2024);

// Event Details Modal state
const showDetailsModal = ref(false);
const selectedEvent = ref<EventWithOwnerDto | null>(null);

// Event Form Modal state
const showFormModal = ref(false);
const eventToEdit = ref<EventWithOwnerDto | null>(null);
const formModalRef = ref<InstanceType<typeof EventFormModal> | null>(null);

// Recurring event prompts state
const showRecurringDeletePrompt = ref(false);
const showRecurringEditPrompt = ref(false);
const eventToDelete = ref<EventWithOwnerDto | null>(null);
const pendingEditEvent = ref<EventWithOwnerDto | null>(null);
const editSeriesMode = ref(false);

// Weather functionality
const { weatherData, fetchWeather, getWeatherForDate } = useWeather();

// Track current range of loaded days
// Initialize to the currently selected month from props
const startDate = ref(dayjs().year(props.currentYear).month(props.currentMonth).startOf('month'));
const endDate = ref(dayjs().year(props.currentYear).month(props.currentMonth).endOf('month').add(1, 'month'));

// Generate days list synchronously (without events/weather)
function generateDaysListSync() {
    days.value = [];
    let current = startDate.value.clone();

    while (current.isBefore(endDate.value) || current.isSame(endDate.value, 'day')) {
        days.value.push({
            date: current.date(),
            month: current.month(),
            year: current.year(),
            dayName: current.format('dddd'),
            monthName: current.format('MMMM'),
            events: [],
            weather: null
        });
        current = current.add(1, 'day');
    }
}

// Generate initial days list (with events/weather)
async function generateDaysList() {
    generateDaysListSync();
    await loadEventsForRange(startDate.value, endDate.value);
    updateWeatherForDays();
}

// Load events for a date range
async function loadEventsForRange(start: dayjs.Dayjs, end: dayjs.Dayjs) {
    loadingEvents.value = true;

    try {
        const occurrences = await api.occurrences(start.toDate(), end.toDate());

        // Convert occurrences directly to Event objects for display (no individual API calls)
        occurrences.forEach(occurrence => {
            if (!occurrence.occurrenceStart || !occurrence.eventId) return;

            // Create a display event from occurrence data
            const displayEvent = new EventWithOwnerDto({
                id: occurrence.eventId,
                startDateTime: occurrence.occurrenceStart,
                endDateTime: occurrence.occurrenceEnd,
                title: occurrence.title || '',
                description: occurrence.description,
                color: occurrence.color,
                isRecurring: occurrence.isRecurring,
                userId: 0 // Not needed for display
            });

            const eventDate = dayjs(occurrence.occurrenceStart);
            const dayIndex = days.value.findIndex(d =>
                d.date === eventDate.date() &&
                d.month === eventDate.month() &&
                d.year === eventDate.year()
            );

            if (dayIndex !== -1) {
                days.value[dayIndex].events.push(displayEvent);
            }
        });

        // Sort events by start time for each day
        days.value.forEach(day => {
            day.events.sort((a, b) => {
                if (!a.startDateTime || !b.startDateTime) return 0;
                return new Date(a.startDateTime).getTime() - new Date(b.startDateTime).getTime();
            });
        });
    } catch (error) {
        console.error('Failed to load events:', error);
    } finally {
        loadingEvents.value = false;
    }
}

// Update weather for all days
function updateWeatherForDays() {
    const today = dayjs().startOf('day');

    // Create a new array to trigger reactivity
    days.value = days.value.map(day => {
        const dayDate = dayjs().year(day.year).month(day.month).date(day.date).startOf('day');
        const daysDiff = dayDate.diff(today, 'day');

        // Only show weather for next 14 days
        if (daysDiff >= 0 && daysDiff < 14) {
            const weather = getWeatherForDate(dayDate.toDate());
            if (weather) {
                return { ...day, weather };
            }
        }
        return day;
    });
}

// Flag to prevent scroll handler from interfering with initial scroll
const isInitialScrolling = ref(true);

// Infinite scroll handler
async function handleScroll() {
    if (!scrollContainer.value || isLoadingMore.value || isInitialScrolling.value) return;

    const element = scrollContainer.value;
    const scrollTop = element.scrollTop;
    const scrollHeight = element.scrollHeight;
    const clientHeight = element.clientHeight;

    // Load more when scrolled to bottom (with 200px threshold)
    if (scrollHeight - scrollTop - clientHeight < 200) {
        await loadMoreDays();
    }

    // Load previous month when scrolled to top (with 200px threshold)
    if (scrollTop < 200 && days.value.length > 0) {
        await loadPreviousDays();
    }

    // Detect currently visible day
    detectVisibleDay();
}

// Detect which day is currently at the top of the viewport
function detectVisibleDay() {
    if (!scrollContainer.value) return;

    const containerRect = scrollContainer.value.getBoundingClientRect();
    const dayElements = scrollContainer.value.querySelectorAll('.day-item');

    for (const dayEl of Array.from(dayElements)) {
        const rect = dayEl.getBoundingClientRect();
        // Check if this day item is in the top portion of the viewport
        if (rect.top >= containerRect.top && rect.top < containerRect.top + 100) {
            const index = Array.from(dayElements).indexOf(dayEl);
            if (index >= 0 && index < days.value.length) {
                const day = days.value[index];

                // Only set the scroll flag if the visible month is DIFFERENT from current prop
                // This prevents the flag from being set during swipe gestures on the same month
                const isMonthChanging = day.month !== props.currentMonth || day.year !== props.currentYear;

                if (isMonthChanging) {
                    // Mark that this update is from scrolling, not external navigation
                    isUpdatingFromScroll.value = true;
                }

                emit('visibleDateChange', {
                    date: day.date,
                    month: day.month,
                    year: day.year
                });

                // Clear the flag after emitting
                if (isMonthChanging) {
                    // Use a small delay to ensure the parent's update completes
                    setTimeout(() => {
                        isUpdatingFromScroll.value = false;
                    }, 50);
                }
                break;
            }
        }
    }
}

// Load previous days (previous month)
async function loadPreviousDays() {
    if (isLoadingMore.value) return;
    isLoadingMore.value = true;

    try {
        const prevMonthEnd = startDate.value.clone().subtract(1, 'day');
        const prevMonthStart = prevMonthEnd.clone().startOf('month');

        // Generate new days
        let current = prevMonthStart.clone();
        const newDays: DayData[] = [];

        while (current.isBefore(prevMonthEnd) || current.isSame(prevMonthEnd, 'day')) {
            newDays.push({
                date: current.date(),
                month: current.month(),
                year: current.year(),
                dayName: current.format('dddd'),
                monthName: current.format('MMMM'),
                events: [],
                weather: null
            });
            current = current.add(1, 'day');
        }

        // Load occurrences for new range
        const occurrences = await api.occurrences(prevMonthStart.toDate(), prevMonthEnd.toDate());

        // Convert occurrences directly to Event objects for display (no individual API calls)
        occurrences.forEach(occurrence => {
            if (!occurrence.occurrenceStart || !occurrence.eventId) return;

            const displayEvent = new EventWithOwnerDto({
                id: occurrence.eventId,
                startDateTime: occurrence.occurrenceStart,
                endDateTime: occurrence.occurrenceEnd,
                title: occurrence.title || '',
                description: occurrence.description,
                color: occurrence.color,
                isRecurring: occurrence.isRecurring,
                userId: 0
            });

            const eventDate = dayjs(occurrence.occurrenceStart);
            const dayIndex = newDays.findIndex(d =>
                d.date === eventDate.date() &&
                d.month === eventDate.month() &&
                d.year === eventDate.year()
            );

            if (dayIndex !== -1) {
                newDays[dayIndex].events.push(displayEvent);
            }
        });

        // Sort events for new days
        newDays.forEach(day => {
            day.events.sort((a, b) => {
                if (!a.startDateTime || !b.startDateTime) return 0;
                return new Date(a.startDateTime).getTime() - new Date(b.startDateTime).getTime();
            });
        });

        // Save current scroll position
        const currentScrollHeight = scrollContainer.value?.scrollHeight || 0;

        // Add new days to the beginning of the list
        days.value.unshift(...newDays);

        // Update start date
        startDate.value = prevMonthStart;

        // Update weather for new days
        updateWeatherForDays();

        // Restore scroll position (adjust for new content added above)
        await new Promise(resolve => setTimeout(resolve, 0)); // Wait for DOM update
        if (scrollContainer.value) {
            const newScrollHeight = scrollContainer.value.scrollHeight;
            scrollContainer.value.scrollTop += (newScrollHeight - currentScrollHeight);
        }
    } catch (error) {
        console.error('Failed to load previous days:', error);
    } finally {
        isLoadingMore.value = false;
    }
}

// Load more days (next month)
async function loadMoreDays() {
    isLoadingMore.value = true;

    try {
        const nextMonthStart = endDate.value.clone().add(1, 'day');
        const nextMonthEnd = nextMonthStart.clone().add(1, 'month').endOf('month');

        // Generate new days
        let current = nextMonthStart.clone();
        const newDays: DayData[] = [];

        while (current.isBefore(nextMonthEnd) || current.isSame(nextMonthEnd, 'day')) {
            newDays.push({
                date: current.date(),
                month: current.month(),
                year: current.year(),
                dayName: current.format('dddd'),
                monthName: current.format('MMMM'),
                events: [],
                weather: null
            });
            current = current.add(1, 'day');
        }

        // Add new days to the list
        days.value.push(...newDays);

        // Load occurrences for new range
        const occurrences = await api.occurrences(nextMonthStart.toDate(), nextMonthEnd.toDate());

        // Convert occurrences directly to Event objects for display (no individual API calls)
        occurrences.forEach(occurrence => {
            if (!occurrence.occurrenceStart || !occurrence.eventId) return;

            const displayEvent = new EventWithOwnerDto({
                id: occurrence.eventId,
                startDateTime: occurrence.occurrenceStart,
                endDateTime: occurrence.occurrenceEnd,
                title: occurrence.title || '',
                description: occurrence.description,
                color: occurrence.color,
                isRecurring: occurrence.isRecurring,
                userId: 0
            });

            const eventDate = dayjs(occurrence.occurrenceStart);
            const dayIndex = days.value.findIndex(d =>
                d.date === eventDate.date() &&
                d.month === eventDate.month() &&
                d.year === eventDate.year()
            );

            if (dayIndex !== -1) {
                days.value[dayIndex].events.push(displayEvent);
            }
        });

        // Sort events for new days
        newDays.forEach((_, index) => {
            const actualIndex = days.value.length - newDays.length + index;
            days.value[actualIndex].events.sort((a, b) => {
                if (!a.startDateTime || !b.startDateTime) return 0;
                return new Date(a.startDateTime).getTime() - new Date(b.startDateTime).getTime();
            });
        });

        // Update end date
        endDate.value = nextMonthEnd;

        // Update weather for new days
        updateWeatherForDays();
    } catch (error) {
        console.error('Failed to load more days:', error);
    } finally {
        isLoadingMore.value = false;
    }
}

function formatEventTime(date?: Date): string {
    if (!date) return '';
    return dayjs(date).format('HH:mm');
}

function openDayModal(day: DayData) {
    // Emit dateSelect to update the parent's selected date
    emit('dateSelect', {
        date: day.date,
        month: day.month,
        year: day.year,
        thisMonth: day.month === props.currentMonth
    });

    modalDay.value = day.date;
    modalMonth.value = day.month;
    modalYear.value = day.year;
    showDayModal.value = true;
}

function closeDayModal() {
    showDayModal.value = false;
}

async function openEventDetails(event: EventWithOwnerDto) {
    if (!event.startDateTime || !event.id) return;

    const eventDate = dayjs(event.startDateTime);

    // Emit dateSelect to update the parent's selected date
    emit('dateSelect', {
        date: eventDate.date(),
        month: eventDate.month(),
        year: eventDate.year(),
        thisMonth: eventDate.month() === props.currentMonth
    });

    // Fetch full event details from API to get complete recurrence info
    try {
        const fullEvent = await api.eventsGET(event.id);
        // Store the occurrence date for use in single-occurrence edits
        // But keep the event's base date for display and "edit all" operations
        (fullEvent as any)._occurrenceDate = event.startDateTime;
        (fullEvent as any)._occurrenceEndDate = event.endDateTime;
        selectedEvent.value = fullEvent;
        showDetailsModal.value = true;
    } catch (error) {
        console.error('Failed to load event details:', error);
        // Fallback to display event if API call fails
        selectedEvent.value = event;
        showDetailsModal.value = true;
    }
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

    // For single occurrence edit, use the occurrence-specific date
    const eventToEditCopy = { ...pendingEditEvent.value };
    const occurrenceDate = (pendingEditEvent.value as any)._occurrenceDate;
    const occurrenceEndDate = (pendingEditEvent.value as any)._occurrenceEndDate;

    if (occurrenceDate) {
        eventToEditCopy.startDateTime = occurrenceDate;
        eventToEditCopy.endDateTime = occurrenceEndDate;
    }

    eventToEdit.value = eventToEditCopy;
    editSeriesMode.value = false;
    showFormModal.value = true;
}

function editAllEvents() {
    if (!pendingEditEvent.value) return;
    showRecurringEditPrompt.value = false;
    eventToEdit.value = pendingEditEvent.value;
    editSeriesMode.value = true;
    showFormModal.value = true;
}

function cancelEdit() {
    showRecurringEditPrompt.value = false;
    pendingEditEvent.value = null;
}

function closeFormModal() {
    showFormModal.value = false;
    eventToEdit.value = null;
    pendingEditEvent.value = null;
}

async function handleFormSubmit(formData: EventFormData) {
    try {
        const selectedDate = eventToEdit.value
            ? dayjs(eventToEdit.value.startDateTime)
            : dayjs();

        if (eventToEdit.value && eventToEdit.value.id) {
            // Check if this is a recurring event edit
            const isRecurring = isRecurringEvent(eventToEdit.value);

            if (isRecurring && !editSeriesMode.value) {
                // Edit single occurrence - create exception and new event
                await editOccurrence(eventToEdit.value, formData, selectedDate);
                formModalRef.value?.setMessage('Occurrence updated successfully!', 'success');
            } else {
                // Edit entire series or non-recurring event
                await updateEvent(eventToEdit.value, formData, selectedDate);
                formModalRef.value?.setMessage('Event updated successfully!', 'success');
            }
        } else {
            // Create new event
            await createEvent(formData, selectedDate);
            formModalRef.value?.setMessage('Event created successfully!', 'success');
        }

        // Reload events after successful save
        await reloadEvents();

        // Close form after delay
        setTimeout(() => {
            closeFormModal();
        }, 1500);
    } catch (error: any) {
        const errorMessage = formatErrorMessage(error);
        formModalRef.value?.setMessage(errorMessage, 'error');
    }
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
                await reloadEvents();
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
        await reloadEvents();
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
        await reloadEvents();
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

async function reloadEvents() {
    // Reload the current view to reflect changes
    const firstDay = days.value[0];
    if (firstDay) {
        const startDate = dayjs().date(firstDay.date).month(firstDay.month).year(firstDay.year);
        const endDate = startDate.add(days.value.length, 'days');

        // Clear current days and reload
        days.value.forEach(day => day.events = []);
        await loadEventsForRange(startDate, endDate);
    }
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

// Watch for weather data changes and update days
watch(weatherData, () => {
    if (weatherData.value) {
        updateWeatherForDays();
    }
});

// Helper function to scroll to a specific month
function scrollToMonth(month: number, year: number, day: number) {
    if (!scrollContainer.value) return;

    nextTick().then(() => {
        setTimeout(() => {
            if (!scrollContainer.value) return;

            // Find the target day in the month
            const targetDayIndex = days.value.findIndex(d =>
                d.date === day &&
                d.month === month &&
                d.year === year
            );

            if (targetDayIndex !== -1) {
                const dayItems = scrollContainer.value.querySelectorAll('.day-item');
                if (dayItems && dayItems[targetDayIndex]) {
                    const targetElement = dayItems[targetDayIndex] as HTMLElement;
                    targetElement.scrollIntoView({ behavior: 'auto', block: 'start' });
                }
            } else {
                // If the selected day doesn't exist, scroll to first day of the month
                const firstDayIndex = days.value.findIndex(d =>
                    d.month === month && d.year === year
                );
                if (firstDayIndex !== -1) {
                    const dayItems = scrollContainer.value.querySelectorAll('.day-item');
                    if (dayItems && dayItems[firstDayIndex]) {
                        const targetElement = dayItems[firstDayIndex] as HTMLElement;
                        targetElement.scrollIntoView({ behavior: 'auto', block: 'start' });
                    }
                }
            }
        }, 100);
    });
}

// Watch for month/year changes from navigation buttons
watch(() => [props.currentMonth, props.currentYear], ([newMonth, newYear], [oldMonth, oldYear]) => {
    // If the values haven't actually changed, skip (but NOT on first run after mount)
    if (oldMonth !== undefined && oldMonth === newMonth && oldYear === newYear) {
        return;
    }

    // Skip only on the very first run (mount) when oldMonth is undefined
    if (oldMonth === undefined) {
        return;
    }

    // If this update came from natural scrolling (user manually scrolled and we detected the visible month),
    // don't reload or force scroll - the user is already viewing the correct content
    if (isUpdatingFromScroll.value) {
        isUpdatingFromScroll.value = false; // Reset the flag
        return;
    }

    // This is a navigation action (button click or swipe gesture)
    // Disable scroll handler during navigation to prevent infinite scroll from triggering
    isInitialScrolling.value = true;

    // Update the date range to show the selected month
    const newDate = dayjs().year(newYear).month(newMonth);
    startDate.value = newDate.startOf('month');
    endDate.value = newDate.endOf('month').add(1, 'month');

    // Regenerate the days list
    generateDaysListSync();

    // Reload events and weather for the new range
    loadEventsForRange(startDate.value, endDate.value);
    updateWeatherForDays();

    // Scroll to the selected day in the new month
    nextTick().then(() => {
        setTimeout(() => {
            if (!scrollContainer.value) return;

            // Find the selected day in the new month
            const targetDayIndex = days.value.findIndex(d =>
                d.date === props.selectedDay &&
                d.month === newMonth &&
                d.year === newYear
            );

            if (targetDayIndex !== -1) {
                const dayItems = scrollContainer.value.querySelectorAll('.day-item');
                if (dayItems && dayItems[targetDayIndex]) {
                    const targetElement = dayItems[targetDayIndex] as HTMLElement;
                    targetElement.scrollIntoView({ behavior: 'auto', block: 'start' });
                }
            } else {
                // If the selected day doesn't exist in the new month (e.g., Feb 30), scroll to first day
                const firstDayIndex = days.value.findIndex(d =>
                    d.month === newMonth && d.year === newYear
                );
                if (firstDayIndex !== -1) {
                    const dayItems = scrollContainer.value.querySelectorAll('.day-item');
                    if (dayItems && dayItems[firstDayIndex]) {
                        const targetElement = dayItems[firstDayIndex] as HTMLElement;
                        targetElement.scrollIntoView({ behavior: 'auto', block: 'start' });
                    }
                }
            }

            // Re-enable scroll handler after a longer delay to prevent interference
            setTimeout(() => {
                isInitialScrolling.value = false;
            }, 1000);
        }, 200);
    });
});

// Function to scroll to the currently selected date
function scrollToSelectedDate() {
    // Use the currently selected date from props (whatever is shown in the date indicator)
    const targetIndex = days.value.findIndex(d =>
        d.date === props.selectedDay &&
        d.month === props.currentMonth &&
        d.year === props.currentYear
    );

    if (targetIndex !== -1 && scrollContainer.value && days.value.length > 0) {
        // Wait for rendering and layout to complete
        nextTick().then(() => {
            setTimeout(() => {
                if (!scrollContainer.value) {
                    isInitialScrolling.value = false;
                    return;
                }
                const dayItems = scrollContainer.value.querySelectorAll('.day-item');
                if (dayItems && dayItems[targetIndex]) {
                    const targetElement = dayItems[targetIndex] as HTMLElement;
                    targetElement.scrollIntoView({ behavior: 'auto', block: 'start' });

                    // Re-enable scroll handler after a delay
                    setTimeout(() => {
                        isInitialScrolling.value = false;
                    }, 500);
                } else {
                    isInitialScrolling.value = false;
                }
            }, 200);
        });
    } else {
        isInitialScrolling.value = false;
    }
}

onMounted(async () => {
    try {
        // Generate days list immediately (without events/weather)
        generateDaysListSync();
        isInitializing.value = false;

        // Load events and weather in parallel (non-blocking)
        Promise.all([
            loadEventsForRange(startDate.value, endDate.value),
            initWeather().then(() => updateWeatherForDays())
        ]).catch(error => {
            console.error('Error loading data:', error);
        });

        // Wait for next tick to ensure scrollContainer ref is available
        await nextTick();

        // Scroll to the currently selected date (from the date indicator)
        scrollToSelectedDate();
    } catch (error) {
        console.error('Error initializing ListView:', error);
        isInitializing.value = false;
    }
});

</script>

<style scoped>
.list-view {
    height: 60vh;
    overflow-y: auto;
    background: var(--color-surface);
    scroll-behavior: smooth;
}

.loading-container {
    display: flex;
    justify-content: center;
    align-items: center;
    height: 100%;
    min-height: 300px;
}

.loading-message {
    font-size: 1.1rem;
    color: var(--color-text-muted);
    font-weight: 500;
}

.days-list {
    display: flex;
    flex-direction: column;
}

.day-item {
    border-bottom: 1px solid var(--color-border);
    padding: 16px;
    cursor: pointer;
    transition: background-color 0.2s;
    position: relative;
    overflow: hidden;
}

.day-item:hover {
    background-color: rgba(249, 249, 249, 0.8);
}

.day-item-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 12px;
    position: relative;
    z-index: 2;
}

.date-info {
    display: flex;
    align-items: center;
    gap: 16px;
}

.day-number {
    font-size: 2rem;
    font-weight: 700;
    color: var(--color-text-dark);
    min-width: 50px;
    text-align: center;
}

.day-meta {
    display: flex;
    flex-direction: column;
    gap: 2px;
}

.day-name {
    font-size: 1rem;
    font-weight: 600;
    color: var(--color-text-dark);
}

.month-year {
    font-size: 0.85rem;
    color: var(--color-text-muted);
    white-space: nowrap;
}

.day-weather-icon {
    position: absolute;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    z-index: 0;
}

.day-events {
    padding-left: 66px;
    position: relative;
    z-index: 2;
}

.loading-small {
    font-size: 0.85rem;
    color: var(--color-text-subtle);
}

.no-events {
    font-size: 0.85rem;
    color: var(--color-text-subtle);
    font-style: italic;
}

.events-list {
    display: flex;
    flex-direction: column;
    gap: 8px;
}

.event-item {
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 8px 12px;
    border-radius: 6px;
    transition: background-color 0.2s;
}

.event-item:hover {
    background: rgba(255, 255, 255, 0.3);
}

.event-dot {
    width: 10px;
    height: 10px;
    border-radius: 50%;
    flex-shrink: 0;
}

.event-title {
    flex: 1;
    font-size: 0.9rem;
    color: var(--color-text-dark);
    font-weight: 500;
}

.event-time {
    font-size: 0.85rem;
    color: var(--color-text-muted);
    font-weight: 600;
    flex-shrink: 0;
}

.loading-more {
    padding: 20px;
    text-align: center;
    color: var(--color-text-muted);
    font-style: italic;
}

/* Mobile styles */
@media screen and (max-width: 800px) {
    .list-view {
        height: calc(100vh - 200px - env(safe-area-inset-top, 0px) - env(safe-area-inset-bottom, 0px));
        overflow-y: auto;
        overscroll-behavior: contain;
    }

    .day-item {
        padding: 12px;
    }

    .day-number {
        font-size: 1.5rem;
        min-width: 40px;
    }

    .day-name {
        font-size: 0.9rem;
    }

    .month-year {
        font-size: 0.75rem;
    }

    .day-events {
        padding-left: 56px;
    }

    .event-item {
        padding: 6px 10px;
        gap: 8px;
    }

    .event-dot {
        width: 8px;
        height: 8px;
    }

    .event-title {
        font-size: 0.85rem;
    }

    .event-time {
        font-size: 0.75rem;
    }
}
</style>
