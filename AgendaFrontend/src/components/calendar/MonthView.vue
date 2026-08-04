<template>
    <div class="days">
        <div v-for="(day, index) in days" :key="day" class="day">
            <span class="day-name-full">{{ day }}</span>
            <span class="day-name-abbr">{{ daysAbbreviated[index] }}</span>
        </div>
        <template v-for="(d, index) in dates" :key="index">
            <div :class="['day-cell', d.thisMonth ? 'this-month' : 'other-month']" @click="onDateSelect(d)">
                <div class="day-header">
                    <div class="day-number">{{ d.date }}</div>
                </div>
                <div class="day-events">
                    <div v-if="loadingEvents" class="loading-small">...</div>
                    <template v-else>
                        <div
                            v-for="(occurrence, idx) in getVisibleEvents(d, index)"
                            :key="`${occurrence.eventId}-${occurrence.occurrenceStart?.getTime()}`"
                            class="event-tag"
                            :class="{ 'show-title-mode': showEventTitleInMonthView }"
                        >
                            <span class="event-dot" :style="{ backgroundColor: occurrence.color || 'var(--color-primary)' }"></span>
                            <span class="event-title-text" v-if="!showEventTitleInMonthView">{{ occurrence.title }}</span>
                            <span class="event-title-text event-title-full" v-if="showEventTitleInMonthView">{{ occurrence.title }}</span>
                            <span class="event-time" v-if="!showEventTitleInMonthView">{{ formatEventTime(occurrence.occurrenceStart) }}</span>
                            <span class="event-time-mobile" v-if="!showEventTitleInMonthView">{{ formatEventTimeHour(occurrence.occurrenceStart) }}</span>
                        </div>
                        <div v-if="getMoreCount(d, index) > 0" class="more-events">
                            .. {{ getMoreCount(d, index) }} more
                        </div>
                    </template>
                </div>
                <!-- Weather icon overlay -->
                <WeatherIcon
                    v-if="getWeatherForDay(d)"
                    :weatherCode="getWeatherForDay(d)!.weatherCode"
                    :tempMin="getWeatherForDay(d)!.temperatureMin"
                    :tempMax="getWeatherForDay(d)!.temperatureMax"
                    :date="getDateForDay(d)"
                />
            </div>
        </template>

        <!-- Day Modal -->
        <DayModal
            :show="showDayModal"
            :currentMonth="modalMonth"
            :currentYear="modalYear"
            :selectedDay="modalDay"
            :monthImageUrl="monthImageUrl"
            @close="closeDayModal"
        />
    </div>
</template>

<script setup lang="ts">
import { ref, watch, onMounted, onUnmounted } from 'vue';
import dayjs from "dayjs";
import weekOfYear from 'dayjs/plugin/weekOfYear';
import isoWeek from 'dayjs/plugin/isoWeek';
import { AgendaAPI, EventOccurrenceDto } from '@/api/agenda-api-swagger';
import { authenticatedAxios, getApiBaseUrl } from '@/api/axios-config';
import DayModal from './DayModal.vue';
import WeatherIcon from '../weather/WeatherIcon.vue';
import { useWeather } from '@/composables/useWeather';
import type { CalendarDate } from '@/types/calendar';

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
    "M",
    "T",
    "W",
    "T",
    "F",
    "S",
    "S"
];

const dates = ref<CalendarDate[]>([]);
const monthOccurrences = ref<EventOccurrenceDto[]>([]);
const loadingEvents = ref(false);
const MAX_VISIBLE_EVENTS = 3;
const showEventTitleInMonthView = ref(false);

// Day Modal state
const showDayModal = ref(false);
const modalDay = ref(1);
const modalMonth = ref(0);
const modalYear = ref(2024);

// Weather functionality
const { weatherData, fetchWeather, getWeatherForDate } = useWeather();

// Helper to get the actual date for a calendar day
function getDateForDay(d: CalendarDate): Date {
    let targetMonth = props.currentMonth;
    let targetYear = props.currentYear;

    if (!d.thisMonth) {
        if (d.date > 20) {
            targetMonth = props.currentMonth - 1;
            if (targetMonth < 0) {
                targetMonth = 11;
                targetYear = props.currentYear - 1;
            }
        } else {
            targetMonth = props.currentMonth + 1;
            if (targetMonth > 11) {
                targetMonth = 0;
                targetYear = props.currentYear + 1;
            }
        }
    }

    return dayjs().year(targetYear).month(targetMonth).date(d.date).hour(0).minute(0).second(0).millisecond(0).toDate();
}

// Get weather for a specific day
function getWeatherForDay(d: CalendarDate) {
    const date = getDateForDay(d);
    const today = dayjs().startOf('day');
    const dayDate = dayjs(date).startOf('day');
    const daysDiff = dayDate.diff(today, 'day');

    // Only show weather for next 14 days
    if (daysDiff < 0 || daysDiff >= 14) {
        return null;
    }

    return getWeatherForDate(date);
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

// Load user preferences
async function loadUserPreferences() {
    try {
        const response = await authenticatedAxios.get(`${getApiBaseUrl()}/api/UserPreferences`);
        showEventTitleInMonthView.value = response.data.showEventTitleInMonthView;
    } catch (error) {
        console.warn('Could not load user preferences:', error);
        // Keep default value (false)
    }
}

// Listen for preference updates from settings page
function handlePreferencesUpdate(event: CustomEvent) {
    showEventTitleInMonthView.value = event.detail.showEventTitleInMonthView;
}

function generateMonthView() {
    dates.value = [];

    let firstPeriod = false;
    let firstStartDate = 1;
    let secondPeriod = false;
    let secondStartDate = 1;
    let thirdPeriod = false;
    let thirdStartDate = 1;

    let selectedDate = dayjs().date(props.selectedDay).month(props.currentMonth).year(props.currentYear);
    let date = dayjs().month(props.currentMonth).year(props.currentYear);

    let startOfMonth = dayjs().month(props.currentMonth).year(props.currentYear).startOf('month');
    let endOfMonth = dayjs().month(props.currentMonth).year(props.currentYear).endOf('month');
    let startOfFirstWeek = startOfMonth.startOf('isoWeek');
    let endOfLastWeek = endOfMonth.endOf('isoWeek');

    let daysInMonth = startOfMonth.daysInMonth();
    let daysInPreceedingMonth = startOfFirstWeek.daysInMonth();

    secondPeriod = true;
    thirdPeriod = true;

    secondStartDate = parseInt(selectedDate.startOf('month').format('D'));
    thirdStartDate = parseInt(startOfMonth.add(1, 'month').format('D'));

    if (startOfFirstWeek.isBefore(selectedDate, 'month')) {
        firstPeriod = true;

        firstStartDate = parseInt(startOfMonth.startOf('isoWeek').format('D'));
        secondStartDate = parseInt(selectedDate.startOf('month').format('D'));
    }

    if (firstPeriod){
        for (let i = firstStartDate; i < daysInPreceedingMonth + 1; i++) {
            dates.value.push({
                date: i,
                day: date.date(i).month(props.currentMonth -1).year(props.currentYear).isoWeekday(),
                thisMonth: false
            });
        }
    }

    if (secondPeriod) {
        for (let i = secondStartDate; i <= daysInMonth; i++) {
            dates.value.push({
                date: i,
                day: date.date(i).isoWeekday(),
                thisMonth: true
            });
        }
    }

    if(thirdPeriod) {
        let iterator = 42 - dates.value.length;
        for (let i = thirdStartDate; i <= iterator; i++) {
            dates.value.push({
                date: i,
                day: date.date(i).month(props.currentMonth +1).year(props.currentYear).isoWeekday(),
                thisMonth: false
            });
        }
    }
}

async function loadMonthEvents() {
    loadingEvents.value = true;
    monthOccurrences.value = [];

    try {
        const startOfMonth = dayjs().month(props.currentMonth).year(props.currentYear).startOf('month');
        const endOfMonth = dayjs().month(props.currentMonth).year(props.currentYear).endOf('month');
        const startOfFirstWeek = startOfMonth.startOf('isoWeek');
        const endOfLastWeek = endOfMonth.endOf('isoWeek');

        // Load all event occurrences (including RRULE-calculated ones) for the visible calendar
        monthOccurrences.value = await api.occurrences(startOfFirstWeek.toDate(), endOfLastWeek.toDate());
    } catch (error) {
        console.error('Failed to load month events:', error);
        monthOccurrences.value = [];
    } finally {
        loadingEvents.value = false;
    }
}

function getEventsForDay(d: CalendarDate, index: number): EventOccurrenceDto[] {
    // Determine which month this date belongs to
    let targetMonth = props.currentMonth;
    if (!d.thisMonth) {
        if (d.date > 20) {
            // Previous month
            targetMonth = props.currentMonth - 1;
        } else {
            // Next month
            targetMonth = props.currentMonth + 1;
        }
    }

    const targetDate = dayjs()
        .date(d.date)
        .month(targetMonth)
        .year(props.currentYear);

    const occurrences = monthOccurrences.value.filter(occurrence => {
        if (!occurrence.occurrenceStart) return false;
        const occurrenceDate = dayjs(occurrence.occurrenceStart);
        return occurrenceDate.isSame(targetDate, 'day');
    });

    // Sort by start time
    return occurrences.sort((a, b) => {
        if (!a.occurrenceStart || !b.occurrenceStart) return 0;
        return new Date(a.occurrenceStart).getTime() - new Date(b.occurrenceStart).getTime();
    });
}

function getVisibleEvents(d: CalendarDate, index: number): EventOccurrenceDto[] {
    const events = getEventsForDay(d, index);
    return events.slice(0, MAX_VISIBLE_EVENTS);
}

function getMoreCount(d: CalendarDate, index: number): number {
    const events = getEventsForDay(d, index);
    return Math.max(0, events.length - MAX_VISIBLE_EVENTS);
}

function formatEventTime(date?: Date): string {
    if (!date) return '';
    return dayjs(date).format('HH:mm');
}

function formatEventTimeHour(date?: Date): string {
    if (!date) return '';
    return dayjs(date).format('HH');
}

function onDateSelect(d: CalendarDate) {
    // Determine which month this date belongs to
    let targetMonth = props.currentMonth;
    let targetYear = props.currentYear;

    if (!d.thisMonth) {
        if (d.date > 20) {
            // Previous month
            targetMonth = props.currentMonth - 1;
            if (targetMonth < 0) {
                targetMonth = 11;
                targetYear = props.currentYear - 1;
            }
        } else {
            // Next month
            targetMonth = props.currentMonth + 1;
            if (targetMonth > 11) {
                targetMonth = 0;
                targetYear = props.currentYear + 1;
            }
        }
    }

    // Emit dateSelect to update the parent's selected date
    emit('dateSelect', d);

    modalDay.value = d.date;
    modalMonth.value = targetMonth;
    modalYear.value = targetYear;
    showDayModal.value = true;
}

function closeDayModal() {
    showDayModal.value = false;
    // Reload month events to reflect any changes made in the day modal
    loadMonthEvents();
}

watch(() => [props.currentMonth, props.currentYear], () => {
    generateMonthView();
    loadMonthEvents();
});

onMounted(() => {
    generateMonthView();
    loadMonthEvents();
    initWeather();
    loadUserPreferences();

    // Listen for preference changes
    window.addEventListener('preferencesUpdated', handlePreferencesUpdate as EventListener);
});

// Clean up event listener on unmount
onUnmounted(() => {
    window.removeEventListener('preferencesUpdated', handlePreferencesUpdate as EventListener);
});

</script>

<style scoped>

.days div.day {
    border-bottom: 1px solid var(--color-border);
    display: flex;
    justify-content: center;
    align-items: center;
    color: var(--color-text-dark);
    font-weight: 700;
}

.days div.day:not(:nth-child(7n)) {
    border-right: 1px solid var(--color-border); 
}

.days div.day:nth-child(7) {
    color: #ff685d;
}

/* Day Cell */
.day-cell {
    border-bottom: 1px solid var(--color-border);
    cursor: pointer;
    display: flex !important;
    flex-direction: column !important;
    padding: 8px;
    overflow: hidden;
    transition: background-color 0.2s;
    align-items: stretch !important;
    justify-content: flex-start !important;
    position: relative;
}

.day-cell:nth-last-child(-n + 7) {
    border-bottom: none;
}

.day-cell:not(:nth-child(7n +7)) {
   border-right: 1px solid var(--color-border); 
}

.day-cell:hover {
    background-color: var(--color-bg-subtle);
}

.day-cell.other-month {
    background: hsla(0, 0%, 15%, 0.05);
}

.day-cell.other-month .day-number {
    color: var(--color-text-subtle);
}

/* Sunday styling */
.day-cell:nth-child(7n + 7) .day-number {
    color: #ff685d;
    font-weight: 700;
}

.day-header {
    width: 100%;
    margin-bottom: 5px;
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
    right: 7px;
    bottom: 7px;
    z-index: 1;
}

.btn-add-event:hover {
    background: var(--color-primary-hover);
    transform: scale(1.1);
}

.day-number {
    font-weight: 600;
    font-size: 0.9rem;
    color: var(--color-text-dark);
    text-align: left;
}

.day-events {
    display: flex;
    flex-direction: column;
    gap: 0;
    overflow: hidden;
    align-items: flex-start;
    width: 100%;
    position: relative;
    z-index: 2;
}

.loading-small {
    font-size: 0.7rem;
    color: var(--color-text-subtle);
    text-align: left;
}

.event-tag {
    display: flex;
    justify-content: space-between;
    align-items: center;
    font-size: 0.7rem;
    padding: 1px 5px 1px 3px;
    color: var(--color-text-dark);
    position: relative;
    width: 100%;
    line-height: 1.3;
    gap: 5px;
}

.event-dot {
    width: 8px;
    height: 8px;
    border-radius: 50%;
    flex-shrink: 0;
    margin-right: 2px;
}

.event-title-text {
    flex: 1;
    text-overflow: ellipsis;
    white-space: nowrap;
    overflow: hidden;
    min-width: 0;
}

.event-title-text.event-title-full {
    /* When showing title instead of time, allow it to take full width */
    flex: 1;
    text-overflow: ellipsis;
    white-space: nowrap;
    overflow: hidden;
}

.event-time {
    flex-shrink: 0;
    font-size: 0.65rem;
    color: var(--color-black) !important;
    font-weight: 700;
}

.more-events {
    font-size: 0.65rem;
    color: var(--color-text-muted);
    font-style: italic;
    padding: 1px 0 1px 13px;
    text-align: left;
    line-height: 1.3;
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

/* Mobile-specific styles */
.day-name-abbr {
    display: none;
}

.event-time-mobile {
    display: none;
}

/* Tablet and medium screens - use abbreviated day names */
@media screen and (max-width: 1024px) {
    .day-name-full {
        display: none;
    }

    .day-name-abbr {
        display: inline;
    }
}

@media screen and (max-width: 800px) {
    /* Hide full day names, show abbreviated */
    .day-name-full {
        display: none;
    }

    .day-name-abbr {
        display: inline;
    }

    /* Default mobile behavior: hide title, show only hour */
    .event-tag:not(.show-title-mode) .event-title-text {
        display: none;
    }

    .event-tag:not(.show-title-mode) .event-time {
        display: none;
    }

    .event-tag:not(.show-title-mode) .event-time-mobile {
        display: inline;
        font-size: 0.65rem;
        color: var(--color-black);
        font-weight: 700;
    }

    /* When in show-title mode: show title, hide time */
    .event-tag.show-title-mode .event-title-text {
        display: block;
        font-size: 0.6rem;
    }

    .event-tag.show-title-mode .event-time {
        display: none;
    }

    .event-tag.show-title-mode .event-time-mobile {
        display: none;
    }

    /* Adjust event tag for mobile - horizontal layout */
    .event-tag {
        flex-direction: row;
        justify-content: flex-start;
        gap: 3px;
        padding: 1px 3px;
    }

    /* Smaller dots on mobile */
    .event-dot {
        width: 6px;
        height: 6px;
    }

    /* Reduce day cell padding */
    .day-cell {
        padding: 2px 3px;
    }

    /* Smaller day numbers */
    .day-number {
        font-size: 0.75rem;
    }

    /* Adjust more-events indicator */
    .more-events {
        font-size: 0.55rem;
        padding: 1px 0 1px 9px;
    }

    /* Reduce day header margin */
    .day-header {
        margin-bottom: 3px;
    }

    /* Ensure day events section has proper spacing */
    .day-events {
        gap: 1px;
    }

    /* Optimize weather display for mobile */
    .day-cell :deep(.weather-icon) {
        width: 30px !important;
        height: 30px !important;
        top: 2px !important;
        right: 2px !important;
        opacity: 1 !important;
    }

    .day-cell :deep(.temp-display) {
        font-size: 11px !important;
        bottom: 2px !important;
        right: 2px !important;
        top: auto !important;
        left: auto !important;
        font-weight: 800 !important;
        text-shadow:
            0 1px 2px rgba(0, 0, 0, 0.7),
            0 0 4px rgba(0, 0, 0, 0.5) !important;
    }

    .day-cell :deep(.weather-triangle::before) {
        opacity: 0.75;
    }
}

</style>
