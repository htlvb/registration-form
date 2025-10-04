<script setup lang="ts">
import * as DataTransfer from '@/DataTransfer.ts'
import EventView from './EventView.vue'

defineProps<{
  events: DataTransfer.Event[]
}>()
</script>
<template>
  <InfoBar v-if="events.length === 0">Keine Events vorhanden.</InfoBar>
  <div v-else class="flex flex-col gap-2">
    <EventView v-for="event in events" :key="event.key" :event="event" />
  </div>
</template>