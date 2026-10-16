/// <reference types="vitest/config" />
import { fileURLToPath, URL } from 'node:url'

import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'
import Components from 'unplugin-vue-components/vite'
import {BootstrapVueNextResolver} from 'unplugin-vue-components/resolvers'


// https://vitejs.dev/config/
export default defineConfig(({ mode }) => {
  return {
    base: '/',
    plugins: [
      vue(),
      Components({
        resolvers: [BootstrapVueNextResolver()],
      }),
    ],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
      }
    },
    test: {
      environment: 'jsdom',
      include: ['src/**/__tests__/**/*.spec.ts'],
      restoreMocks: true,
      unstubEnvs: true,
      unstubGlobals: true,
      coverage: {
        provider: 'v8',
        // without this, v8 maps SFC lines loosely and a component that never ran can show as covered
        experimentalAstAwareRemapping: true,
        // lcov paths relative to the repo root, which is where Sonar resolves them from
        reporter: ['text', ['lcov', { projectRoot: '..' }]],
        include: ['src/**/*.{ts,vue}'],
        exclude: ['src/**/__tests__/**', 'src/main.ts', 'src/api/agenda-api-swagger.ts', 'src/**/*.d.ts']
      }
    }
  }
})
