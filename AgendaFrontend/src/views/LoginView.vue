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
      console.log('Initializing Google Sign-In...');
      google.accounts.id.initialize({
        client_id: '210057685866-ektli26tc1i7kv46ftc9bmc6eo2g1ggv.apps.googleusercontent.com',
        callback: handleGoogleResponse
      });

      google.accounts.id.renderButton(
        document.getElementById('google-signin-button')!,
        {
          theme: 'outline',
          size: 'large',
          text: 'signin_with',
          width: 300
        }
      );
      console.log('Google Sign-In initialized successfully');
    } else {
      console.log('Google Sign-In library not yet loaded, retrying...');
      setTimeout(initGoogleSignIn, 100);
    }
  };

  initGoogleSignIn();
});

const handleGoogleResponse = async (response: any) => {
  console.log('Google Sign-In callback triggered', response);
  try {
    error.value = null;
    console.log('Calling login API...');
    await login(response.credential);
    console.log('Login successful, redirecting to /agenda');
    // Redirect to calendar on successful login
    router.push('/agenda');
  } catch (err: any) {
    console.error('Login failed:', err);
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
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
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
  color: #333;
}

.subtitle {
  color: #666;
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
  color: #999;
  margin-top: 2rem;
}
</style>
