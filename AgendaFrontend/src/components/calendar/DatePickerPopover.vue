<template>
  <div v-if="isOpen" class="date-picker-popover" ref="popoverRef">
    <div class="picker-header">
      <button @click="decrementYear" class="year-nav-btn">&lt;</button>
      <select v-model="selectedYear" class="year-select">
        <option v-for="y in yearRange" :key="y" :value="y">{{ y }}</option>
      </select>
      <button @click="incrementYear" class="year-nav-btn">&gt;</button>
    </div>

    <div class="months-grid">
      <button
        v-for="(monthName, index) in monthNames"
        :key="index"
        @click="selectMonth(index)"
        :class="['month-btn', { 'selected': index === selectedMonth && selectedYear === currentYear }]"
      >
        {{ monthName }}
      </button>
    </div>

    <div class="days-grid">
      <div class="day-header" v-for="dayName in dayNames" :key="dayName">
        {{ dayName }}
      </div>

      <button
        v-for="day in calendarDays"
        :key="day.key"
        @click="selectDay(day)"
        :class="[
          'day-btn',
          {
            'other-month': !day.isCurrentMonth,
            'selected': day.isSelected,
            'today': day.isToday
          }
        ]"
        :disabled="!day.isCurrentMonth"
      >
        {{ day.date }}
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, onUnmounted } from 'vue';
import dayjs from 'dayjs';

interface Props {
  isOpen: boolean;
  currentMonth: number;
  currentYear: number;
  currentDay: number;
}

interface Emits {
  (e: 'close'): void;
  (e: 'dateSelect', year: number, month: number, day: number): void;
}

const props = defineProps<Props>();
const emit = defineEmits<Emits>();

const popoverRef = ref<HTMLElement | null>(null);
const selectedYear = ref(props.currentYear);
const selectedMonth = ref(props.currentMonth);

const monthNames = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const dayNames = ['Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa', 'Su'];

// Generate year range (10 years before and after current year)
const yearRange = computed(() => {
  const currentYear = dayjs().year();
  const years: number[] = [];
  for (let i = currentYear - 10; i <= currentYear + 10; i++) {
    years.push(i);
  }
  return years;
});

// Calculate calendar days for the selected month
const calendarDays = computed(() => {
  const firstDay = dayjs().year(selectedYear.value).month(selectedMonth.value).date(1);
  const lastDay = firstDay.endOf('month');
  const startDay = firstDay.isoWeekday(); // 1 = Monday, 7 = Sunday
  const days: Array<{
    date: number;
    isCurrentMonth: boolean;
    isSelected: boolean;
    isToday: boolean;
    key: string;
  }> = [];

  // Add days from previous month
  const prevMonthLastDay = firstDay.subtract(1, 'day');
  for (let i = startDay - 1; i > 0; i--) {
    const date = prevMonthLastDay.subtract(i - 1, 'day');
    days.push({
      date: date.date(),
      isCurrentMonth: false,
      isSelected: false,
      isToday: false,
      key: `prev-${date.date()}`
    });
  }

  // Add days from current month
  const today = dayjs();
  for (let i = 1; i <= lastDay.date(); i++) {
    const date = firstDay.date(i);
    days.push({
      date: i,
      isCurrentMonth: true,
      isSelected:
        i === props.currentDay &&
        selectedMonth.value === props.currentMonth &&
        selectedYear.value === props.currentYear,
      isToday:
        i === today.date() &&
        selectedMonth.value === today.month() &&
        selectedYear.value === today.year(),
      key: `current-${i}`
    });
  }

  // Add days from next month to fill the grid
  const remainingSlots = 42 - days.length; // 6 rows × 7 days
  for (let i = 1; i <= remainingSlots; i++) {
    days.push({
      date: i,
      isCurrentMonth: false,
      isSelected: false,
      isToday: false,
      key: `next-${i}`
    });
  }

  return days;
});

const decrementYear = () => {
  selectedYear.value--;
};

const incrementYear = () => {
  selectedYear.value++;
};

const selectMonth = (monthIndex: number) => {
  selectedMonth.value = monthIndex;
};

const selectDay = (day: any) => {
  if (!day.isCurrentMonth) return;

  emit('dateSelect', selectedYear.value, selectedMonth.value, day.date);
  emit('close');
};

// Handle click outside to close
const handleClickOutside = (event: MouseEvent) => {
  if (popoverRef.value && !popoverRef.value.contains(event.target as Node)) {
    emit('close');
  }
};

// Watch for prop changes to update internal state
watch(() => props.currentYear, (newYear) => {
  selectedYear.value = newYear;
});

watch(() => props.currentMonth, (newMonth) => {
  selectedMonth.value = newMonth;
});

// Add/remove click outside listener
watch(() => props.isOpen, (isOpen) => {
  if (isOpen) {
    setTimeout(() => {
      document.addEventListener('click', handleClickOutside);
    }, 0);
  } else {
    document.removeEventListener('click', handleClickOutside);
  }
});

onUnmounted(() => {
  document.removeEventListener('click', handleClickOutside);
});
</script>

<style scoped>
.date-picker-popover {
  position: fixed;
  top: 120px;
  left: 400px;
  background: linear-gradient(135deg, rgba(30, 30, 50, 0.98) 0%, rgba(20, 20, 40, 0.98) 100%);
  border: 1px solid rgba(255, 255, 255, 0.15);
  border-radius: 12px;
  padding: 16px;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.4);
  z-index: 1000;
  width: 300px;
  backdrop-filter: blur(10px);
}

/* Mobile responsive positioning */
@media screen and (max-width: 800px) {
  .date-picker-popover {
    position: fixed;
    top: 50%;
    left: 50%;
    transform: translate(-50%, -50%);
    width: 90%;
    max-width: 320px;
  }
}

.picker-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 15px;
  gap: 10px;
}

.year-nav-btn {
  background: rgba(255, 255, 255, 0.1);
  border: 1px solid rgba(255, 255, 255, 0.2);
  color: white;
  width: 32px;
  height: 32px;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s;
  font-size: 16px;
}

.year-nav-btn:hover {
  background: rgba(255, 255, 255, 0.2);
  transform: scale(1.05);
}

.year-select {
  flex: 1;
  background: rgba(255, 255, 255, 0.1);
  border: 1px solid rgba(255, 255, 255, 0.2);
  color: white;
  padding: 8px 12px;
  border-radius: 6px;
  font-size: 16px;
  font-weight: 600;
  cursor: pointer;
  text-align: center;
  transition: all 0.2s;
}

.year-select:hover {
  background: rgba(255, 255, 255, 0.15);
}

.year-select option {
  background: #1a1a2e;
  color: white;
}

.months-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 8px;
  margin-bottom: 15px;
}

.month-btn {
  background: rgba(255, 255, 255, 0.08);
  border: 1px solid rgba(255, 255, 255, 0.15);
  color: white;
  padding: 10px;
  border-radius: 6px;
  cursor: pointer;
  font-size: 13px;
  font-weight: 500;
  transition: all 0.2s;
}

.month-btn:hover {
  background: rgba(255, 255, 255, 0.15);
  transform: translateY(-1px);
}

.month-btn.selected {
  background: rgba(100, 150, 255, 0.4);
  border-color: rgba(100, 150, 255, 0.6);
  font-weight: 600;
}

.days-grid {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  gap: 4px;
}

.day-header {
  color: rgba(255, 255, 255, 0.6);
  font-size: 11px;
  font-weight: 600;
  text-align: center;
  padding: 8px 0;
}

.day-btn {
  background: rgba(255, 255, 255, 0.08);
  border: 1px solid rgba(255, 255, 255, 0.1);
  color: white;
  padding: 8px;
  border-radius: 6px;
  cursor: pointer;
  font-size: 13px;
  transition: all 0.2s;
  aspect-ratio: 1;
  display: flex;
  align-items: center;
  justify-content: center;
}

.day-btn:hover:not(:disabled) {
  background: rgba(255, 255, 255, 0.2);
  transform: scale(1.05);
}

.day-btn.other-month {
  color: rgba(255, 255, 255, 0.3);
  background: rgba(255, 255, 255, 0.03);
  border-color: transparent;
}

.day-btn.selected {
  background: rgba(100, 150, 255, 0.5);
  border-color: rgba(100, 150, 255, 0.8);
  font-weight: 700;
}

.day-btn.today {
  border-color: rgba(255, 200, 100, 0.8);
  font-weight: 600;
}

.day-btn:disabled {
  cursor: not-allowed;
}
</style>
