import { ref, computed } from 'vue';
import { jwtDecode } from 'jwt-decode';
import { AgendaAPI, AuthResponse, GoogleLoginRequest } from '@/api/agenda-api-swagger';
import { authenticatedAxios, getApiBaseUrl } from '@/api/axios-config';

interface User {
  id: number;
  email: string;
  name: string;
  profilePictureUrl?: string;
}

interface JwtPayload {
  sub: string;
  email: string;
  name: string;
  exp: number;
}

const accessToken = ref<string | null>(localStorage.getItem('accessToken'));
const refreshToken = ref<string | null>(localStorage.getItem('refreshToken'));
const user = ref<User | null>(null);
const isLoading = ref(false);
const error = ref<string | null>(null);

// Initialize user from token on load
if (accessToken.value) {
  try {
    const decoded = jwtDecode<JwtPayload>(accessToken.value);
    user.value = {
      id: parseInt(decoded.sub),
      email: decoded.email,
      name: decoded.name
    };
  } catch (e) {
    // Token is invalid, clear it
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    accessToken.value = null;
    refreshToken.value = null;
  }
}

export function useAuth() {
  const api = new AgendaAPI(getApiBaseUrl(), authenticatedAxios);

  const isAuthenticated = computed(() => !!accessToken.value && !!user.value);

  const login = async (googleIdToken: string): Promise<void> => {
    isLoading.value = true;
    error.value = null;

    try {
      console.log('Creating GoogleLoginRequest with token:', googleIdToken.substring(0, 20) + '...');
      const request = new GoogleLoginRequest({
        googleIdToken: googleIdToken
      });
      console.log('Request object created:', request);
      console.log('API base URL:', getApiBaseUrl());
      console.log('Calling api.googleLogin...');

      const response: AuthResponse = await api.googleLogin(request);

      console.log('API response received:', response);

      // Store tokens
      accessToken.value = response.accessToken;
      refreshToken.value = response.refreshToken;
      localStorage.setItem('accessToken', response.accessToken);
      localStorage.setItem('refreshToken', response.refreshToken);

      // Set user
      user.value = {
        id: response.user.id,
        email: response.user.email,
        name: response.user.name,
        profilePictureUrl: response.user.profilePictureUrl
      };
    } catch (err: any) {
      console.error('Full error object:', err);
      console.error('Error message:', err.message);
      console.error('Error stack:', err.stack);
      console.error('Error response:', err.response);
      error.value = err.message || 'Login failed';
      throw err;
    } finally {
      isLoading.value = false;
    }
  };

  const refreshAccessToken = async (): Promise<boolean> => {
    if (!refreshToken.value) {
      return false;
    }

    try {
      const response: AuthResponse = await api.refresh({ refreshToken: refreshToken.value });

      // Update tokens
      accessToken.value = response.accessToken;
      refreshToken.value = response.refreshToken;
      localStorage.setItem('accessToken', response.accessToken);
      localStorage.setItem('refreshToken', response.refreshToken);

      // Update user
      user.value = {
        id: response.user.id,
        email: response.user.email,
        name: response.user.name,
        profilePictureUrl: response.user.profilePictureUrl
      };

      return true;
    } catch (err) {
      // Refresh failed, log user out
      logout();
      return false;
    }
  };

  const logout = async (): Promise<void> => {
    try {
      if (refreshToken.value) {
        await api.logout({ refreshToken: refreshToken.value });
      }
    } catch (err) {
      // Ignore errors on logout
    } finally {
      // Clear local state
      accessToken.value = null;
      refreshToken.value = null;
      user.value = null;
      localStorage.removeItem('accessToken');
      localStorage.removeItem('refreshToken');
    }
  };

  const getAccessToken = (): string | null => {
    return accessToken.value;
  };

  return {
    user: computed(() => user.value),
    isAuthenticated,
    isLoading: computed(() => isLoading.value),
    error: computed(() => error.value),
    login,
    logout,
    refreshAccessToken,
    getAccessToken
  };
}
