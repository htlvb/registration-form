<script setup lang="ts">
withDefaults(
  defineProps<{
    retryTitle?: string
    type?: 'inline' | 'banner'
  }>(), {
    type: 'banner'
  }
)
defineEmits<{
  (e: 'retry') : void
}>()
</script>

<template>
  <div v-if="type === 'banner'" class="flex justify-center items-center gap-2 p-4 rounded">
    <div class="font-semibold"><slot /></div>
    <button class="button button-htlvb bg-white" @click="$emit('retry')">{{ retryTitle ?? 'Erneut versuchen' }}</button>
  </div>
  <div v-if="type === 'inline'" class="flex justify-center items-center gap-2 rounded">
    <div class="font-semibold"><slot /></div>
    <button class="button button-htlvb bg-white" @click="$emit('retry')">{{ retryTitle ?? 'Erneut versuchen' }}</button>
  </div>
</template>
