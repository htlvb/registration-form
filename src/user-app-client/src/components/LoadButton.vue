<script setup lang="ts">
import LoadingBar from './LoadingBar.vue'

defineProps<{
  loading?: boolean
  disabled?: boolean
}>()
</script>

<template>
  <button :disabled="loading || disabled" class="button !grid place-content-center">
    <div class="col-span-full row-span-full" :class="{ 'opacity-50': loading }"><slot></slot></div>
    <LoadingBar v-if="loading" class="col-span-full row-span-full" type="minimal" />
  </button>
</template>