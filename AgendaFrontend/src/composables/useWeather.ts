import { ref } from 'vue';
import axios from 'axios';
import { getApiBaseUrl } from '@/api/axios-config';

interface WeatherForecast {
    date: string;
    temperatureMax: number;
    temperatureMin: number;
    weatherCode: number;
    precipitationProbability: number;
}

interface WeatherResponse {
    latitude: number;
    longitude: number;
    daily: WeatherForecast[];
}

// Shared state across all components
const weatherData = ref<WeatherResponse | null>(null);
const loading = ref(false);
const error = ref<string | null>(null);
const lastFetchTime = ref<number>(0);
const lastLocation = ref<{ latitude: number; longitude: number } | null>(null);

// Cache duration: 5 minutes
const CACHE_DURATION = 5 * 60 * 1000;

// Calendar day as YYYY-MM-DD in the user's own timezone. Deliberately not
// toISOString(), which converts to UTC first and therefore reports the previous
// day for every timezone east of UTC - that shifted the whole forecast a day.
function toLocalDateKey(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
}

// The API queries Open-Meteo with timezone=auto, so the leading YYYY-MM-DD of
// each forecast entry already is the local calendar day it describes.
function toForecastDateKey(value: string): string {
    return value.slice(0, 10);
}

export function useWeather() {
    async function fetchWeather(latitude: number, longitude: number, forceRefresh = false) {
        const now = Date.now();
        const isSameLocation = lastLocation.value?.latitude === latitude && lastLocation.value?.longitude === longitude;
        const isCacheValid = (now - lastFetchTime.value) < CACHE_DURATION;

        // Return cached data if valid and location hasn't changed
        if (!forceRefresh && isSameLocation && isCacheValid && weatherData.value) {
            return;
        }

        loading.value = true;
        error.value = null;

        try {
            const response = await axios.get<WeatherResponse>(
                `${getApiBaseUrl()}/api/Weather/forecast`,
                {
                    params: { latitude, longitude }
                }
            );

            weatherData.value = response.data;
            lastFetchTime.value = now;
            lastLocation.value = { latitude, longitude };
        } catch (err) {
            console.error('Error fetching weather:', err);
            error.value = 'Failed to fetch weather data';
            weatherData.value = null;
        } finally {
            loading.value = false;
        }
    }

    function getWeatherForDate(date: Date): WeatherForecast | null {
        if (!weatherData.value) return null;

        const dateKey = toLocalDateKey(date);
        return weatherData.value.daily.find(d => toForecastDateKey(d.date) === dateKey) || null;
    }

    return {
        weatherData,
        loading,
        error,
        fetchWeather,
        getWeatherForDate
    };
}
