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

        const dateStr = date.toISOString().split('T')[0];
        return weatherData.value.daily.find(d => d.date.startsWith(dateStr)) || null;
    }

    return {
        weatherData,
        loading,
        error,
        fetchWeather,
        getWeatherForDate
    };
}
