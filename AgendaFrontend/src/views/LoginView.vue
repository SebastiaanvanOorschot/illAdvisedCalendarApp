<template>
  <div class="login-container">
    <div class="login-card">
      <h1>Welcome to IllAdvisedCalendar</h1>
      <p class="subtitle">Sign in to access your calendar</p>

      <div v-if="error" class="error-message">
        {{ error }}
      </div>

      <div id="google-signin-button"></div>

      <p class="privacy-note">
        By signing in, you agree to our Terms of Service and Privacy Policy
      </p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';
import { useAuth } from '@/composables/useAuth';

const router = useRouter();
const { login } = useAuth();
const error = ref<string | null>(null);

onMounted(() => {
  // Initialize Google Sign-In
  const initGoogleSignIn = () => {
    if (typeof google !== 'undefined' && google.accounts) {
      google.accounts.id.initialize({
        client_id: import.meta.env.VITE_GOOGLE_CLIENT_ID,
        callback: handleGoogleResponse
      });

      google.accounts.id.renderButton(
        document.getElementById('google-signin-button')!,
        {
          type: 'standard',
          theme: 'outline',
          size: 'large',
          text: 'signin_with',
          width: 300
        }
      );
    } else {
      setTimeout(initGoogleSignIn, 100);
    }
  };

  initGoogleSignIn();
});

const handleGoogleResponse = async (response: any) => {
  try {
    error.value = null;
    await login(response.credential);
    router.push('/agenda');
  } catch (err: any) {
    error.value = err.message || 'Failed to sign in with Google';
  }
};
</script>

<style scoped>
.login-container {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 100vh;
  background: linear-gradient(135deg, var(--color-accent) 0%, var(--color-accent-2) 100%);
}

.login-card {
  background: white;
  padding: 3rem 2rem;
  border-radius: 12px;
  box-shadow: 0 10px 40px rgba(0, 0, 0, 0.1);
  text-align: center;
  max-width: 400px;
  width: 90%;
}

h1 {
  font-size: 1.8rem;
  margin-bottom: 0.5rem;
  color: var(--color-text);
}

.subtitle {
  color: var(--color-text-muted);
  margin-bottom: 2rem;
}

#google-signin-button {
  display: flex;
  justify-content: center;
  margin: 2rem 0;
}

.error-message {
  background: #fee;
  color: #c33;
  padding: 1rem;
  border-radius: 6px;
  margin-bottom: 1rem;
}

.privacy-note {
  font-size: 0.85rem;
  color: var(--color-text-subtle);
  margin-top: 2rem;
}
</style>
