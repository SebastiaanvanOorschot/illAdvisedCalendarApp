<template>
    <div class="weather-triangle" :class="`weather-${weatherType}`" :title="`${tempMin}° - ${tempMax}°C`">
        <!-- Sunny (clear sky) -->
        <svg v-if="weatherType === 'sunny'" class="weather-icon" viewBox="0 0 100 100" xmlns="http://www.w3.org/2000/svg">
            <!-- Simple sun with rays -->
            <g class="sun-rays">
                <line class="sun-ray ray-1" x1="50" y1="12" x2="50" y2="5" stroke="var(--color-white)" stroke-width="2.5" stroke-linecap="round" />
                <line class="sun-ray ray-2" x1="64" y1="16" x2="69" y2="11" stroke="var(--color-white)" stroke-width="2.5" stroke-linecap="round" />
                <line class="sun-ray ray-3" x1="68" y1="30" x2="75" y2="30" stroke="var(--color-white)" stroke-width="2.5" stroke-linecap="round" />
                <line class="sun-ray ray-4" x1="64" y1="44" x2="69" y2="49" stroke="var(--color-white)" stroke-width="2.5" stroke-linecap="round" />
                <line class="sun-ray ray-5" x1="50" y1="48" x2="50" y2="55" stroke="var(--color-white)" stroke-width="2.5" stroke-linecap="round" />
                <line class="sun-ray ray-6" x1="36" y1="44" x2="31" y2="49" stroke="var(--color-white)" stroke-width="2.5" stroke-linecap="round" />
                <line class="sun-ray ray-7" x1="32" y1="30" x2="25" y2="30" stroke="var(--color-white)" stroke-width="2.5" stroke-linecap="round" />
                <line class="sun-ray ray-8" x1="36" y1="16" x2="31" y2="11" stroke="var(--color-white)" stroke-width="2.5" stroke-linecap="round" />
            </g>
            <circle class="sun-center" cx="50" cy="30" r="12" fill="var(--color-white)" />
        </svg>

        <!-- Partly Cloudy -->
        <svg v-else-if="weatherType === 'partly-cloudy'" class="weather-icon" viewBox="0 0 100 100" xmlns="http://www.w3.org/2000/svg">
            <!-- Single cloud - simple and clear -->
            <ellipse class="cloud-simple cloud-drift" cx="50" cy="30" rx="18" ry="11" fill="var(--color-white)" opacity="0.95" />
            <ellipse class="cloud-simple cloud-drift" cx="38" cy="34" rx="15" ry="9" fill="var(--color-white)" opacity="0.9" />
            <ellipse class="cloud-simple cloud-drift" cx="62" cy="35" rx="13" ry="8" fill="var(--color-white)" opacity="0.85" />
        </svg>

        <!-- Cloudy -->
        <svg v-else-if="weatherType === 'cloudy'" class="weather-icon" viewBox="0 0 100 100" xmlns="http://www.w3.org/2000/svg">
            <!-- Two overlapping cloud groups for distinction -->
            <!-- First cloud group (back) -->
            <ellipse class="cloud-simple cloud-float" cx="45" cy="24" rx="16" ry="10" fill="var(--color-white)" opacity="0.85" />
            <ellipse class="cloud-simple cloud-float" cx="35" cy="28" rx="13" ry="8" fill="var(--color-white)" opacity="0.8" />
            <ellipse class="cloud-simple cloud-float" cx="55" cy="28" rx="11" ry="7" fill="var(--color-white)" opacity="0.75" />
            <!-- Second cloud group (front) -->
            <ellipse class="cloud-simple cloud-float" cx="52" cy="32" rx="17" ry="10" fill="var(--color-white)" opacity="0.95" />
            <ellipse class="cloud-simple cloud-float" cx="42" cy="36" rx="14" ry="9" fill="var(--color-white)" opacity="0.9" />
            <ellipse class="cloud-simple cloud-float" cx="62" cy="37" rx="12" ry="8" fill="var(--color-white)" opacity="0.85" />
        </svg>

        <!-- Rainy -->
        <svg v-else-if="weatherType === 'rainy'" class="weather-icon" viewBox="0 0 100 100" xmlns="http://www.w3.org/2000/svg">
            <!-- Cloud -->
            <ellipse class="cloud-simple" cx="50" cy="22" rx="16" ry="10" fill="var(--color-white)" opacity="0.9" />
            <ellipse class="cloud-simple" cx="40" cy="26" rx="12" ry="8" fill="var(--color-white)" opacity="0.9" />
            <ellipse class="cloud-simple" cx="60" cy="26" rx="10" ry="7" fill="var(--color-white)" opacity="0.9" />
            <!-- Rain drops -->
            <line class="rain-drop rain-drop-1" x1="42" y1="38" x2="40" y2="50" stroke="var(--color-white)" stroke-width="2.5" stroke-linecap="round" />
            <line class="rain-drop rain-drop-2" x1="50" y1="38" x2="48" y2="50" stroke="var(--color-white)" stroke-width="2.5" stroke-linecap="round" />
            <line class="rain-drop rain-drop-3" x1="58" y1="38" x2="56" y2="50" stroke="var(--color-white)" stroke-width="2.5" stroke-linecap="round" />
        </svg>

        <!-- Stormy -->
        <svg v-else-if="weatherType === 'stormy'" class="weather-icon" viewBox="0 0 100 100" xmlns="http://www.w3.org/2000/svg">
            <!-- Dark cloud -->
            <ellipse class="cloud-simple" cx="50" cy="22" rx="18" ry="11" fill="var(--color-white)" opacity="0.85" />
            <ellipse class="cloud-simple" cx="38" cy="26" rx="14" ry="9" fill="var(--color-white)" opacity="0.85" />
            <ellipse class="cloud-simple" cx="62" cy="26" rx="12" ry="8" fill="var(--color-white)" opacity="0.85" />
            <!-- Lightning bolt -->
            <polygon class="lightning" points="50,32 47,42 49,42 45,54 53,40 51,40 54,32" fill="#FFD700" />
        </svg>

        <!-- Snowy -->
        <svg v-else-if="weatherType === 'snowy'" class="weather-icon" viewBox="0 0 100 100" xmlns="http://www.w3.org/2000/svg">
            <!-- Cloud -->
            <ellipse class="cloud-simple" cx="50" cy="22" rx="16" ry="10" fill="var(--color-white)" opacity="0.9" />
            <ellipse class="cloud-simple" cx="40" cy="26" rx="12" ry="8" fill="var(--color-white)" opacity="0.9" />
            <ellipse class="cloud-simple" cx="60" cy="26" rx="10" ry="7" fill="var(--color-white)" opacity="0.9" />
            <!-- Snowflakes -->
            <text class="snowflake snowflake-1" x="34" y="45" fill="var(--color-white)" font-size="12">❄</text>
            <text class="snowflake snowflake-2" x="44" y="50" fill="var(--color-white)" font-size="12">❄</text>
            <text class="snowflake snowflake-3" x="54" y="45" fill="var(--color-white)" font-size="12">❄</text>
        </svg>

        <!-- Temp display -->
        <div class="temp-display">{{ tempMax }}°</div>
    </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';

interface Props {
    weatherCode: number;
    tempMin: number;
    tempMax: number;
    date?: Date;
}

const props = defineProps<Props>();

// Map WMO weather codes to weather types
// https://open-meteo.com/en/docs
const weatherType = computed(() => {
    const code = props.weatherCode;

    // Clear sky
    if (code === 0 || code === 1) return 'sunny';

    // Partly cloudy
    if (code === 2) return 'partly-cloudy';

    // Overcast
    if (code === 3) return 'cloudy';

    // Fog
    if (code >= 45 && code <= 48) return 'cloudy';

    // Drizzle
    if (code >= 51 && code <= 55) return 'rainy';

    // Freezing drizzle
    if (code >= 56 && code <= 57) return 'snowy';

    // Rain
    if (code >= 61 && code <= 67) return 'rainy';

    // Snow
    if (code >= 71 && code <= 77) return 'snowy';

    // Rain showers
    if (code >= 80 && code <= 82) return 'rainy';

    // Snow showers
    if (code >= 85 && code <= 86) return 'snowy';

    // Thunderstorm
    if (code >= 95 && code <= 99) return 'stormy';

    return 'partly-cloudy';
});
</script>

<style scoped>
.weather-triangle {
    position: absolute;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    pointer-events: none;
    cursor: help;
    overflow: hidden;
    z-index: 0;
}

.weather-triangle::before {
    content: '';
    position: absolute;
    top: -50%;
    right: -50%;
    width: 200%;
    height: 200%;
    border-radius: 50%;
}

/* Gradient backgrounds for each weather type - fade from top-right corner along the curve */
.weather-sunny::before {
    background: radial-gradient(circle at 75% 25%, #FFD700 0%, #FFE066 20%, rgba(255, 242, 179, 0.3) 40%, transparent 60%);
}

.weather-partly-cloudy::before {
    background: radial-gradient(circle at 75% 25%, #87CEEB 0%, #A8D8F0 20%, rgba(212, 234, 247, 0.3) 40%, transparent 60%);
}

.weather-cloudy::before {
    background: radial-gradient(circle at 75% 25%, #A9A9A9 0%, #C8C8C8 20%, rgba(232, 232, 232, 0.3) 40%, transparent 60%);
}

.weather-rainy::before {
    background: radial-gradient(circle at 75% 25%, var(--color-primary) 0%, #6FA8E8 20%, rgba(180, 212, 241, 0.3) 40%, transparent 60%);
}

.weather-stormy::before {
    background: radial-gradient(circle at 75% 25%, #5A6C7D 0%, #7A8A99 20%, rgba(180, 189, 198, 0.3) 40%, transparent 60%);
}

.weather-snowy::before {
    background: radial-gradient(circle at 75% 25%, #B3D9E8 0%, #D4E9F2 20%, rgba(239, 247, 250, 0.3) 40%, transparent 60%);
}

.weather-icon {
    position: absolute;
    top: 3px;
    right: 12px;
    width: 55px;
    height: 55px;
    filter: drop-shadow(0 2px 4px rgba(0, 0, 0, 0.25));
    z-index: 1;
    pointer-events: all;
}

.temp-display {
    position: absolute;
    top: 3px;
    right: 72px;
    font-size: 16px;
    font-weight: 800;
    color: var(--color-white);
    text-shadow:
        0 1px 3px rgba(0, 0, 0, 0.6),
        0 0 6px rgba(0, 0, 0, 0.4);
    pointer-events: none;
    z-index: 1;
    letter-spacing: -0.5px;
}

/* Sun animations */
.sun-center {
    transform-origin: center;
    transform-box: fill-box;
}

.sun-rays {
    transform-origin: 50px 30px;
    animation: rays-expand 3s ease-in-out infinite;
}

.sun-ray {
    animation: ray-shine 3s ease-in-out infinite;
}

@keyframes rays-expand {
    0%, 100% {
        transform: scale(1);
        opacity: 0.9;
    }
    50% {
        transform: scale(1.15);
        opacity: 1;
    }
}

@keyframes ray-shine {
    0%, 100% { opacity: 0.7; }
    50% { opacity: 1; }
}

/* Cloud animations */
.cloud-drift {
    animation: drift 6s ease-in-out infinite;
}

.cloud-float {
    animation: float 8s ease-in-out infinite;
}

.cloud-simple {
    animation: float-slow 10s ease-in-out infinite;
}

@keyframes drift {
    0%, 100% { transform: translateX(0); }
    50% { transform: translateX(2px); }
}

@keyframes float {
    0%, 100% { transform: translateY(0); }
    50% { transform: translateY(-2px); }
}

@keyframes float-slow {
    0%, 100% { transform: translateY(0); }
    50% { transform: translateY(-1px); }
}

/* Rain animations */
.rain-drop {
    animation: fall 1.5s linear infinite;
    opacity: 0;
}

.rain-drop-1 {
    animation-delay: 0s;
}

.rain-drop-2 {
    animation-delay: 0.5s;
}

.rain-drop-3 {
    animation-delay: 1s;
}

@keyframes fall {
    0% { opacity: 0; transform: translateY(0); }
    10% { opacity: 0.9; }
    90% { opacity: 0.9; }
    100% { opacity: 0; transform: translateY(12px); }
}

/* Lightning animation */
.lightning {
    animation: flash 3s ease-in-out infinite;
}

@keyframes flash {
    0%, 90%, 100% { opacity: 0; }
    91%, 93% { opacity: 1; }
    92% { opacity: 0.4; }
}

/* Snowflake animations */
.snowflake {
    animation: snow-fall 1.5s linear infinite;
    opacity: 0;
}

.snowflake-1 {
    animation-delay: 0s;
}

.snowflake-2 {
    animation-delay: 0.5s;
}

.snowflake-3 {
    animation-delay: 1s;
}

@keyframes snow-fall {
    0% { opacity: 0; transform: translateY(0); }
    10% { opacity: 0.9; }
    90% { opacity: 0.9; }
    100% { opacity: 0; transform: translateY(12px); }
}
</style>
