<template>
        <div class="calendar"
            @touchstart="handleTouchStart"
            @touchmove="handleTouchMove"
            @touchend="handleTouchEnd">
            <!-- Sidebar -->
            <Sidebar :isOpen="isSidebarOpen" @close="isSidebarOpen = false" />

            <div class="image" :style="backgroundImageStyle">
                <div class="view-toggle-buttons">
                    <button v-on:click="setView('list')" :class="['btn-view-toggle', 'btn-icon', { active: currentView === 'list' }]" title="List View">
                        <span class="material-symbols-outlined">list_alt</span>
                    </button>
                    <button v-on:click="setView('month')" :class="['btn-view-toggle', { active: currentView === 'month' }]">Month</button>
                    <button v-on:click="setView('week')" :class="['btn-view-toggle', { active: currentView === 'week' }]">Week</button>
                </div>

                <!-- Image control buttons -->
                <div class="image-controls">
                    <button class="image-control-btn" @click="isUploadModalOpen = true" title="Upload month image">
                        <span class="material-symbols-outlined">image_arrow_up</span>
                    </button>
                    <button class="image-control-btn" @click="resetToDefault" title="Reset to default image">
                        <span class="material-symbols-outlined">reset_image</span>
                    </button>
                </div>

                <!-- Settings menu button -->
                <button class="settings-menu" @click="isSidebarOpen = true">
                    <svg xmlns="http://www.w3.org/2000/svg" height="28" viewBox="0 -960 960 960" width="28" fill="white">
                        <path d="m370-80-16-128q-13-5-24.5-12T307-235l-119 50L78-375l103-78q-1-7-1-13.5v-27q0-6.5 1-13.5L78-585l110-190 119 50q11-8 23-15t24-12l16-128h220l16 128q13 5 24.5 12t22.5 15l119-50 110 190-103 78q1 7 1 13.5v27q0 6.5-2 13.5l103 78-110 190-118-50q-11 8-23 15t-24 12L590-80H370Zm70-80h79l14-106q31-8 57.5-23.5T639-327l99 41 39-68-86-65q5-14 7-29.5t2-31.5q0-16-2-31.5t-7-29.5l86-65-39-68-99 42q-22-23-48.5-38.5T533-694l-13-106h-79l-14 106q-31 8-57.5 23.5T321-633l-99-41-39 68 86 64q-5 15-7 30t-2 32q0 16 2 31t7 30l-86 65 39 68 99-42q22 23 48.5 38.5T427-266l13 106Zm42-180q58 0 99-41t41-99q0-58-41-99t-99-41q-59 0-99.5 41T342-480q0 58 40.5 99t99.5 41Zm-2-140Z"/>
                    </svg>
                </button>
                <div class="column date-display-container">
                    <div class="row dateGroupDay">
                        <h2 @click="isDatePickerOpen = !isDatePickerOpen" style="cursor: pointer;">{{ dateDisplay }}</h2>
                    </div>
                    <div class="row dateGroupYear">
                        <span><button type="button" class="dateNav" v-on:click="navigatePrevious"><</button></span>
                        <h3 @click="isDatePickerOpen = !isDatePickerOpen" style="cursor: pointer;">{{ periodDisplay }}</h3>
                        <span><button type="button" class="dateNav" v-on:click="navigateNext">></button></span>
                    </div>

                    <DatePickerPopover
                        :isOpen="isDatePickerOpen"
                        :currentMonth="month"
                        :currentYear="year"
                        :currentDay="dayOfMonth"
                        @close="isDatePickerOpen = false"
                        @dateSelect="handleDatePickerSelect"
                    />
                </div>
            </div>
            <ListView
                v-if="currentView === 'list'"
                :currentMonth="month"
                :currentYear="year"
                :selectedDay="dayOfMonth"
                :monthImageUrl="monthImageUrl || defaultImage"
                @dateSelect="handleDateSelect"
                @visibleDateChange="handleVisibleDateChange"
            />
            <MonthView
                v-if="currentView === 'month'"
                :currentMonth="month"
                :currentYear="year"
                :selectedDay="dayOfMonth"
                :monthImageUrl="monthImageUrl || defaultImage"
                @dateSelect="handleDateSelect"
            />
            <WeekView
                v-if="currentView === 'week'"
                :currentMonth="month"
                :currentYear="year"
                :selectedDay="dayOfMonth"
                :monthImageUrl="monthImageUrl || defaultImage"
                @dateSelect="handleDateSelect"
            />
            <DayView
                v-if="currentView === 'day'"
                :currentMonth="month"
                :currentYear="year"
                :selectedDay="dayOfMonth"
                :monthImageUrl="monthImageUrl || defaultImage"
            />

            <!-- Image Upload Modal -->
            <ImageUploadModal
                v-if="isUploadModalOpen"
                :month="month + 1"
                @close="isUploadModalOpen = false"
                @uploaded="handleImageUploaded"
            />
        </div>
</template>    
 
<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import dayjs from "dayjs";
import weekOfYear from 'dayjs/plugin/weekOfYear';
import isoWeek from 'dayjs/plugin/isoWeek';
import MonthView from '../components/calendar/MonthView.vue';
import WeekView from '../components/calendar/WeekView.vue';
import DayView from '../components/calendar/DayView.vue';
import ListView from '../components/calendar/ListView.vue';
import Sidebar from '../components/navigation/Sidebar.vue';
import ImageUploadModal from '../components/calendar/ImageUploadModal.vue';
import DatePickerPopover from '../components/calendar/DatePickerPopover.vue';
import { AgendaAPI } from '@/api/agenda-api-swagger';
import { authenticatedAxios, getApiBaseUrl } from '@/api/axios-config';
import { useBackButton } from '@/composables/useBackButton';
import type { CalendarDate } from '@/types/calendar';

const defaultImage = new URL('../images/Default image.jpg', import.meta.url).href;

dayjs.extend(weekOfYear);
dayjs.extend(isoWeek);

type ViewType = 'list' | 'month' | 'week' | 'day';

const api = new AgendaAPI(getApiBaseUrl(), authenticatedAxios);

// Set default view based on screen size
const getDefaultView = (): ViewType => {
    return window.innerWidth <= 600 ? 'list' : 'month';
};

const currentView = ref<ViewType>(getDefaultView());
const isSidebarOpen = ref(false);
const isUploadModalOpen = ref(false);
const isDatePickerOpen = ref(false);
const monthImageUrl = ref<string | null>(null);
const monthImageCache = new Map<number, string>(); // month (1-12) → blob URL

// Handle back button for modals
useBackButton(isUploadModalOpen, () => { isUploadModalOpen.value = false; });
useBackButton(isSidebarOpen, () => { isSidebarOpen.value = false; });

const day = ref<number>(dayjs().isoWeekday());
const dayOfMonth = ref<number>(dayjs().date());
const week = ref<number>(dayjs().week());
const month = ref<number>(dayjs().month());
const year = ref<number>(dayjs().year());

let currentMonth = month.value;

const currentDate = computed(() =>
    dayjs().date(dayOfMonth.value).month(month.value).year(year.value)
);

const dateDisplay = computed(() => {
    if (currentView.value === 'list') {
        return `${currentDate.value.format('D')}, ${currentDate.value.format('dddd')}`;
    } else if (currentView.value === 'month') {
        return `${currentDate.value.format('D')}, ${currentDate.value.format('dddd')}`;
    } else if (currentView.value === 'week') {
        const startOfWeek = currentDate.value.startOf('isoWeek');
        const endOfWeek = currentDate.value.endOf('isoWeek');
        return `${startOfWeek.format('D')} - ${endOfWeek.format('D')}, Week ${currentDate.value.isoWeek()}`;
    } else {
        return `${currentDate.value.format('D')}, ${currentDate.value.format('dddd')}`;
    }
});

const periodDisplay = computed(() => {
    if (currentView.value === 'list') {
        return `${currentDate.value.format('MMMM')} | ${year.value}`;
    } else if (currentView.value === 'month') {
        return `${currentDate.value.format('MMMM')} | ${year.value}`;
    } else if (currentView.value === 'week') {
        const startOfWeek = currentDate.value.startOf('isoWeek');
        const endOfWeek = currentDate.value.endOf('isoWeek');
        if (startOfWeek.month() === endOfWeek.month()) {
            return `${startOfWeek.format('MMMM')} | ${year.value}`;
        } else {
            return `${startOfWeek.format('MMM')} - ${endOfWeek.format('MMM')} | ${year.value}`;
        }
    } else {
        return `${currentDate.value.format('MMMM')} | ${year.value}`;
    }
});

function setView(view: ViewType) {
    currentView.value = view;
}

function changeYear(int: number) {
    year.value += int;
}

function changeMonth(int: number) {
    month.value += int;

    if (month.value > 11) {
        month.value = 0;
        changeYear(1);
    }
    if (month.value < 0) {
        month.value = 11;
        changeYear(-1);
    }

    currentMonth = month.value;
}

function changeWeek(int: number) {
    const newDate = currentDate.value.add(int, 'week');
    dayOfMonth.value = newDate.date();
    month.value = newDate.month();
    year.value = newDate.year();
    week.value = newDate.isoWeek();
    currentMonth = month.value;
}

function changeDay(int: number) {
    const newDate = currentDate.value.add(int, 'day');
    dayOfMonth.value = newDate.date();
    month.value = newDate.month();
    year.value = newDate.year();
    currentMonth = month.value;
}

function navigatePrevious() {
    if (currentView.value === 'list') {
        changeMonth(-1);
    } else if (currentView.value === 'month') {
        changeMonth(-1);
    } else if (currentView.value === 'week') {
        changeWeek(-1);
    } else {
        changeDay(-1);
    }
}

function navigateNext() {
    if (currentView.value === 'list') {
        changeMonth(1);
    } else if (currentView.value === 'month') {
        changeMonth(1);
    } else if (currentView.value === 'week') {
        changeWeek(1);
    } else {
        changeDay(1);
    }
}

function handleDateSelect(d: CalendarDate) {
    dayOfMonth.value = d.date;
    day.value = d.day ?? dayjs().year(d.year ?? year.value).month(d.month ?? month.value).date(d.date).isoWeekday();

    if (!d.thisMonth && d.date >= 24) {
        month.value = currentMonth - 1;
        if (month.value < 0) {
            month.value = 11;
            changeYear(-1);
        }
    }
    if (!d.thisMonth && d.date <= 13) {
        month.value = currentMonth + 1;
        if (month.value > 11) {
            month.value = 0;
            changeYear(1);
        }
    }
    if (d.thisMonth) {
        month.value = currentMonth;
    }

    currentMonth = month.value;
}

function handleVisibleDateChange(date: { date: number; month: number; year: number }) {
    // Update the banner to reflect the currently visible date in list view
    dayOfMonth.value = date.date;
    month.value = date.month;
    year.value = date.year;

    // Update currentMonth for consistency
    currentMonth = date.month;
}

function handleDatePickerSelect(selectedYear: number, selectedMonth: number, selectedDay: number) {
    // Update all date state when user selects from date picker
    year.value = selectedYear;
    month.value = selectedMonth;
    dayOfMonth.value = selectedDay;

    // Update week and day of week
    const selectedDate = dayjs().year(selectedYear).month(selectedMonth).date(selectedDay);
    week.value = selectedDate.isoWeek();
    day.value = selectedDate.isoWeekday();

    // Update currentMonth for consistency
    currentMonth = selectedMonth;
}

const backgroundImageStyle = computed(() => {
    const imageUrl = monthImageUrl.value || defaultImage;
    return {
        backgroundImage: `url(${imageUrl})`,
        backgroundSize: 'cover',
        backgroundPosition: 'center'
    };
});

async function loadMonthImage() {
    const m = month.value + 1; // 1-12

    // Serve from cache — no network round-trip needed
    if (monthImageCache.has(m)) {
        monthImageUrl.value = monthImageCache.get(m)!;
        return;
    }

    try {
        const response = await authenticatedAxios.get(`/api/MonthImages/${m}`, {
            responseType: 'blob'
        });
        const url = URL.createObjectURL(new Blob([response.data]));
        monthImageCache.set(m, url);
        monthImageUrl.value = url;
    } catch {
        // No custom image for this month
        monthImageUrl.value = null;
    }
}

function handleImageUploaded() {
    const m = month.value + 1;
    const old = monthImageCache.get(m);
    if (old) { URL.revokeObjectURL(old); monthImageCache.delete(m); }
    loadMonthImage();
}

async function resetToDefault() {
    const m = month.value + 1;
    try {
        await authenticatedAxios.delete(`/api/MonthImages/${m}`);
    } catch (error: any) {
        if (error.response?.status !== 404) {
            console.error('Failed to reset image:', error);
            return;
        }
    }
    const old = monthImageCache.get(m);
    if (old) { URL.revokeObjectURL(old); monthImageCache.delete(m); }
    monthImageUrl.value = null;
}

// Watch for month changes to load appropriate image
watch(month, () => {
    loadMonthImage();
}, { immediate: true });

// Update body background when month image changes
watch(monthImageUrl, (newImageUrl) => {
    const imageUrl = newImageUrl || defaultImage;
    document.body.style.background = `linear-gradient(rgba(0, 0, 0, 0.5), rgba(0, 0, 0, 0.5)), url(${imageUrl})`;
    document.body.style.backgroundSize = 'cover';
}, { immediate: true });

// Swipe gesture handling
let touchStartX = 0;
let touchStartY = 0;
let touchEndX = 0;
let touchEndY = 0;
let isScrolling = false;

const SWIPE_THRESHOLD = 60; // Minimum horizontal distance for a swipe
const HORIZONTAL_RATIO = 2; // Horizontal movement must be at least 2x vertical movement

function handleTouchStart(event: TouchEvent) {
    touchStartX = event.touches[0].clientX;
    touchStartY = event.touches[0].clientY;
    touchEndX = touchStartX;
    touchEndY = touchStartY;
    isScrolling = false;
}

function handleTouchMove(event: TouchEvent) {
    touchEndX = event.touches[0].clientX;
    touchEndY = event.touches[0].clientY;

    const deltaX = Math.abs(touchEndX - touchStartX);
    const deltaY = Math.abs(touchEndY - touchStartY);

    // Determine if user is scrolling vertically
    // Once we detect scrolling, we lock it in for this gesture
    if (!isScrolling && (deltaX > 10 || deltaY > 10)) {
        isScrolling = deltaY > deltaX;
    }
}

function handleTouchEnd() {
    const deltaX = touchEndX - touchStartX;
    const deltaY = Math.abs(touchEndY - touchStartY);
    const absDeltaX = Math.abs(deltaX);

    // Only trigger navigation if:
    // 1. Not scrolling vertically
    // 2. Horizontal movement exceeds threshold
    // 3. Horizontal movement is significantly larger than vertical (at least 2x)
    if (!isScrolling &&
        absDeltaX > SWIPE_THRESHOLD &&
        absDeltaX > deltaY * HORIZONTAL_RATIO) {

        if (deltaX > 0) {
            // Swipe right - go to previous
            navigatePrevious();
        } else {
            // Swipe left - go to next
            navigateNext();
        }
    }

    // Reset values
    touchStartX = 0;
    touchStartY = 0;
    touchEndX = 0;
    touchEndY = 0;
    isScrolling = false;
}

</script>

<style scoped>
.date-display-container {
    position: relative;
}

.view-toggle-buttons {
    position: absolute;
    bottom: 20px;
    right: 20px;
    display: flex;
    gap: 10px;
    z-index: 10;
}

.image-controls {
    position: absolute;
    top: 20px;
    right: 20px;
    display: flex;
    gap: 8px;
    z-index: 10;
}

.image-control-btn {
    background: rgba(0, 0, 0, 0.3);
    border: none;
    border-radius: 50%;
    width: 44px;
    height: 44px;
    cursor: pointer;
    display: flex;
    align-items: center;
    justify-content: center;
    transition: all 0.2s;
    backdrop-filter: blur(10px);
    color: white;
}

.image-control-btn .material-symbols-outlined {
    font-size: 24px;
}

.image-control-btn:hover {
    background: rgba(0, 0, 0, 0.5);
    transform: scale(1.05);
}

.image-control-btn:active {
    transform: scale(0.95);
}

.settings-menu {
    position: absolute;
    bottom: 20px;
    left: 20px;
    background: rgba(0, 0, 0, 0.3);
    border: none;
    border-radius: 50%;
    width: 48px;
    height: 48px;
    cursor: pointer;
    display: flex;
    align-items: center;
    justify-content: center;
    transition: all 0.2s;
    z-index: 10;
    backdrop-filter: blur(10px);
}

.settings-menu:hover {
    background: rgba(0, 0, 0, 0.5);
    transform: scale(1.05);
}

.settings-menu:active {
    transform: scale(0.95);
}

.btn-view-toggle {
    background: var(--color-primary);
    color: white;
    border: none;
    border-radius: 25px;
    padding: 10px 30px;
    font-size: 14px;
    font-weight: 600;
    cursor: pointer;
    transition: all 0.2s;
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.2);
}

.btn-view-toggle:hover {
    background: var(--color-primary-hover);
    transform: translateY(-1px);
    box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
}

.btn-view-toggle.active {
    background: #2a5f8f;
    box-shadow: inset 0 2px 4px rgba(0, 0, 0, 0.2);
}

.btn-view-toggle.btn-icon {
    padding: 10px 15px;
    display: flex;
    align-items: center;
    justify-content: center;
}

.btn-view-toggle.btn-icon .material-symbols-outlined {
    font-size: 20px;
}

/* Mobile responsive styles */
@media screen and (max-width: 800px) {
    .view-toggle-buttons {
        bottom: 10px;
        right: 10px;
        gap: 5px;
    }

    .btn-view-toggle {
        padding: 8px 20px;
        font-size: 12px;
        border-radius: 20px;
    }

    .btn-view-toggle.btn-icon {
        padding: 8px 12px;
    }

    .btn-view-toggle.btn-icon .material-symbols-outlined {
        font-size: 18px;
    }

    .image-controls {
        top: 10px;
        right: 10px;
        gap: 5px;
    }

    .image-control-btn {
        width: 36px;
        height: 36px;
    }

    .image-control-btn .material-symbols-outlined {
        font-size: 20px;
    }

    .settings-menu {
        bottom: 10px;
        left: 10px;
        width: 40px;
        height: 40px;
    }

    .settings-menu svg {
        width: 24px;
        height: 24px;
    }
}
</style>
